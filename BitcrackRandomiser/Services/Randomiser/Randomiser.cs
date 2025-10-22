using BitcrackRandomiser.Enums;
using BitcrackRandomiser.Models;
using BitcrackRandomiser.Services.PoolService;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;

namespace BitcrackRandomiser.Services.Randomiser
{
    /// <summary>
    /// Main randomiser app functions.
    /// </summary>
    internal static class Randomiser
    {
        // Found private key
        public static string privateKey = "";

        // Is proof of work key
        public static bool[] isProofKeys = new bool[16];

        // Proof of work keys list.
        public static string[] proofKeys = new string[16];

        // Is reward key
        public static bool[] isRewardKeys = new bool[16];

        // Addresses for rewards
        public static string[] rewardAddresses = new string[16];

        // GPU Model names
        public static string[] gpuNames = new string[16];

        // Scan completed
        public static bool[] scanCompleted = new bool[16];

        // Backend pool client instance
        private static BackendPoolClient? backendPoolClient;

        // Backend range assignments per GPU
        private static BackendPoolClient.BackendRangeAssignment?[] backendRanges = new BackendPoolClient.BackendRangeAssignment?[16];

        // Backend runtime telemetry
        private static double[] backendSpeeds = new double[16];
        private static double[] backendProgress = new double[16];
        private static DateTime[] backendLastReportUtc = new DateTime[16];

        // Check if app started
        public static bool appStarted = false;

