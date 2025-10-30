using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using BitcrackRandomiser.Enums;
using BitcrackRandomiser.Models;
using BitcrackRandomiser.Services;

namespace BitcrackRandomiser.Services.PoolService
{
    internal sealed class BackendPoolClient : IDisposable
    {
        private readonly Setting _settings;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
        private readonly SemaphoreSlim _registrationLock = new(1, 1);
        private readonly Dictionary<int, WorkerState> _workers = new();

        private sealed class WorkerState
        {
            public Guid ClientId { get; set; }
            public string ClientToken { get; set; } = string.Empty;
            public BackendRangeAssignment? CurrentRange { get; set; }
            public DateTime RegisteredAtUtc { get; set; }
        }

        internal sealed class BackendRangeAssignment
        {
            public Guid RangeId { get; init; }
            public string Puzzle { get; init; } = string.Empty;
            public string PrefixStart { get; init; } = string.Empty;
            public string PrefixEnd { get; init; } = string.Empty;
            public string RangeStart { get; init; } = string.Empty;
            public string RangeEnd { get; init; } = string.Empty;
            public int ChunkSize { get; init; }
            public double ProgressPercent { get; set; }
            public string TargetAddress { get; init; } = string.Empty;
            public string WorkloadStartSuffix { get; init; } = string.Empty;
            public string WorkloadEndSuffix { get; init; } = string.Empty;
        }

        public BackendPoolClient(Setting settings)
        {
            _settings = settings;
            if (string.IsNullOrWhiteSpace(settings.BackendBaseUrl))
                throw new InvalidOperationException("Backend base URL is not configured.");

            var baseUrl = settings.BackendBaseUrl.Trim();
            if (!baseUrl.EndsWith('/'))
                baseUrl += "/";

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(20)
            };
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<BackendRangeAssignment?> ClaimRangeAsync(int gpuIndex, CancellationToken cancellationToken)
        {
            var worker = await EnsureWorkerAsync(gpuIndex, cancellationToken).ConfigureAwait(false);
            if (worker.CurrentRange is null)
            {
                worker.CurrentRange = await RequestRangeAsync(worker, gpuIndex, cancellationToken).ConfigureAwait(false);
            }
            return worker.CurrentRange;
        }

        public async Task ReportProgressAsync(int gpuIndex, Guid rangeId, double progressPercent, bool markComplete, double? speedKeysPerSecond, int cardsConnected, CancellationToken cancellationToken)
        {
            var worker = await EnsureWorkerAsync(gpuIndex, cancellationToken).ConfigureAwait(false);

            var request = new RangeProgressReportRequestModel
            {
                ClientId = worker.ClientId,
                RangeId = rangeId,
                ProgressPercent = progressPercent,
                SpeedKeysPerSecond = speedKeysPerSecond,
                CardsConnected = cardsConnected,
                MarkComplete = markComplete
            };

            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, "api/ranges/report")
                {
                    Content = JsonContent.Create(request, options: _serializerOptions)
                };
                message.Headers.Add("X-Client-Token", worker.ClientToken);
                using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    Logger.LogError(null, $"Backend report error ({response.StatusCode}): {error}");
                    return;
                }

