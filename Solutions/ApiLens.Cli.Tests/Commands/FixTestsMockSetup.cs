using ApiLens.Core.Models;
using ApiLens.Core.Services;

namespace ApiLens.Cli.Tests.Commands;

/// <summary>
/// Extension methods to fix test mock setups.
/// </summary>
internal static class FixTestsMockSetup
{
    /// <summary>
    /// Sets up scanner mock with both sync and async methods.
    /// </summary>
    public static void SetupScannerWithPackages(
        this INuGetCacheScanner mockScanner,
        string cachePath,
        NuGetPackageInfo[] packages)
    {
        ImmutableArray<NuGetPackageInfo> packagesArray = [..packages];
        
        // Setup sync method
        mockScanner.ScanDirectory(cachePath).Returns(packagesArray);
        
        // Setup async method
        mockScanner.ScanDirectoryAsync(cachePath, Arg.Any<CancellationToken>(), Arg.Any<IProgress<int>?>())
            .Returns(Task.FromResult(packagesArray));
    }

    /// <summary>
    /// Sets up deduplication service to pass all packages through.
    /// </summary>
    public static void SetupPassThroughDeduplication(
        this IPackageDeduplicationService mockDeduplicationService,
        NuGetPackageInfo[] packages)
    {
        PackageDeduplicationResult result = new PackageDeduplicationResult
        {
            PackagesToIndex = packages,
            PackageIdsToDelete = new HashSet<string>(),
            SkippedPackages = 0,
            Stats = new DeduplicationStats
            {
                TotalScannedPackages = packages.Length,
                UniqueXmlFiles = packages.Select(p => p.XmlDocumentationPath).Distinct().Count(),
                EmptyXmlFilesSkipped = 0,
                AlreadyIndexedSkipped = 0,
                NewPackages = packages.Length,
                UpdatedPackages = 0
            }
        };

        mockDeduplicationService.DeduplicatePackages(
            Arg.Any<IReadOnlyList<NuGetPackageInfo>>(),
            Arg.Any<IReadOnlyDictionary<string, HashSet<(string Version, string Framework)>>>(),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<bool>())
            .Returns(result);
    }

    /// <summary>
    /// Sets up deduplication service with custom result.
    /// </summary>
    public static void SetupDeduplicationWithResult(
        this IPackageDeduplicationService mockDeduplicationService,
        PackageDeduplicationResult result)
    {
        mockDeduplicationService.DeduplicatePackages(
            Arg.Any<IReadOnlyList<NuGetPackageInfo>>(),
            Arg.Any<IReadOnlyDictionary<string, HashSet<(string Version, string Framework)>>>(),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<bool>())
            .Returns(result);
    }
}