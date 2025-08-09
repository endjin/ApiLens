using ApiLens.Core.Models;

namespace ApiLens.Core.Services;

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