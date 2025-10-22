namespace BitcrackPoolBackend.Dtos
{
    public class PuzzleDefinitionDto
    {
        public Guid? Id { get; set; }
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
        public string WorkloadStartSuffix { get; set; } = string.Empty;
        public string WorkloadEndSuffix { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}