        /// <summary>
        /// Start scan!
        /// </summary>
        /// <param name="settings">Initial settings</param>
        /// <param name="gpuIndex">GPU index</param>
        /// <returns></returns>
        public static Task<int> Scan(Setting settings, int gpuIndex)
        {
            // Check important area
            if (!settings.TelegramShare && settings.UntrustedComputer && !settings.IsApiShare)
            {
                Helper.WriteLine("If the 'untrusted_computer' setting is 'true', the private key will only be sent to your Telegram address. Please change the 'telegram_share' to 'true' in settings.txt. Then enter your 'access token' and 'chat id'. Otherwise, even if the private key is found, you will not be able to see it anywhere!", MessageType.error, true);
                Thread.Sleep(10000);
            }
            if (settings.ForceContinue && settings.UntrustedComputer && !settings.TelegramShare && !settings.IsApiShare)
            {
                Helper.WriteLine("The settings you enter will never show you the key. The application will be closed. Disable \"force_continue\" setting.", MessageType.error, true);
                return Task.FromResult(0);
            }

            BackendPoolClient.BackendRangeAssignment? backendRange = null;
            bool useBackend = settings.IsBackendConfigured;
            string targetAddress;
            List<string> proofValues;
            string randomHex;
            string workloadStart;
            string workloadEnd;
            string rangeIdentifier;

            if (useBackend)
            {
                backendPoolClient ??= new BackendPoolClient(settings);
                backendRange = backendPoolClient.ClaimRangeAsync(gpuIndex, CancellationToken.None).Result;

                if (backendRange is null)
                {
                    Helper.WriteLine("No backend ranges available. Retrying in 30 seconds...", MessageType.info, true);
                    Thread.Sleep(TimeSpan.FromSeconds(30));
                    return Scan(settings, gpuIndex);
                }

                string backendTarget = !string.IsNullOrWhiteSpace(backendRange.TargetAddress)
                    ? backendRange.TargetAddress.Trim()
                    : settings.BackendTargetAddress?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(backendTarget))
                {
                    Helper.WriteLine("Backend did not provide a target address. Set 'backend_target_address' as a fallback.", MessageType.error, true);
                    return Task.FromResult(0);
                }

                targetAddress = backendTarget;
                proofValues = new List<string>();
                randomHex = backendRange.PrefixStart;
                workloadStart = backendRange.WorkloadStartSuffix;
                workloadEnd = backendRange.WorkloadEndSuffix;
                rangeIdentifier = $"{backendRange.Puzzle}:{backendRange.PrefixStart}-{backendRange.PrefixEnd}";
                backendRanges[gpuIndex] = backendRange;
                backendProgress[gpuIndex] = 0;
                backendSpeeds[gpuIndex] = 0;
                backendLastReportUtc[gpuIndex] = DateTime.MinValue;

                backendPoolClient.ReportProgressAsync(
                    gpuIndex,
                    backendRange.RangeId,
                    progressPercent: 0,
                    markComplete: false,
                    speedKeysPerSecond: null,
                    cardsConnected: GetWorkerGpuCount(settings),
                    cancellationToken: CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                backendLastReportUtc[gpuIndex] = DateTime.UtcNow;

                Helper.WriteLine($"Assigned backend range [Puzzle {backendRange.Puzzle}] {rangeIdentifier}", MessageType.info, gpuIndex: gpuIndex);
            }
            else
            {
                // Get random HEX value from API
                var hexResult = MainService.GetHex(settings, gpuIndex).Result;

                // Cannot get HEX value
                if (!hexResult.isSuccess && hexResult.error is null)
                {
                    Helper.WriteLine("Database connection error. Please wait...", MessageType.error);
                    Thread.Sleep(5000);
                    return Scan(settings, gpuIndex);
                }

                // Check for errors
                if (hexResult.error is not null)
                {
                    Helper.WriteLine(hexResult.error.ReplaceLineEndings(), MessageType.error);
                    return Task.FromResult(0);
                }

                targetAddress = hexResult.data?.TargetAddress!;

                // Parse hex result
                randomHex = hexResult.data.Hex;
                proofValues = hexResult.data.ProofOfWorkAddresses;
                workloadStart = hexResult.data.WorkloadStart;
                workloadEnd = hexResult.data.WorkloadEnd;
                rangeIdentifier = randomHex;
            }

            // Write info
            if (!appStarted)
            {
                Helper.WriteLine(string.Format("[v{1}] [{2}] starting... Puzzle: [{0}]", settings.TestMode ? "TEST" : settings.TargetPuzzle, Assembly.GetEntryAssembly()?.GetName().Version, settings.AppType.ToString()), MessageType.normal, true);
                Helper.WriteLine(string.Format("Target address: {0}", targetAddress));
                if (settings.TestMode) Helper.WriteLine("Test mode is active.", MessageType.error);
                else if (settings.TargetPuzzle == "38") Helper.WriteLine("Test pool 38 is active.", MessageType.error);
                else Helper.WriteLine("Test mode is passive.", MessageType.info);
                Helper.WriteLine(string.Format("Custom range: {0}", $"[{settings.CustomRange}]", MessageType.info));
                Helper.WriteLine(string.Format("API share: {0} / Telegram share: {1}", settings.IsApiShare, settings.TelegramShare), MessageType.info);
                Helper.WriteLine(string.Format("Untrusted computer: {0}", settings.UntrustedComputer), MessageType.info);
                if (useBackend)
                    Helper.WriteLine(string.Format("Progress: {0}", "Using custom backend pool."));
                else
                    Helper.WriteLine(string.Format("Progress: {0}", "Visit the [btcpuzzle.info] for statistics."));
                Helper.WriteLine(string.Format("Worker name: {0}", settings.WorkerName));
                Helper.WriteLine("", MessageType.seperator);

                appStarted = true;
            }

            // App arguments
            string appArguments = "";
            string keyspaceArgument = useBackend
                ? $"{backendRange!.RangeStart}:{backendRange.RangeEnd}"
                : $"{randomHex}{workloadStart}:+{workloadEnd}";

            if (settings.AppType == AppType.bitcrack)
            {
                var proofAddressList = string.Join(' ', proofValues);
                var currentGpuIndex = settings.GPUCount > 1 ? gpuIndex : settings.GPUIndex;
                var baseArguments = $"{settings.AppArgs} --keyspace {keyspaceArgument} {targetAddress}".Trim();
                if (!string.IsNullOrWhiteSpace(proofAddressList))
                    baseArguments = $"{baseArguments} {proofAddressList}";
                appArguments = $"{baseArguments} -d {currentGpuIndex}";
            }
            else if (settings.AppType == AppType.vanitysearch ^ settings.AppType == AppType.cpu)
            {
                var addresses = new List<string>(proofValues)
                {
                    targetAddress
                };
                var fileSaved = Helper.SaveAddressVanity(addresses, gpuIndex);

                if (fileSaved)
                {
                    switch (settings.AppType)
                    {
                        case AppType.vanitysearch:
                            string settedGpus = settings.GPUIndex > 0
                                ? $"-gpuId {settings.GPUIndex}"
                                : settings.GPUSeperatedRange
                                ? $"-gpuId {gpuIndex}"
                                : $"-gpuId {string.Join(",", Enumerable.Range(0, settings.GPUCount).ToArray())}";
                            appArguments = $"{settings.AppArgs} -t 0 -gpu {settedGpus} -i vanitysearch_gpu{gpuIndex}.txt --keyspace {keyspaceArgument}";
                            break;
                        case AppType.cpu:
                            appArguments = $"{settings.AppArgs} -i vanitysearch_gpu{gpuIndex}.txt --keyspace {keyspaceArgument}";
                            break;
                    }
                }
            }

            // Check app is exists
            if (!File.Exists(settings.AppPath))
            {
                Helper.WriteLine($"[{settings.AppType}] cannot find at path ({settings.AppPath}).", MessageType.error);
                return Task.FromResult(0);
            }

            // Tcs
            var taskCompletionSource = new TaskCompletionSource<int>();

            // Proccess info
            var process = new Process
            {
                StartInfo = { FileName = settings.AppPath, RedirectStandardError = true, RedirectStandardOutput = true, Arguments = appArguments },
                EnableRaisingEvents = true
            };

            // Output from BitCrack
            process.ErrorDataReceived += (o, s) => OutputReceivedHandler(o, s, targetAddress, proofValues, rangeIdentifier, settings, process, gpuIndex);
            process.OutputDataReceived += (o, s) => OutputReceivedHandler(o, s, targetAddress, proofValues, rangeIdentifier, settings, process, gpuIndex);

            // App exited
            process.Exited += (sender, args) =>
            {
                int checkTries = 0, maxTries = 20;
                while (!scanCompleted[gpuIndex] && checkTries < maxTries)
                {
                    checkTries++;
                    Thread.Sleep(200);
                }

                if (!scanCompleted[gpuIndex])
                {
                    Logger.LogError(null, $"App [{settings.AppType}] exited with [{process.ExitCode}] code.");
                    Helper.WriteLine($"App [{settings.AppType}] exited with [{process.ExitCode}] code.");
                    ShareService.ShareService.Send(ResultType.workerExited, settings);
                }

                taskCompletionSource.SetResult(process.ExitCode);
                process.Dispose();
            };

            // Start the app
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            return taskCompletionSource.Task;
        }

