namespace BitcrackPoolBackend.Dtos
{
    public class StatsOverviewResponse
    {
        public double TotalSpeedKeysPerSecond { get; set; }
        public int WorkersOnline { get; set; }
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
        public List<PuzzleOverviewDto> Puzzles { get; set; } = new();
        public List<WorkerStatDto> Workers { get; set; } = new();
        public List<RangeSummaryDto> ActiveRanges { get; set; } = new();
    }
}
