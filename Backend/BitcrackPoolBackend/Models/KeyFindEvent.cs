namespace BitcrackPoolBackend.Models
{
    public class KeyFindEvent
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Guid? RangeId { get; set; }
        public string Puzzle { get; set; } = string.Empty;
        public string WorkerName { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public DateTime ReportedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

