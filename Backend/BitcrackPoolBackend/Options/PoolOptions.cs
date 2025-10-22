namespace BitcrackPoolBackend.Options
{
    public class PoolOptions
    {
        public string Puzzle { get; set; } = "71";
        public string RangeStartHex { get; set; } = "8000000";
        public string RangeEndHex { get; set; } = "8000FFF";
        public int RangeChunkSize { get; set; } = 4;
        public string WorkloadStartSuffix { get; set; } = "000000000";
        public string WorkloadEndSuffix { get; set; } = "FFFFFFFFF";
        public TimeSpan WorkerOfflineAfter { get; set; } = TimeSpan.FromMinutes(2);
        public string? TelegramBotToken { get; set; }
        public string? TelegramChatId { get; set; }
        public string? AdminApiKey { get; set; }
        public List<SeedPuzzleOptions> SeedPuzzles { get; set; } = new();
    }

    public class SeedPuzzleOptions
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string TargetAddress { get; set; } = string.Empty;
        public string MinPrefixHex { get; set; } = string.Empty;
        public string MaxPrefixHex { get; set; } = string.Empty;
        public int PrefixLength { get; set; }
        public int ChunkSize { get; set; } = 4;
        public string WorkloadStartSuffix { get; set; } = string.Empty;
        public string WorkloadEndSuffix { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool Randomized { get; set; } = true;
        public double Weight { get; set; } = 1.0;
        public string? Notes { get; set; }
    }
}
