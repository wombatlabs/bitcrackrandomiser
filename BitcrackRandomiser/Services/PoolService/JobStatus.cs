using BitcrackRandomiser.Enums;
using BitcrackRandomiser.Models;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BitcrackRandomiser.Services.PoolService
{
    /// <summary>
    /// BitcrackRandomiser job class.
    /// </summary>
    internal static class JobStatus
    {
        /// <summary>
        /// Get current status of external app (Bitcrack or another)
        /// </summary>
        /// <param name="line">Output line</param>
        /// <param name="gpuIndex">GPU index</param>
        /// <param name="hex">HEX</param>
        /// <returns>[string] output message of external app</returns>
        public static Result GetStatus(string? line, int gpuIndex, string hex, AppType appType)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                var data = line;
                string currentDate = DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss");
                int consoleWidth = 720;
                if (Environment.UserInteractive && !Console.IsInputRedirected)
                    consoleWidth = Console.WindowWidth;

                if (!Program.isCloudSearchMode)
                {
                    Console.CursorVisible = false;
                    Console.SetCursorPosition(0, 9 + gpuIndex);
                }

                if (appType == AppType.bitcrack)
                {
                    /// Bitcrack
                    if (data.Contains("Reached") || data.Contains("No targets remaining"))
                    {
                        string finishedMessage = string.Format("[{0}] [Info] {1}", currentDate, "Scan completed.");
                        if (Program.isCloudSearchMode)
                            Console.WriteLine(finishedMessage + new string(' ', consoleWidth - finishedMessage.Length));
                        else
                            Console.Write(finishedMessage + new string(' ', consoleWidth - finishedMessage.Length));
                        return new Result { OutputType = OutputType.finished };
                    }
                    else if (data.Contains("Address:"))
                    {
                        string address = data.Split(':').Last().Trim();
                        return new Result { OutputType = OutputType.address, Content = address };
                    }
                    else if (data.Contains("Private key:"))
                    {
                        string key = data.Replace(" ", "").ToLower().Trim().Replace("privatekey:", "").Trim().ToUpper();
                        return new Result { OutputType = OutputType.privateKeyFound, Content = key };
                    }
                    else if (data.Contains("Initializing"))
                    {
                        string gpuModel = data[data.IndexOf("Initializing")..].Replace("Initializing", "").Trim();
                        return new Result { OutputType = OutputType.gpuModel, Content = gpuModel };
                    }
                    else
                    {
                        data = data.Trim();
                        var result = new Result { OutputType = OutputType.running };

                        if (TryExtractSpeed(data, out double speed))
                            result.SpeedKeysPerSecond = speed;

                        if (TryExtractProgress(data, out double progress))
                            result.ProgressPercent = progress;

                        if (data.Length > 0)
                        {
                            string message = string.Format("{0} [GPU={1}] [HEX={2}]", data.Length > 1 ? data : "...", gpuIndex, hex);
                            int totalLength = consoleWidth - message.Length;
                            string spaces = totalLength > 0 ? new string(' ', totalLength) : "";
                            if (Program.isCloudSearchMode)
                                Console.WriteLine($"{message}{spaces}");
                            else
                                Console.Write($"{message}{spaces}");
                        }
                        else
                        {
                            string loadingMessage = string.Format("[{0}] [Info] Running ... [GPU={2}] [HEX:{1}]", currentDate, hex, gpuIndex);
                            if(Program.isCloudSearchMode)
                                Console.WriteLine(loadingMessage + new string(' ', consoleWidth - loadingMessage.Length));
                            else
                                Console.Write(loadingMessage + new string(' ', consoleWidth - loadingMessage.Length));
                        }
                        return result;
                    }
                }
                else if (appType == AppType.vanitysearch || appType == AppType.cpu)
                {
                    /// VanitySearch
                    if (data.Contains("[EXIT]"))
                    {
                        string finishedMessage = string.Format("[{0}] [Info] {1}", currentDate, "Scan completed.");
                        if (Program.isCloudSearchMode)
                            Console.WriteLine(finishedMessage + new string(' ', consoleWidth - finishedMessage.Length));
                        else
                            Console.Write(finishedMessage + new string(' ', consoleWidth - finishedMessage.Length));
                        return new Result { OutputType = OutputType.finished };
                    }
                    else if (data.Contains("Public Addr:"))
                    {
                        string address = data.Split(':').Last().Trim();
                        return new Result { OutputType = OutputType.address, Content = address };
                    }
                    else if (data.Contains("Priv (HEX):"))
                    {
                        string key = data.Split(':').Last().Trim().Replace("0x", "").Trim();
                        if (key.Length != 64)
                            key = new string('0', 64 - key.Length) + key;
                        return new Result { OutputType = OutputType.privateKeyFound, Content = key };
                    }
                    else if (data.Contains("GPU:"))
                    {
                        try
                        {
                            string gpuModel = data.Split(':').Last().Trim().Replace("GPU", "");
                            gpuModel = gpuModel.Substring(gpuModel.IndexOf('#') + 2);
                            gpuModel = gpuModel.Remove(gpuModel.IndexOf('(')).Trim();
                            return new Result { OutputType = OutputType.gpuModel, Content = gpuModel };
                        }
                        catch
                        {
                            return new Result { OutputType = OutputType.gpuModel, Content = "-" };
                        }
                    }
                    else
                    {
                        data = data.Trim();
                        var result = new Result { OutputType = OutputType.running };

                        if (TryExtractSpeed(data, out double speed))
                            result.SpeedKeysPerSecond = speed;

                        if (TryExtractProgress(data, out double progress))
                            result.ProgressPercent = progress;

                        if (data.Length > 0)
                        {
                            string message = string.Format("{0} [HEX={2}]", data.Length > 1 ? data : "...", gpuIndex, hex);
                            int totalLength = consoleWidth - message.Length;
                            string spaces = totalLength > 0 ? new string(' ', totalLength) : "";

                            if(Program.isCloudSearchMode)
                                Console.WriteLine($"{message}{spaces}");
                            else
                                Console.Write($"{message}{spaces}");
                        }
                        else
                        {
                            string loadingMessage = string.Format("[{0}] [Info] Running ... [HEX:{1}]", currentDate, hex, gpuIndex);
                            if(Program.isCloudSearchMode)
                                Console.WriteLine(loadingMessage + new string(' ', consoleWidth - loadingMessage.Length));
                            else
                                Console.Write(loadingMessage + new string(' ', consoleWidth - loadingMessage.Length));
                        }
                        return result;
                    }
                }
            }

            return new Result { OutputType = OutputType.unknown };
        }

        private static bool TryExtractSpeed(string data, out double speed)
        {
            speed = 0;
            if (string.IsNullOrWhiteSpace(data))
                return false;

            var match = Regex.Match(data, "([0-9]+(?:\\.[0-9]+)?)\\s*([KMGTP]?)(?:keys|key|K)(?:/|\\s)*/s", RegexOptions.IgnoreCase);
            if (!match.Success)
                match = Regex.Match(data, "([0-9]+(?:\\.[0-9]+)?)\\s*([KMGTP]?)(?:h?ash|key)s?/s", RegexOptions.IgnoreCase);

            if (!match.Success)
                return false;

            if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                return false;

            string unit = match.Groups[2].Value.ToUpperInvariant();
            double multiplier = unit switch
            {
                "K" => 1_000d,
                "M" => 1_000_000d,
                "G" => 1_000_000_000d,
                "T" => 1_000_000_000_000d,
                "P" => 1_000_000_000_000_000d,
                _ => 1d
            };

            speed = value * multiplier;
            return true;
        }

        private static bool TryExtractProgress(string data, out double progress)
        {
            progress = 0;
            if (string.IsNullOrWhiteSpace(data))
                return false;

            var matches = Regex.Matches(data, "([0-9]+(?:\\.[0-9]+)?)%", RegexOptions.IgnoreCase);
            if (matches.Count == 0)
                return false;

            double maxPercent = 0;
            bool parsed = false;
            foreach (Match match in matches)
            {
                if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
                    continue;
                Logger.LogInformation($"Parsed progress segment: {percent}% from '{data}'");
                maxPercent = Math.Max(maxPercent, percent);
                parsed = true;
            }

            if (!parsed)
                return false;

            progress = Math.Clamp(maxPercent, 0, 100);
            return true;
        }
    }
}
