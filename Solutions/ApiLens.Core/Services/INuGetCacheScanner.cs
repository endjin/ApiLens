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

    /// <summary>
    /// Asynchronously scans the user's NuGet cache for XML documentation files.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of found NuGet packages with documentation.</returns>
    Task<ImmutableArray<NuGetPackageInfo>> ScanNuGetCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously scans a specific directory for NuGet packages with XML documentation.
    /// </summary>
    /// <param name="cachePath">The path to scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="progress">Optional progress reporting.</param>
    /// <returns>Collection of found NuGet packages with documentation.</returns>
    Task<ImmutableArray<NuGetPackageInfo>> ScanDirectoryAsync(
        string cachePath, 
        CancellationToken cancellationToken = default,
        IProgress<int>? progress = null);
}