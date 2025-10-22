namespace BitcrackPoolBackend.Dtos
{
    public class RangeDescriptor
    {
        public Guid RangeId { get; set; }
        public string Puzzle { get; set; } = string.Empty;
        public string PrefixStart { get; set; } = string.Empty;
        public string PrefixEnd { get; set; } = string.Empty;
        public string RangeStart { get; set; } = string.Empty;
        public string RangeEnd { get; set; } = string.Empty;
        public int ChunkSize { get; set; }
        public string TargetAddress { get; set; } = string.Empty;
        public string WorkloadStartSuffix { get; set; } = string.Empty;
        public string WorkloadEndSuffix { get; set; } = string.Empty;
    }
}
