namespace BitcrackPoolBackend.Dtos
{
    public class WorkerStatDto
    {
        public Guid ClientId { get; set; }
        public string User { get; set; } = string.Empty;
        public string? WorkerName { get; set; }
        public string ApplicationType { get; set; } = string.Empty;
        public string PuzzleCode { get; set; } = string.Empty;
        public int CardsConnected { get; set; }
        public double SpeedKeysPerSecond { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public string? CurrentRange { get; set; }
        public double? CurrentRangeProgress { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
