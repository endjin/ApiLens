namespace ApiLens.Core.Models;

public record IndexStatistics
{
    public required string IndexPath { get; init; }
    public required long TotalSizeInBytes { get; init; }
    public required int DocumentCount { get; init; }
    public required int FieldCount { get; init; }
    public required int FileCount { get; init; }
    public DateTime? LastModified { get; init; }
}