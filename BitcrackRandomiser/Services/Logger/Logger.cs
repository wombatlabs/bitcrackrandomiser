using Serilog;

namespace BitcrackRandomiser.Services
{
    /// <summary>
    /// Logger class
    /// </summary>
    internal static class Logger
    {
        public static Serilog.Core.Logger? _logger = null;

        static Logger()
        {
            if (_logger == null)
            {
                var fileName = DateTime.Now.ToString("dd-MM-yyyy");
                var baseFile = $"logs\\logs_{fileName}.txt";
                var fileTemplate = $"logs\\logs_{fileName}_{{WorkerName}}.txt";

                _logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Map(
                        keySelector: "WorkerName",
                        defaultKey: "default",
                        configure: (workerName, lc) => lc.File(
                            (workerName == "default" || string.IsNullOrWhiteSpace(workerName)) ? baseFile : fileTemplate.Replace("{WorkerName}", workerName)
                        ))
                .CreateLogger();
                _logger.Information("Logger created/updated");
            }
        }

        /// <summary>
        /// Log error
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="message"></param>
        public static void LogError(Exception? ex, string? message)
        {
            _logger?.Error(ex, message);
        }

        /// <summary>
        /// Log information
        /// </summary>
        /// <param name="message"></param>
        public static void LogInformation(string? message)
        {
            _logger?.Information(message);
        }
    }
}