                var reportResponse = await response.Content.ReadFromJsonAsync<RangeReportResponseModel>(_serializerOptions, cancellationToken).ConfigureAwait(false);
                if (reportResponse?.NextRange is not null)
                {
                    worker.CurrentRange = ConvertRange(reportResponse.NextRange);
                }
                else if (reportResponse?.CurrentRange is not null)
                {
                    var updatedRange = ConvertRange(reportResponse.CurrentRange);
                    if (updatedRange is not null)
                    {
                        updatedRange.ProgressPercent = progressPercent;
                        worker.CurrentRange = markComplete ? null : updatedRange;
                    }
                }
                else if (markComplete)
                {
                    worker.CurrentRange = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send progress to backend.");
            }
        }

        public async Task ReportHeartbeatAsync(int gpuIndex, CancellationToken cancellationToken)
        {
            var worker = await EnsureWorkerAsync(gpuIndex, cancellationToken).ConfigureAwait(false);
            if (worker.CurrentRange is null)
                return;

            await ReportProgressAsync(
                gpuIndex,
                worker.CurrentRange.RangeId,
                worker.CurrentRange.ProgressPercent,
                markComplete: false,
                speedKeysPerSecond: null,
                cardsConnected: 1,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task SubmitKeyFoundAsync(int gpuIndex, Guid rangeId, string puzzleCode, string privateKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(privateKey))
                return;

            var worker = await EnsureWorkerAsync(gpuIndex, cancellationToken).ConfigureAwait(false);

            var request = new KeyFoundRequestModel
            {
                ClientId = worker.ClientId,
                RangeId = rangeId,
                PrivateKey = privateKey.Trim(),
                Puzzle = string.IsNullOrWhiteSpace(puzzleCode) ? _settings.TargetPuzzle ?? "71" : puzzleCode
            };

            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, "api/events/key-found")
                {
                    Content = JsonContent.Create(request, options: _serializerOptions)
                };
                message.Headers.Add("X-Client-Token", worker.ClientToken);

                using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    Logger.LogError(null, $"Backend key report error ({response.StatusCode}): {error}");
                }
                else
                {
                    Logger.LogInformation($"Key reported to backend for worker {BuildWorkerName(gpuIndex)}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to notify backend about found key.");
            }
        }

        private async Task<WorkerState> EnsureWorkerAsync(int gpuIndex, CancellationToken cancellationToken)
        {
            if (_workers.TryGetValue(gpuIndex, out var cached))
                return cached;

            await _registrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_workers.TryGetValue(gpuIndex, out cached))
                    return cached;

                var state = new WorkerState
                {
                    RegisteredAtUtc = DateTime.UtcNow
                };

                var configuredId = _settings.GetBackendClientId(gpuIndex);
                var configuredToken = _settings.GetBackendClientToken(gpuIndex);

                if (!string.IsNullOrWhiteSpace(configuredId) &&
                    Guid.TryParse(configuredId, out var parsedId) &&
                    !string.IsNullOrWhiteSpace(configuredToken))
                {
                    state.ClientId = parsedId;
                    state.ClientToken = configuredToken!;
                }
                else
                {
                    var registration = await RegisterWorkerAsync(gpuIndex, cancellationToken).ConfigureAwait(false);
                    state.ClientId = registration.ClientId;
                    state.ClientToken = registration.ClientToken;
                    state.CurrentRange = registration.AssignedRange;
                }

                _workers[gpuIndex] = state;
                return state;
            }
            finally
            {
                _registrationLock.Release();
            }
        }

        private async Task<RegistrationResult> RegisterWorkerAsync(int gpuIndex, CancellationToken cancellationToken)
        {
            var payload = new RegisterClientRequestModel
            {
                User = string.IsNullOrWhiteSpace(_settings.BackendUser)
                    ? _settings.WorkerName
                    : _settings.BackendUser!,
                WorkerName = BuildWorkerName(gpuIndex),
                Puzzle = _settings.TargetPuzzle ?? "71",
                ApplicationType = _settings.AppType.ToString().ToLowerInvariant(),
                CardsConnected = 1,
                GpuInfo = null,
                ClientVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/clients/register", payload, _serializerOptions, cancellationToken).ConfigureAwait(false);
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogError(null, $"Backend registration failed ({response.StatusCode}): {content}");
                    throw new InvalidOperationException($"Registration failed: {response.StatusCode}");
                }

                var registerResponse = JsonSerializer.Deserialize<RegisterClientResponseModel>(content, _serializerOptions)
                    ?? throw new InvalidOperationException("Cannot parse backend registration response.");

                Logger.LogInformation($"Registered backend worker [{payload.WorkerName}] with id {registerResponse.ClientId}");
                var credentialMessage = $"Backend credentials for {payload.WorkerName}: client_id={registerResponse.ClientId} | client_token={registerResponse.ClientToken}";
                Logger.LogInformation(credentialMessage);
                Helper.WriteLine(credentialMessage, MessageType.success, gpuIndex: gpuIndex);

                return new RegistrationResult
                {
                    ClientId = registerResponse.ClientId,
                    ClientToken = registerResponse.ClientToken,
                    AssignedRange = ConvertRange(registerResponse.AssignedRange)
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to register worker with backend.");
                throw;
            }
        }

        private async Task<BackendRangeAssignment?> RequestRangeAsync(WorkerState worker, int gpuIndex, CancellationToken cancellationToken)
        {
            var request = new RangeClaimRequestModel
            {
                ClientId = worker.ClientId
            };

            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, "api/ranges/claim")
                {
                    Content = JsonContent.Create(request, options: _serializerOptions)
                };
                message.Headers.Add("X-Client-Token", worker.ClientToken);

                using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Helper.WriteLine($"No ranges available for worker {BuildWorkerName(gpuIndex)}.", MessageType.info, gpuIndex: gpuIndex);
                    return null;
                }

                response.EnsureSuccessStatusCode();
                var descriptor = await response.Content.ReadFromJsonAsync<RangeDescriptorModel>(_serializerOptions, cancellationToken).ConfigureAwait(false);
                var range = ConvertRange(descriptor);
                if (range is not null)
                    Logger.LogInformation($"Worker {BuildWorkerName(gpuIndex)} claimed range {range.PrefixStart}-{range.PrefixEnd}");
                return range;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Failed to claim range for worker {BuildWorkerName(gpuIndex)}.");
                return null;
            }
        }

        private string BuildWorkerName(int gpuIndex)
        {
            string baseName = string.IsNullOrWhiteSpace(_settings.WorkerName)
                ? $"worker{gpuIndex}"
                : _settings.WorkerName!;

            bool needsSuffix = (_settings.AppType == AppType.bitcrack && _settings.GPUCount > 1)
                || (_settings.AppType == AppType.vanitysearch && _settings.GPUSeperatedRange);

            return needsSuffix ? $"{baseName}_{gpuIndex}" : baseName;
        }

        private static BackendRangeAssignment? ConvertRange(RangeDescriptorModel? descriptor)
        {
            if (descriptor is null)
                return null;

            return new BackendRangeAssignment
            {
                RangeId = descriptor.RangeId,
                Puzzle = descriptor.Puzzle ?? string.Empty,
                PrefixStart = descriptor.PrefixStart ?? string.Empty,
                PrefixEnd = descriptor.PrefixEnd ?? string.Empty,
                RangeStart = descriptor.RangeStart ?? descriptor.PrefixStart ?? string.Empty,
                RangeEnd = descriptor.RangeEnd ?? descriptor.PrefixEnd ?? string.Empty,
                ChunkSize = descriptor.ChunkSize,
                TargetAddress = descriptor.TargetAddress ?? string.Empty,
                WorkloadStartSuffix = descriptor.WorkloadStartSuffix ?? string.Empty,
                WorkloadEndSuffix = descriptor.WorkloadEndSuffix ?? string.Empty,
                ProgressPercent = 0
            };
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _registrationLock.Dispose();
        }

        private sealed class RegisterClientRequestModel
        {
            public string User { get; set; } = string.Empty;
            public string WorkerName { get; set; } = string.Empty;
            public string Puzzle { get; set; } = string.Empty;
            public string ApplicationType { get; set; } = string.Empty;
            public int CardsConnected { get; set; }
            public string? GpuInfo { get; set; }
            public string? ClientVersion { get; set; }
        }

        private sealed class RegisterClientResponseModel
        {
            public Guid ClientId { get; set; }
            public string ClientToken { get; set; } = string.Empty;
            public RangeDescriptorModel? AssignedRange { get; set; }
        }

        private sealed class RangeDescriptorModel
        {
            public Guid RangeId { get; set; }
            public string? Puzzle { get; set; }
            public string? PrefixStart { get; set; }
            public string? PrefixEnd { get; set; }
            public string? RangeStart { get; set; }
            public string? RangeEnd { get; set; }
            public int ChunkSize { get; set; }
            public string? TargetAddress { get; set; }
            public string? WorkloadStartSuffix { get; set; }
            public string? WorkloadEndSuffix { get; set; }
        }

        private sealed class RangeClaimRequestModel
        {
            public Guid ClientId { get; set; }
        }

        private sealed class RangeProgressReportRequestModel
        {
            public Guid ClientId { get; set; }
            public Guid RangeId { get; set; }
            public double ProgressPercent { get; set; }
            public double? SpeedKeysPerSecond { get; set; }
            public int CardsConnected { get; set; }
            public bool MarkComplete { get; set; }
        }

        private sealed class RangeReportResponseModel
        {
            public RangeDescriptorModel? CurrentRange { get; set; }
            public RangeDescriptorModel? NextRange { get; set; }
            public bool HasMoreWork { get; set; }
        }

        private sealed class RegistrationResult
        {
            public Guid ClientId { get; init; }
            public string ClientToken { get; init; } = string.Empty;
            public BackendRangeAssignment? AssignedRange { get; init; }
        }

        private sealed class KeyFoundRequestModel
        {
            public Guid ClientId { get; set; }
            public Guid RangeId { get; set; }
            public string PrivateKey { get; set; } = string.Empty;
            public string? Puzzle { get; set; }
        }
    }
}
