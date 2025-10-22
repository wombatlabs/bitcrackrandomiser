namespace BitcrackPoolBackend.Dtos
{
    public class RangeProgressReportRequest
    {
        public Guid ClientId { get; set; }
        public Guid RangeId { get; set; }
        public double ProgressPercent { get; set; }
        public double? SpeedKeysPerSecond { get; set; }
        public int? CardsConnected { get; set; }
        public bool MarkComplete { get; set; }
    }
}

