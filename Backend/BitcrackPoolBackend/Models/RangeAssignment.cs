using BitcrackPoolBackend.Enums;

namespace BitcrackPoolBackend.Models
{
    public class RangeAssignment
    {
        public Guid Id { get; set; }
        public string Puzzle { get; set; } = string.Empty;
        public Guid PuzzleId { get; set; }
        public PuzzleDefinition? PuzzleDefinition { get; set; }
        public string PrefixStart { get; set; } = string.Empty;
        public string PrefixEnd { get; set; } = string.Empty;
        public string RangeStartHex { get; set; } = string.Empty;
        public string RangeEndHex { get; set; } = string.Empty;
        public int ChunkSize { get; set; }
        public RangeStatus Status { get; set; } = RangeStatus.Pending;
        public Guid? AssignedToClientId { get; set; }
        public Client? AssignedToClient { get; set; }
        public DateTime? AssignedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? LastUpdateUtc { get; set; }
        public double ProgressPercent { get; set; }
        public double ReportedSpeedKeysPerSecond { get; set; }
    }
}
