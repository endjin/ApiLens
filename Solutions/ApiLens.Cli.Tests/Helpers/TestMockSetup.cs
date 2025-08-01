using ApiLens.Core.Models;
using ApiLens.Core.Services;

namespace ApiLens.Cli.Tests.Helpers;

/// <summary>
/// Helper class to setup test mocks consistently.
/// </summary>
internal static class TestMockSetup
{
    /// <summary>
    /// Sets up the scanner mock to return packages for both sync and async methods.
    /// </summary>
    public static void SetupScannerMock(
        INuGetCacheScanner mockScanner,
        string cachePath,
        ImmutableArray<NuGetPackageInfo> packages)
    {
        // Setup sync methods
        mockScanner.ScanDirectory(cachePath).Returns(packages);
        
        // Setup async methods
        mockScanner.ScanDirectoryAsync(cachePath, Arg.Any<CancellationToken>(), Arg.Any<IProgress<int>?>())
            .Returns(Task.FromResult(packages));
    }

    /// <summary>
    /// Sets up the deduplication service mock.
    /// </summary>
    public static void SetupDeduplicationMock(
        IPackageDeduplicationService mockDeduplicationService,
        IReadOnlyList<NuGetPackageInfo> packagesToIndex,
        IReadOnlySet<string> packageIdsToDelete,
        int skippedPackages,
        DeduplicationStats stats)
    {
        PackageDeduplicationResult result = new()
        {
            PackagesToIndex = packagesToIndex,
            PackageIdsToDelete = packageIdsToDelete,
            SkippedPackages = skippedPackages,
            Stats = stats
        };

        mockDeduplicationService.DeduplicatePackages(
            Arg.Any<IReadOnlyList<NuGetPackageInfo>>(),
            Arg.Any<IReadOnlyDictionary<string, HashSet<(string Version, string Framework)>>>(),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<bool>())
            .Returns(result);
    }
}