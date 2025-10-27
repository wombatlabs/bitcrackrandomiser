namespace BitcrackPoolBackend.Dtos
{
    public class KeyFindEventDto
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public string Puzzle { get; set; } = string.Empty;
        public string WorkerName { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public DateTime ReportedAtUtc { get; set; }
    }
}
