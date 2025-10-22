namespace BitcrackPoolBackend.Dtos
{
    public class RegisterClientRequest
    {
        public string User { get; set; } = string.Empty;
        public string? WorkerName { get; set; }
        public string Puzzle { get; set; } = string.Empty;
        public string ApplicationType { get; set; } = string.Empty;
        public int CardsConnected { get; set; }
        public string? GpuInfo { get; set; }
        public string? ClientVersion { get; set; }
    }
}

