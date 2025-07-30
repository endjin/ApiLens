using ApiLens.Core.Models;

namespace ApiLens.Core.Services;

/// <summary>
/// Service for scanning and processing NuGet package cache.
/// </summary>
public interface INuGetCacheScanner
{
    /// <summary>
    /// Scans the user's NuGet cache for XML documentation files.
    /// </summary>
    /// <returns>Collection of found NuGet packages with documentation.</returns>
    ImmutableArray<NuGetPackageInfo> ScanNuGetCache();

    /// <summary>
    /// Scans a specific directory for NuGet packages with XML documentation.
    /// </summary>
    /// <param name="cachePath">The path to scan.</param>
    /// <returns>Collection of found NuGet packages with documentation.</returns>
    ImmutableArray<NuGetPackageInfo> ScanDirectory(string cachePath);

    /// <summary>
    /// Gets the latest version of each package per target framework.
    /// </summary>
    /// <param name="packages">All scanned packages.</param>
    /// <returns>Filtered collection with only the latest versions.</returns>
    ImmutableArray<NuGetPackageInfo> GetLatestVersions(ImmutableArray<NuGetPackageInfo> packages);
}