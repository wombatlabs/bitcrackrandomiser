namespace BitcrackPoolBackend.Dtos
{
    public class RangeReportResponse
    {
        public RangeDescriptor CurrentRange { get; set; } = new RangeDescriptor();
        public RangeDescriptor? NextRange { get; set; }
        public bool HasMoreWork { get; set; }
    }
}

