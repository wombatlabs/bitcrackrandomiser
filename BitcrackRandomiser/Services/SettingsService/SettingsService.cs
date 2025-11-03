using BitcrackRandomiser.Enums;
using BitcrackRandomiser.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BitcrackRandomiser.Services.SettingsService
{
    internal static class SettingsService
    {
        private const string DefaultBackendUrl = "https://api.btcmultipuzzle.com";
        private static readonly Regex Base58AddressRegex = new("^[13][1-9A-HJ-NP-Za-km-z]{25,34}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Bech32AddressRegex = new("^bc1[ac-hj-np-z0-9]{11,71}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Get settings.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static Setting GetSettings(string[] args)
        {
            var settings = new Setting();
            string path = AppDomain.CurrentDomain.BaseDirectory + "settings.txt";
            string pathExample = AppDomain.CurrentDomain.BaseDirectory + "settings.example.txt";

            // If file not exists copy from example 
            if (!File.Exists(path) && File.Exists(pathExample))
                File.Copy(pathExample, path);

            // From file
            string[] lines = File.ReadLines(path).ToArray();

            // From arguments
            if (args.Length > 0)
                lines = args;

            foreach (var line in lines)
            {
                if (line.Contains('='))
                {
                    string key = line.Split('=')[0];
                    string value = line.Split("=")[1];

                    switch (key)
                    {
                        case "target_puzzle":
                            settings.TargetPuzzle = value;
                            break;
                        case "app_type":
                            _ = Enum.TryParse(value, true, out AppType _at);
                            settings.AppType = _at;
                            break;
                        case "app_path":
                            if (value == "cuBitcrack" || value == "clBitcrack")
                            {
                                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                                    value = string.Format("{0}Apps\\bitcrack\\{1}.exe", AppDomain.CurrentDomain.BaseDirectory, value);
                                else
                                    value = string.Format("{0}Apps/bitcrack/./{1}", AppDomain.CurrentDomain.BaseDirectory, value);
                            }
                            else if (value == "vanitysearch")
                            {
                                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                                    value = string.Format("{0}Apps\\vanitysearch\\{1}.exe", AppDomain.CurrentDomain.BaseDirectory, value);
                                else
                                    value = string.Format("{0}Apps/vanitysearch/./{1}", AppDomain.CurrentDomain.BaseDirectory, value);
                            }
                            settings.AppPath = value;
                            break;
                        case "app_arguments":
                            settings.AppArgs = value;
                            break;
                        case "user_token":
                            settings.UserToken = value;
                            break;
                        case "btc_address":
                            settings.BitcoinAddress = value;
                            break;
                        case "worker_name":
                            settings.WorkerName = value;
                            break;
                        case "gpu_count":
                            _ = int.TryParse(value, out int _g);
                            settings.GPUCount = _g;
                            break;
                        case "gpu_index":
                            _ = int.TryParse(value, out int _gi);
                            settings.GPUIndex = _gi;
                            break;
                        case "gpu_seperated_range":
                            _ = bool.TryParse(value, out bool _gsr);
                            settings.GPUSeperatedRange = _gsr;
                            break;
                        case "scan_rewards":
                            _ = bool.TryParse(value, out bool _r);
                            settings.ScanRewards = _r;
                            break;
                        case "custom_range":
                            settings.CustomRange = value;
                            break;
                        case "api_share":
                            settings.ApiShare = value;
                            break;
                        case "telegram_share":
                            _ = bool.TryParse(value, out bool _v);
                            settings.TelegramShare = _v;
                            break;
                        case "telegram_accesstoken":
                            settings.TelegramAccessToken = value;
                            break;
                        case "telegram_chatid":
                            settings.TelegramChatId = value;
                            break;
                        case "telegram_share_eachkey":
                            _ = bool.TryParse(value, out bool _s);
                            settings.TelegramShareEachKey = _s;
                            break;
                        case "untrusted_computer":
                            _ = bool.TryParse(value, out bool _u);
                            settings.UntrustedComputer = _u;
                            break;
                        case "test_mode":
                            _ = bool.TryParse(value, out bool _t);
                            settings.TestMode = _t;
                            break;
                        case "force_continue":
                            _ = bool.TryParse(value, out bool _f);
                            settings.ForceContinue = _f;
                            break;
                        case "cloud_search_mode":
                            _ = bool.TryParse(value, out bool _cs);
                            settings.CloudSearchMode = _cs;
                            break;
                        case "backend_enabled":
                            _ = bool.TryParse(value, out bool _be);
                            settings.BackendEnabled = _be;
                            break;
                        case "backend_base_url":
                            settings.BackendBaseUrl = value;
                            break;
                        case "backend_user":
                            settings.BackendUser = value;
                            break;
                        case "backend_client_ids":
                            settings.BackendClientIdsRaw = value;
                            break;
                        case "backend_client_tokens":
                            settings.BackendClientTokensRaw = value;
                            break;
                        case "backend_target_address":
                            settings.BackendTargetAddress = value;
                            break;
                    }
                }
            }
            return settings;
        }

        /// <summary>
        /// Apply defaults needed to connect to the pool and, when interactive, collect the miner identity.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static Setting ConfigureForPool(Setting settings)
        {
            if (settings is null)
                throw new ArgumentNullException(nameof(settings));

            var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Ensure backend integration stays enabled with sensible defaults.
            if (!settings.BackendEnabled)
            {
                settings.BackendEnabled = true;
                changes["backend_enabled"] = "true";
            }

            if (string.IsNullOrWhiteSpace(settings.BackendBaseUrl))
            {
                settings.BackendBaseUrl = DefaultBackendUrl;
                changes["backend_base_url"] = settings.BackendBaseUrl;
            }

            if (string.IsNullOrWhiteSpace(settings.TargetPuzzle))
            {
                settings.TargetPuzzle = "71";
                changes["target_puzzle"] = settings.TargetPuzzle;
            }

            if (settings.GPUCount < 1)
            {
                settings.GPUCount = 1;
                changes["gpu_count"] = settings.GPUCount.ToString();
            }

            if (!Console.IsInputRedirected)
            {
                Helper.WriteLine("Quick setup: press enter to keep the current value.", MessageType.info, true);

                string workerName = PromptWorkerName(settings.WorkerName);
                bool workerChanged = !string.Equals(workerName, settings.WorkerName, StringComparison.Ordinal);
                settings.WorkerName = workerName;
                if (workerChanged)
                    changes["worker_name"] = workerName;

                string minerName = PromptMinerName(settings.BackendUser, workerName);
                bool minerChanged = !string.Equals(minerName, settings.BackendUser ?? string.Empty, StringComparison.Ordinal);
                settings.BackendUser = minerName;
                if (minerChanged)
                    changes["backend_user"] = minerName;

                string btcAddress = PromptBtcAddress(settings.BitcoinAddress);
                bool btcChanged = !string.Equals(btcAddress, settings.BitcoinAddress ?? string.Empty, StringComparison.Ordinal);
                settings.BitcoinAddress = btcAddress;
                if (btcChanged)
                    changes["btc_address"] = btcAddress;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(settings.BackendUser))
                {
                    settings.BackendUser = settings.WorkerName;
                    changes["backend_user"] = settings.BackendUser;
                }

                if (string.IsNullOrWhiteSpace(settings.BitcoinAddress))
                {
                    Helper.WriteLine("No payout BTC address configured. Update settings.txt to include 'btc_address=<your address>'.", MessageType.error);
                }
            }

            if (string.IsNullOrWhiteSpace(settings.BackendUser))
            {
                settings.BackendUser = settings.WorkerName;
                changes["backend_user"] = settings.BackendUser;
            }

            PersistSettingsChanges(changes);
            return settings;
        }

        /// <summary>
        /// Create settings
        /// </summary>
        /// <returns></returns>
        public static Setting SetSettings()
        {
            Console.Clear();
            var consoleSettings = new Setting();

            // Select puzzle
            string _Puzzle = DetermineSettings("Select a puzzle number", null, 1);

            // Select app path
            string _Folder = DetermineSettings("Enter app folder path [cuBitcrack, clBitcrack, vanitysearch or full path]", null, 6);

            // App arguments
            var _Arguments = DetermineSettings("Enter app arguments (can be empty)");

            // User token
            string _UserToken = DetermineSettings("Your user token value", null, 20);

            // Wallet address
            string _WalletAddress = DetermineSettings("Enter worker name", null, 20);

            // BTC payout address
            string _BtcAddress = DetermineSettings("Enter payout BTC address", null, 14);

            // GPU Count
            string _gpuCount = DetermineSettings("Enter your GPU count [Min:1, Max:16]", null, 1);

            // GPU Index
            string _GPUIndex = DetermineSettings("Enter your GPU Index [Default:0]", null, 1);

            // GPU seperated ranges
            string _GPUSeperatedRanges = DetermineSettings("Use each GPU as a separate worker? VanitySearch only.", new string[2] { "true", "false" });

            // Scan type
            string _ScanType = DetermineSettings("Select a scan type", new string[2] { "default", "includeDefeatedRanges" });

            // Scan rewards
            string _ScanRewards = DetermineSettings("Scan for rewards of the pool?", new string[2] { "true", "false" });

            // Custom range
            string _CustomRange = DetermineSettings("Do you want scan custom range?", new string[2] { "yes", "no" });
            if (_CustomRange == "yes")
            {
                _CustomRange = DetermineSettings("Enter custom range (2B,3DFF or any)", null, 2);
            }
            else
            {
                _CustomRange = "none";
            }

            // Logging
            string _EnableLogging = DetermineSettings("Enable error logging?", new string[2] { "true", "false" });

            // API share
            string _ApiShare = DetermineSettings("Send API request to URL (can be empty)");

            // Telegram share
            string _TelegramShare = DetermineSettings("Will telegram sharing be enabled?", new string[2] { "true", "false" });

            // If telegram share will be enabled
            string _TelegramAccessToken = "0";
            string _TelegramChatId = "0";
            string _TelegramShareEachKey = "false";
            if (_TelegramShare == "true")
            {
                _TelegramAccessToken = DetermineSettings("Enter Telegram access token", null, 5);
                _TelegramChatId = DetermineSettings("Enter Telegram chat id", null, 5);
                _TelegramShareEachKey = DetermineSettings("Send notification when each key scanned", new string[2] { "true", "false" });
            }

            // Untrusted computer
            string _UntrustedComputer = DetermineSettings("Is this computer untrusted?", new string[2] { "true", "false" });

            // Test mode
            string _TestMode = DetermineSettings("Enable test mode", new string[2] { "true", "false" });

            // Force continue
            string _ForceContinue = DetermineSettings("Enable force continue", new string[2] { "true", "false" });

            // Settings
            consoleSettings.TargetPuzzle = _Puzzle;
            consoleSettings.AppPath = _Folder;
            consoleSettings.AppArgs = _Arguments;
            consoleSettings.UserToken = _UserToken;
            consoleSettings.WorkerName = _WalletAddress;
            consoleSettings.BitcoinAddress = _BtcAddress;
            consoleSettings.GPUCount = int.Parse(_gpuCount);
            consoleSettings.GPUIndex = int.Parse(_GPUIndex);
            consoleSettings.GPUSeperatedRange = bool.Parse(_GPUSeperatedRanges);
            consoleSettings.ScanRewards = bool.Parse(_ScanRewards);
            consoleSettings.CustomRange = _CustomRange;
            consoleSettings.ApiShare = _ApiShare;
            consoleSettings.TelegramShare = bool.Parse(_TelegramShare);
            consoleSettings.TelegramAccessToken = _TelegramAccessToken;
            consoleSettings.TelegramChatId = _TelegramChatId;
            consoleSettings.TelegramShareEachKey = bool.Parse(_TelegramShareEachKey);
            consoleSettings.UntrustedComputer = bool.Parse(_UntrustedComputer);
            consoleSettings.TestMode = bool.Parse(_TestMode);
            consoleSettings.ForceContinue = bool.Parse(_ForceContinue);

            // Will save settings
            string saveSettings = "";
            while (saveSettings != "yes" && saveSettings != "no")
            {
                Helper.Write("Do you want to save settings to settings.txt? (yes/no) : ", ConsoleColor.Cyan);
                saveSettings = Console.ReadLine() ?? "";
            }

            // Save settings
            if (saveSettings == "yes")
            {
                string savedSettings =
                    "target_puzzle=" + consoleSettings.TargetPuzzle + Environment.NewLine +
                    "app_path=" + consoleSettings.AppPath + Environment.NewLine +
                    "app_arguments=" + consoleSettings.AppArgs + Environment.NewLine +
                    "user_token=" + consoleSettings.UserToken + Environment.NewLine +
                    "btc_address=" + (consoleSettings.BitcoinAddress ?? "") + Environment.NewLine +
                    "worker_name=" + consoleSettings.WorkerName + Environment.NewLine +
                    "gpu_count=" + consoleSettings.GPUCount + Environment.NewLine +
                    "gpu_index=" + consoleSettings.GPUIndex + Environment.NewLine +
                    "gpu_seperated_range=" + consoleSettings.GPUSeperatedRange + Environment.NewLine +
                    "scan_rewards=" + consoleSettings.ScanRewards + Environment.NewLine +
                    "custom_range=" + consoleSettings.CustomRange + Environment.NewLine +
                    "api_share=" + consoleSettings.ApiShare + Environment.NewLine +
                    "telegram_share=" + consoleSettings.TelegramShare + Environment.NewLine +
                    "telegram_accesstoken=" + consoleSettings.TelegramAccessToken + Environment.NewLine +
                    "telegram_chatid=" + consoleSettings.TelegramChatId + Environment.NewLine +
                    "telegram_share_eachkey=" + consoleSettings.TelegramShareEachKey + Environment.NewLine +
                    "untrusted_computer=" + consoleSettings.UntrustedComputer + Environment.NewLine +
                    "test_mode=" + consoleSettings.TestMode + Environment.NewLine +
                    "force_continue=" + consoleSettings.ForceContinue + Environment.NewLine +
                    "cloud_search_mode=" + consoleSettings.CloudSearchMode + Environment.NewLine +
                    "backend_enabled=" + consoleSettings.BackendEnabled + Environment.NewLine +
                    "backend_base_url=" + (consoleSettings.BackendBaseUrl ?? "") + Environment.NewLine +
                    "backend_user=" + (consoleSettings.BackendUser ?? "") + Environment.NewLine +
                    "backend_client_ids=" + (consoleSettings.BackendClientIdsRaw ?? "") + Environment.NewLine +
                    "backend_client_tokens=" + (consoleSettings.BackendClientTokensRaw ?? "") + Environment.NewLine +
                    "backend_target_address=" + (consoleSettings.BackendTargetAddress ?? "");
                string appPath = AppDomain.CurrentDomain.BaseDirectory;
                using (StreamWriter outputFile = new StreamWriter(Path.Combine(appPath, "settings.txt")))
                {
                    outputFile.WriteLine(savedSettings);
                }
                Helper.Write("\nSettings saved successfully. App starting ...", ConsoleColor.Green);
                Thread.Sleep(2000);
            }
            else
            {
                Helper.Write("\nApp starting ...", ConsoleColor.Green);
                Thread.Sleep(2000);
            }

            return consoleSettings;
        }

        private static string PromptMinerName(string? currentValue, string fallbackWorkerName)
        {
            string activeValue = string.IsNullOrWhiteSpace(currentValue) ? fallbackWorkerName : currentValue!;

            while (true)
            {
                Helper.Write($"Miner name [{activeValue}]: ", ConsoleColor.Cyan);
                string? input = Console.ReadLine();

                if (input is null)
                    return activeValue;

                string trimmed = input.Trim();
                if (trimmed.Length == 0)
                    return activeValue;

                if (trimmed.Length > 32)
                {
                    Helper.WriteLine("Miner name must be between 1 and 32 characters.", MessageType.error);
                    continue;
                }

                return trimmed;
            }
        }

        private static string PromptWorkerName(string currentValue)
        {
            string activeValue = string.IsNullOrWhiteSpace(currentValue) ? $"worker{new Random().Next(1000, 9999)}" : currentValue;

            while (true)
            {
                Helper.Write($"Worker name [{activeValue}]: ", ConsoleColor.Cyan);
                string? input = Console.ReadLine();

                if (input is null)
                    return activeValue;

                if (string.IsNullOrWhiteSpace(input))
                    return activeValue;

                string candidate = NormalizeWorkerName(input);

                if (candidate.Length == 0)
                {
                    Helper.WriteLine("Worker name must contain letters, numbers, '-' or '_'.", MessageType.error);
                    continue;
                }

                if (candidate.Length > 16)
                {
                    Helper.WriteLine("Worker name must be 1-16 characters.", MessageType.error);
                    continue;
                }

                return candidate;
            }
        }

        private static string PromptBtcAddress(string? currentValue)
        {
            string activeValue = string.IsNullOrWhiteSpace(currentValue) ? string.Empty : currentValue.Trim();

            while (true)
            {
                string label = string.IsNullOrEmpty(activeValue)
                    ? "Payout BTC address"
                    : $"Payout BTC address [{activeValue}]";
                Helper.Write($"{label}: ", ConsoleColor.Cyan);
                string? input = Console.ReadLine();

                if (input is null)
                    return activeValue;

                string trimmed = input.Trim();
                if (trimmed.Length == 0)
                {
                    if (!string.IsNullOrEmpty(activeValue))
                        return activeValue;

                    Helper.WriteLine("BTC address is required.", MessageType.error);
                    continue;
                }

                string candidate = trimmed.StartsWith("bc1", StringComparison.OrdinalIgnoreCase)
                    ? trimmed.ToLowerInvariant()
                    : trimmed;

                if (!IsValidBitcoinAddress(candidate))
                {
                    Helper.WriteLine("Invalid BTC address format.", MessageType.error);
                    continue;
                }

                return candidate;
            }
        }

        private static string NormalizeWorkerName(string value)
        {
            value = value.Trim();
            var filtered = value.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray();
            return new string(filtered);
        }

        private static bool IsValidBitcoinAddress(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (Base58AddressRegex.IsMatch(value))
                return true;

            string lower = value.ToLowerInvariant();
            bool uniformCase = string.Equals(value, lower, StringComparison.Ordinal) ||
                               string.Equals(value, value.ToUpperInvariant(), StringComparison.Ordinal);

            if (!uniformCase)
                return false;

            return Bech32AddressRegex.IsMatch(lower);
        }

        private static void PersistSettingsChanges(Dictionary<string, string> changes)
        {
            if (changes.Count == 0)
                return;

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.txt");
            if (!File.Exists(path))
                return;

            var lines = File.ReadAllLines(path).ToList();

            foreach (var kvp in changes)
            {
                string key = kvp.Key;
                string newLine = $"{key}={kvp.Value}";
                bool updated = false;

                for (int i = 0; i < lines.Count; i++)
                {
                    string trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = newLine;
                        updated = true;
                        break;
                    }
                }

                if (!updated)
                    lines.Add(newLine);
            }

            File.WriteAllLines(path, lines);
            Helper.WriteLine("Settings saved to settings.txt", MessageType.success);
        }

        /// <summary>
        /// Create settings via console.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="values"></param>
        /// <param name="minLength"></param>
        /// <returns></returns>
        private static string DetermineSettings(string message, string[]? values = null, int minLength = 0)
        {
            string value = "";
            string messageFormat = values == null ? string.Format("[Settings] {0} : ", message) : string.Format("[Settings] {0} ({1}) : ", message, string.Join('/', values));

            if (minLength > 0)
            {
                while (value.Length < minLength)
                {
                    Helper.Write(messageFormat);
                    value = Console.ReadLine() ?? "";
                }
            }
            else if (values != null)
            {
                while (!values.Contains(value))
                {
                    Helper.Write(messageFormat);
                    value = Console.ReadLine() ?? "";
                }
            }
            else
            {
                Helper.Write(messageFormat);
                value = Console.ReadLine() ?? "";
            }
            Helper.Write("-------------------------------\n");
            return value;
        }
    }
}
