using ApiLens.Core.Models;

namespace ApiLens.Core.Services;

/// <summary>
/// Service for efficiently deduplicating NuGet packages and determining which need indexing.
/// </summary>
public interface IPackageDeduplicationService
{
    /// <summary>
    /// Processes packages to determine which need indexing, handling deduplication and version management.
    /// </summary>
    /// <param name="scannedPackages">All packages found during scanning.</param>
    /// <param name="existingPackages">Package information from the existing index.</param>
    /// <param name="indexedXmlPaths">Set of XML paths already indexed.</param>
    /// <param name="emptyXmlPaths">Set of XML paths known to be empty.</param>
    /// <param name="latestOnly">Whether to keep only the latest version of each package.</param>
    /// <returns>Deduplication result with packages to index and delete.</returns>
    PackageDeduplicationResult DeduplicatePackages(
        IReadOnlyList<NuGetPackageInfo> scannedPackages,
        IReadOnlyDictionary<string, HashSet<(string Version, string Framework)>> existingPackages,
        IReadOnlySet<string> indexedXmlPaths,
        IReadOnlySet<string> emptyXmlPaths,
        bool latestOnly);
}

/// <summary>
/// Result of package deduplication process.
/// </summary>
public record PackageDeduplicationResult
{
    /// <summary>
    /// Packages that need to be indexed (new or updated).
    /// </summary>
    public required IReadOnlyList<NuGetPackageInfo> PackagesToIndex { get; init; }

    /// <summary>
    /// Package IDs whose old versions should be deleted (when using latest-only mode).
    /// </summary>
    public required IReadOnlySet<string> PackageIdsToDelete { get; init; }

    /// <summary>
    /// Number of packages skipped because they're already up-to-date.
    /// </summary>
    public required int SkippedPackages { get; init; }

    /// <summary>
    /// Statistics about the deduplication process.
    /// </summary>
    public required DeduplicationStats Stats { get; init; }
}

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