namespace BitcrackPoolBackend.Dtos
{
    public class KeyFoundRequest
    {
        public Guid ClientId { get; set; }
        public Guid? RangeId { get; set; }
        public string PrivateKey { get; set; } = string.Empty;
        public string? Puzzle { get; set; }
    }
}

