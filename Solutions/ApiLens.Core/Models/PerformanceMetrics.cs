namespace ApiLens.Core.Models;

public record PerformanceMetrics
{
    public required long TotalAllocatedBytes { get; init; }
    public required int Gen0Collections { get; init; }
    public required int Gen1Collections { get; init; }
    public required int Gen2Collections { get; init; }
    public required double AverageParseTimeMs { get; init; }
    public required double AverageIndexTimeMs { get; init; }
    public required double AverageBatchCommitTimeMs { get; init; }
    public required int PeakThreadCount { get; init; }
    public required double CpuUsagePercent { get; init; }
    public required long PeakWorkingSetBytes { get; init; }
    public required int DocumentsPooled { get; init; }
    public required int StringsInterned { get; init; }
}