        /// <summary>
        /// Runs on scan completed or private key found
        /// </summary>
        /// <param name="targetAddress">Target address</param>
        /// <param name="hex">HEX range</param>
        /// <param name="settings">Current settings</param>
        /// <param name="keyFound">Key found or not</param>
        /// <param name="gpuIndex">GPU index</param>
        private static void JobFinished(string targetAddress, string hex, Setting settings, bool keyFound = false, int gpuIndex = 0)
        {
            if (keyFound)
            {
                // Always send notification when key found
                ShareService.ShareService.Send(ResultType.keyFound, settings, privateKey);

                // Not on untrusted computer
                if (!settings.UntrustedComputer && !settings.IsBackendConfigured)
                {
                    Console.WriteLine(Environment.NewLine);
                    Helper.WriteLine(privateKey, MessageType.success);
                    Helper.SaveFile(privateKey, targetAddress);
                }

                if (!settings.IsBackendConfigured)
                {
                    Helper.WriteLine("Congratulations. Key found. Please check your folder.", MessageType.success);
                    Helper.WriteLine("You can donate me; 1eosEvvesKV6C2ka4RDNZhmepm1TLFBtw", MessageType.success);
                }

                if (settings.IsBackendConfigured)
                {
                    var backendRange = backendRanges[gpuIndex];
                    if (backendRange is not null)
                    {
                        backendPoolClient?
                            .ReportProgressAsync(
                                gpuIndex,
                                backendRange.RangeId,
                                progressPercent: 100,
                                markComplete: true,
                                speedKeysPerSecond: backendSpeeds[gpuIndex],
                                cardsConnected: GetWorkerGpuCount(settings),
                                cancellationToken: CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();

                        backendPoolClient?
                            .SubmitKeyFoundAsync(
                                gpuIndex,
                                backendRange.RangeId,
                                backendRange.Puzzle,
                                privateKey,
                                CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();

                        backendRanges[gpuIndex] = null;
                        backendProgress[gpuIndex] = 0;
                        backendSpeeds[gpuIndex] = 0;
                    }
                }
            }
            else
            {
                // Send notification each key scanned
                ShareService.ShareService.Send(ResultType.rangeScanned, settings, hex);

                // Flag HEX as used / report to backend
                if (settings.IsBackendConfigured)
                {
                    var backendRange = backendRanges[gpuIndex];
                    if (backendRange is not null)
                    {
                        backendPoolClient?
                            .ReportProgressAsync(
                                gpuIndex,
                                backendRange.RangeId,
                                progressPercent: 100,
                                markComplete: true,
                                speedKeysPerSecond: backendSpeeds[gpuIndex],
                                cardsConnected: GetWorkerGpuCount(settings),
                                cancellationToken: CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();
                        backendRanges[gpuIndex] = null;
                        backendProgress[gpuIndex] = 0;
                        backendSpeeds[gpuIndex] = 0;
                    }
                }
                else
                {
                    Flagger.Flag(settings, hex, gpuIndex, proofKeys[gpuIndex], gpuNames[gpuIndex]);
                }

                // Wait and restart
                proofKeys[gpuIndex] = "";
                isProofKeys[gpuIndex] = false;
                Thread.Sleep(5000);
                scanCompleted[gpuIndex] = false;
                Scan(settings, gpuIndex);
            }
        }

        /// <summary>
        /// Handler for data received by external app
        /// </summary>
        /// <param name="o"></param>
        /// <param name="e"></param>
        /// <param name="targetAddress">Target address</param>
        /// <param name="proofValues">Proof values</param>
        /// <param name="hex">Selected HEX range</param>
        /// <param name="settings">Current settings</param>
        /// <param name="process">Active proccess</param>
        /// <param name="gpuIndex">GPU Index</param>
        public static void OutputReceivedHandler(object o, DataReceivedEventArgs e, string targetAddress, List<string> proofValues, string hex, Setting settings, Process process, int gpuIndex)
        {
            var status = JobStatus.GetStatus(o, e, gpuIndex, hex, settings.AppType);
            if (status.OutputType == OutputType.finished)
            {
                // Job finished normally and range scanned.
                scanCompleted[gpuIndex] = true;
                JobFinished(targetAddress, hex, settings, keyFound: false, gpuIndex);
            }
            else if (status.OutputType == OutputType.address)
            {
                // An address found. Check it is proof key or target private key
                isProofKeys[gpuIndex] = proofValues.Any(status!.Content!.Contains);
                if (!isProofKeys[gpuIndex])
                {
                    // Check again for known Bitcrack bug - Remove first 10 characters
                    var parsedProofValues = proofValues.Select(x => x[10..]).ToList();
                    isProofKeys[gpuIndex] = parsedProofValues.Any(status.Content.Contains);
                }
            }
            else if (status.OutputType == OutputType.privateKeyFound)
            {
                // A private key found
                if (isProofKeys[gpuIndex])
                {
                    proofKeys[gpuIndex] ??= "";
                    if (!proofKeys[gpuIndex].ToString().Contains(status.Content))
                        proofKeys[gpuIndex] += status.Content;
                }
                else if (isRewardKeys[gpuIndex])
                {
                    // Reward found
                    string rewardResult = $"[Address={rewardAddresses[gpuIndex]}]->[Key={status.Content}]";
                    ShareService.ShareService.Send(ResultType.rewardFound, settings, rewardResult);
                    Helper.SaveFile(rewardResult, $"reward_{rewardAddresses[gpuIndex]}");
                    isRewardKeys[gpuIndex] = false;
                    rewardAddresses[gpuIndex] = "";
                }
                else
                {
                    // Private key found
                    if (settings.ForceContinue == false)
                        process.Kill();
                    privateKey = status!.Content!;
                    JobFinished(targetAddress, hex, settings, keyFound: true, gpuIndex);
                }
            }
            else if (status.OutputType == OutputType.gpuModel)
                gpuNames[gpuIndex] = status!.Content!;

            if (settings.IsBackendConfigured && (status.SpeedKeysPerSecond.HasValue || status.ProgressPercent.HasValue))
            {
                UpdateBackendTelemetry(settings, gpuIndex, status.ProgressPercent, status.SpeedKeysPerSecond);
            }
        }

        public static void DisposeBackend()
        {
            backendPoolClient?.Dispose();
            backendPoolClient = null;
        }

        private static void UpdateBackendTelemetry(Setting settings, int gpuIndex, double? progress, double? speed)
        {
            if (!settings.IsBackendConfigured)
                return;

            var backendRange = backendRanges[gpuIndex];
            if (backendRange is null)
                return;

            bool hasChanges = false;

            if (progress.HasValue)
            {
                backendProgress[gpuIndex] = Math.Clamp(progress.Value, 0, 100);
                backendRange.ProgressPercent = backendProgress[gpuIndex];
                hasChanges = true;
            }

            if (speed.HasValue)
            {
                backendSpeeds[gpuIndex] = Math.Max(0, speed.Value);
                hasChanges = true;
            }

            var now = DateTime.UtcNow;
            if (!hasChanges && (now - backendLastReportUtc[gpuIndex]) < TimeSpan.FromSeconds(15))
                return;

            backendPoolClient?
                .ReportProgressAsync(
                    gpuIndex,
                    backendRange.RangeId,
                    progressPercent: backendProgress[gpuIndex],
                    markComplete: false,
                    speedKeysPerSecond: backendSpeeds[gpuIndex],
                    cardsConnected: GetWorkerGpuCount(settings),
                    cancellationToken: CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            backendLastReportUtc[gpuIndex] = now;

            string summary = $"GPU{gpuIndex} Range {backendRange.PrefixStart}-{backendRange.PrefixEnd} | Progress {backendProgress[gpuIndex]:0.00}% | Speed {FormatSpeed(backendSpeeds[gpuIndex])}";
            Helper.WriteLine(summary, MessageType.externalApp, gpuIndex: gpuIndex);
        }

        private static string FormatSpeed(double value)
        {
            if (value <= 0)
                return "0";

            string[] units = new[] { "keys/s", "K keys/s", "M keys/s", "G keys/s", "T keys/s", "P keys/s" };
            int unitIndex = 0;
            double display = value;
            while (display >= 1000 && unitIndex < units.Length - 1)
            {
                display /= 1000;
                unitIndex++;
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.00} {1}", display, units[unitIndex]);
        }

        private static int GetWorkerGpuCount(Setting settings)
        {
            bool single = (settings.AppType == AppType.bitcrack && settings.GPUCount > 1) ||
                          (settings.AppType == AppType.vanitysearch && settings.GPUSeperatedRange);
            return single ? 1 : Math.Clamp(settings.GPUCount, 1, 32);
        }
    }
}
