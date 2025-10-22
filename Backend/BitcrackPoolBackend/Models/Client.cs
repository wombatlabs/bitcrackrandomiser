using BitcrackPoolBackend.Enums;

namespace BitcrackPoolBackend.Models
{
    public class Client
    {
        public Guid Id { get; set; }
        public string User { get; set; } = string.Empty;
        public string? WorkerName { get; set; }
        public string Puzzle { get; set; } = string.Empty;
        public ClientApplicationType ApplicationType { get; set; } = ClientApplicationType.Unknown;
        public int CardsConnected { get; set; }
        public string? GpuInfo { get; set; }
        public string? ClientVersion { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public ClientStatus Status { get; set; } = ClientStatus.Idle;
        public double SpeedKeysPerSecond { get; set; }
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
        public Guid? CurrentRangeId { get; set; }
        public RangeAssignment? CurrentRange { get; set; }
        public ICollection<RangeAssignment> RangeAssignments { get; set; } = new List<RangeAssignment>();
    }
}

