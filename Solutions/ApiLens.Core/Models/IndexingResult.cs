namespace ApiLens.Core.Models;

public record IndexingResult
{
    public required int TotalDocuments { get; init; }
    public required int SuccessfulDocuments { get; init; }
    public required int FailedDocuments { get; init; }
    public required TimeSpan ElapsedTime { get; init; }
    public required long BytesProcessed { get; init; }
    public required PerformanceMetrics Metrics { get; init; }
    public ImmutableArray<string> Errors { get; init; } = [];

    public double DocumentsPerSecond => ElapsedTime.TotalSeconds > 0
        ? SuccessfulDocuments / ElapsedTime.TotalSeconds
        : 0;

    public double MegabytesPerSecond => ElapsedTime.TotalSeconds > 0
        ? (BytesProcessed / 1024.0 / 1024.0) / ElapsedTime.TotalSeconds
        : 0;
}