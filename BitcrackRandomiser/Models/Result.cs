using BitcrackRandomiser.Enums;

namespace BitcrackRandomiser.Models
{
    /// <summary>
    /// Result of external app with output.
    /// </summary>
    internal class Result
    {
        /// <summary>
        /// Output type
        /// </summary>
        public OutputType OutputType { get; set; }

        /// <summary>
        /// Content. May be private key or another
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Parsed speed in keys per second (if available)
        /// </summary>
        public double? SpeedKeysPerSecond { get; set; }

        /// <summary>
        /// Parsed progress percentage (0-100)
        /// </summary>
        public double? ProgressPercent { get; set; }
    }
}
