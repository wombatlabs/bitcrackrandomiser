namespace BitcrackPoolBackend.Models
{
    public class PoolState
    {
        public Guid Id { get; set; }
        public string Puzzle { get; set; } = string.Empty;
        public string NextPrefixHex { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

