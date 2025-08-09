namespace ApiLens.Core.Services;

/// <summary>
/// Statistics from the deduplication process.
/// </summary>
public record DeduplicationStats
{
    public required int TotalScannedPackages { get; init; }
    public required int UniqueXmlFiles { get; init; }
    public required int EmptyXmlFilesSkipped { get; init; }
    public required int AlreadyIndexedSkipped { get; init; }
    public required int NewPackages { get; init; }
    public required int UpdatedPackages { get; init; }
}