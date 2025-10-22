namespace BitcrackPoolBackend.Dtos
{
    public class RangeSummaryDto
    {
        public Guid RangeId { get; set; }
        public string PrefixStart { get; set; } = string.Empty;
        public string PrefixEnd { get; set; } = string.Empty;
        public double ProgressPercent { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? PuzzleCode { get; set; }
        public string? AssignedTo { get; set; }
        public DateTime? LastUpdateUtc { get; set; }
    }
}
