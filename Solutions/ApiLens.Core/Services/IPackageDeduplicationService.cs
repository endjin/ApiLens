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