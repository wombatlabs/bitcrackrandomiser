namespace BitcrackPoolBackend.Dtos
{
    public class PuzzleOverviewDto
    {
        public Guid PuzzleId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int RangesTotal { get; set; }
        public int RangesCompleted { get; set; }
        public int RangesInProgress { get; set; }
        public double PercentageSearched { get; set; }
        public int KeysFound { get; set; }
    }
}

