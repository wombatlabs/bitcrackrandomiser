namespace BitcrackPoolBackend.Models
{
    public class PuzzleDefinition
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool Randomized { get; set; } = true;
        public double Weight { get; set; } = 1.0;

        public string TargetAddress { get; set; } = string.Empty;

        public string MinPrefixHex { get; set; } = string.Empty;
        public string MaxPrefixHex { get; set; } = string.Empty;
        public int PrefixLength { get; set; }

        public int ChunkSize { get; set; } = 4;
        public string WorkloadStartSuffix { get; set; } = "000000000000000000";
        public string WorkloadEndSuffix { get; set; } = "FFFFFFFFFFFFFFFFFF";

        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<RangeAssignment> RangeAssignments { get; set; } = new List<RangeAssignment>();
    }
}

