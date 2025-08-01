using ApiLens.Core.Models;
using ApiLens.Core.Services;
using ApiLens.Core.Lucene;

namespace ApiLens.Cli.Tests.Helpers;

/// <summary>
/// Helper for setting up NuGetCommand test scenarios.
/// </summary>
internal static class NuGetCommandTestHelper
{
    /// <summary>
    /// Sets up a complete test scenario with scanner and deduplication mocks.
    /// </summary>
    public static void SetupTestScenario(
        INuGetCacheScanner mockScanner,
        IPackageDeduplicationService mockDeduplicationService,
        string cachePath,
        NuGetPackageInfo[] allPackages,
        NuGetPackageInfo[] packagesToIndex,
        string[]? packageIdsToDelete = null,
        int emptyXmlFilesSkipped = 0)
    {
        // Setup scanner to return packages
        ImmutableArray<NuGetPackageInfo> allPackagesArray = [..allPackages];
        mockScanner.ScanDirectory(cachePath).Returns(allPackagesArray);
        mockScanner.ScanDirectoryAsync(cachePath, Arg.Any<CancellationToken>(), Arg.Any<IProgress<int>?>())
            .Returns(Task.FromResult(allPackagesArray));

        // Setup GetLatestVersions if needed
        if (packagesToIndex.Length < allPackages.Length)
        {
            ImmutableArray<NuGetPackageInfo> latestPackagesArray = [..packagesToIndex];
            mockScanner.GetLatestVersions(Arg.Any<ImmutableArray<NuGetPackageInfo>>())
                .Returns(latestPackagesArray);
        }

        // Setup deduplication service
        PackageDeduplicationResult deduplicationResult = new PackageDeduplicationResult
        {
            PackagesToIndex = packagesToIndex,
            PackageIdsToDelete = new HashSet<string>(packageIdsToDelete ?? []),
            SkippedPackages = allPackages.Length - packagesToIndex.Length,
            Stats = new DeduplicationStats
            {
                TotalScannedPackages = allPackages.Length,
                UniqueXmlFiles = packagesToIndex.Select(p => p.XmlDocumentationPath).Distinct().Count(),
                EmptyXmlFilesSkipped = emptyXmlFilesSkipped,
                AlreadyIndexedSkipped = allPackages.Length - packagesToIndex.Length - emptyXmlFilesSkipped,
                NewPackages = packagesToIndex.Length,
                UpdatedPackages = 0
            }
        };

        mockDeduplicationService.DeduplicatePackages(
            Arg.Any<IReadOnlyList<NuGetPackageInfo>>(),
            Arg.Any<IReadOnlyDictionary<string, HashSet<(string Version, string Framework)>>>(),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<bool>())
            .Returns(deduplicationResult);
    }

    /// <summary>
    /// Sets up a scenario where all packages pass through (no filtering).
    /// </summary>
    public static void SetupPassThroughScenario(
        INuGetCacheScanner mockScanner,
        IPackageDeduplicationService mockDeduplicationService,
        string cachePath,
        NuGetPackageInfo[] packages)
    {
        SetupTestScenario(
            mockScanner,
            mockDeduplicationService,
            cachePath,
            packages,
            packages);
    }

    /// <summary>
    /// Sets up a scenario with empty XML files.
    /// </summary>
    public static void SetupEmptyXmlScenario(
        INuGetCacheScanner mockScanner,
        IPackageDeduplicationService mockDeduplicationService,
        ILuceneIndexManager mockIndexManager,
        string cachePath,
        NuGetPackageInfo[] allPackages,
        NuGetPackageInfo[] nonEmptyPackages,
        string[] emptyXmlPaths)
    {
        SetupTestScenario(
            mockScanner,
            mockDeduplicationService,
            cachePath,
            allPackages,
            nonEmptyPackages,
            emptyXmlFilesSkipped: emptyXmlPaths.Length);

        // Setup index manager to return empty XML paths
        mockIndexManager.GetEmptyXmlPaths()
            .Returns([..emptyXmlPaths]);
        mockIndexManager.GetIndexedXmlPaths()
            .Returns([]);
        mockIndexManager.GetIndexedPackageVersionsWithFramework()
            .Returns(new Dictionary<string, HashSet<(string Version, string Framework)>>());
    }
}