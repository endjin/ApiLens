using ApiLens.Cli.Commands;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Services;
using Spectre.Console;
using Spectre.Console.Testing;

namespace ApiLens.Cli.Tests.Commands;

[TestClass]
public class NuGetCommandComprehensiveTests : IDisposable
{
    private IFileSystemService mockFileSystem = null!;
    private INuGetCacheScanner mockScanner = null!;
    private ILuceneIndexManagerFactory mockIndexManagerFactory = null!;
    private ILuceneIndexManager mockIndexManager = null!;
    private NuGetCommand command = null!;
    private TestConsole console = null!;

    [TestInitialize]
    public void Initialize()
    {
        mockFileSystem = Substitute.For<IFileSystemService>();
        mockScanner = Substitute.For<INuGetCacheScanner>();
        mockIndexManagerFactory = Substitute.For<ILuceneIndexManagerFactory>();
        mockIndexManager = Substitute.For<ILuceneIndexManager>();
        mockIndexManagerFactory.Create(Arg.Any<string>()).Returns(mockIndexManager);

        command = new NuGetCommand(mockFileSystem, mockScanner, mockIndexManagerFactory);
        console = new TestConsole();
        AnsiConsole.Console = console;
    }

    #region Change Detection and Skip Logic Tests

    [TestMethod]
    public async Task Execute_WithEmptyXmlFiles_SkipsThemOnSecondRun()
    {
        // Arrange - First run scenario
        string cachePath = "/cache";
        SetupBasicMocks(cachePath);

        // Create packages where some XML files will be empty
        var packages = new List<NuGetPackageInfo>
        {
            // Normal packages with content
            new() { PackageId = "package1", Version = "1.0.0", TargetFramework = "net6.0",
                   XmlDocumentationPath = "/cache/package1/1.0.0/lib/net6.0/Package1.xml" },
            new() { PackageId = "package2", Version = "1.0.0", TargetFramework = "net6.0",
                   XmlDocumentationPath = "/cache/package2/1.0.0/lib/net6.0/Package2.xml" },
            // Empty XML files
            new() { PackageId = "empty1", Version = "1.0.0", TargetFramework = "net6.0",
                   XmlDocumentationPath = "/cache/empty1/1.0.0/lib/net6.0/Empty1.xml" },
            new() { PackageId = "empty2", Version = "1.0.0", TargetFramework = "net6.0",
                   XmlDocumentationPath = "/cache/empty2/1.0.0/lib/net6.0/Empty2.xml" }
        };

        mockScanner.ScanDirectory(cachePath).Returns(packages.ToImmutableArray());
        mockScanner.GetLatestVersions(Arg.Any<ImmutableArray<NuGetPackageInfo>>())
            .Returns(packages.ToImmutableArray());

        // First run - empty index
        SetupEmptyIndex();

        var indexingResult = CreateIndexingResult(successfulDocs: 100, elapsedMs: 50, bytesProcessed: 10000);
        mockIndexManager.IndexXmlFilesAsync(Arg.Any<List<string>>(), Arg.Any<Action<int>?>())
            .Returns(indexingResult);

        var settings = new NuGetCommand.Settings { IndexPath = "./index", LatestOnly = true };

        // Act - First run
        await command.ExecuteAsync(null!, settings);

        // Arrange - Second run with empty files tracked
        var indexedPackages = new Dictionary<string, HashSet<(string, string)>>
        {
            ["package1"] = [("1.0.0", "net6.0")],
            ["package2"] = [("1.0.0", "net6.0")],
            ["empty1"] = [("1.0.0", "net6.0")],
            ["empty2"] = [("1.0.0", "net6.0")]
        };

        var indexedPaths = new HashSet<string>
        {
            "/cache/package1/1.0.0/lib/net6.0/Package1.xml",
            "/cache/package2/1.0.0/lib/net6.0/Package2.xml"
        };

        var emptyPaths = new HashSet<string>
        {
            "/cache/empty1/1.0.0/lib/net6.0/Empty1.xml",
            "/cache/empty2/1.0.0/lib/net6.0/Empty2.xml"
        };

        mockIndexManager.GetIndexedPackageVersionsWithFramework().Returns(indexedPackages);
        mockIndexManager.GetIndexedXmlPaths().Returns(indexedPaths);
        mockIndexManager.GetEmptyXmlPaths().Returns(emptyPaths);

        // Act - Second run
        await command.ExecuteAsync(null!, settings);

        // Assert
        var output = console.Output;
        output.ShouldContain("Skipping 2 known empty XML files");
        output.ShouldContain("All packages are already up-to-date");
    }

    [TestMethod]
    public async Task Execute_WithSharedXmlFiles_CorrectlyDeduplicates()
    {
        // Arrange
        string cachePath = "/cache";
        SetupBasicMocks(cachePath);

        // Multiple frameworks sharing the same XML file
        string sharedXmlPath = "/cache/microsoft.extensions.logging/8.0.0/lib/netstandard2.0/Microsoft.Extensions.Logging.xml";
        var packages = new List<NuGetPackageInfo>
        {
            new() { PackageId = "microsoft.extensions.logging", Version = "8.0.0", TargetFramework = "net6.0",
                   XmlDocumentationPath = sharedXmlPath },
            new() { PackageId = "microsoft.extensions.logging", Version = "8.0.0", TargetFramework = "net7.0",
                   XmlDocumentationPath = sharedXmlPath },
            new() { PackageId = "microsoft.extensions.logging", Version = "8.0.0", TargetFramework = "net8.0",
                   XmlDocumentationPath = sharedXmlPath },
            new() { PackageId = "microsoft.extensions.logging", Version = "8.0.0", TargetFramework = "net9.0",
                   XmlDocumentationPath = sharedXmlPath }
        };

        mockScanner.ScanDirectory(cachePath).Returns(packages.ToImmutableArray());
        mockScanner.GetLatestVersions(Arg.Any<ImmutableArray<NuGetPackageInfo>>())
            .Returns(packages.ToImmutableArray());

        SetupEmptyIndex();

        var indexingResult = CreateIndexingResult(successfulDocs: 500, elapsedMs: 100, bytesProcessed: 50000);
        mockIndexManager.IndexXmlFilesAsync(Arg.Any<List<string>>(), Arg.Any<Action<int>?>())
            .Returns(indexingResult);

        var settings = new NuGetCommand.Settings { IndexPath = "./index", LatestOnly = true };

        // Act
        await command.ExecuteAsync(null!, settings);

        // Assert - Should only index 1 XML file, not 4
        await mockIndexManager.Received(1).IndexXmlFilesAsync(
            Arg.Is<List<string>>(files => files.Count == 1 && files[0] == sharedXmlPath),
            Arg.Any<Action<int>?>());
    }

    [TestMethod]
    public async Task Execute_WithMixedFrameworkScenarios_HandlesCorrectly()
    {
        // Arrange - Complex real-world scenario
        string cachePath = "/cache";
        SetupBasicMocks(cachePath);

        var packages = new List<NuGetPackageInfo>
        {
            // Package with shared XML across frameworks
            new() { PackageId = "shared.package", Version = "1.0.0", TargetFramework = "net6.0",
                   XmlDocumentationPath = "/cache/shared.package/1.0.0/lib/netstandard2.0/Shared.Package.xml" },
            new() { PackageId = "shared.package", Version = "1.0.0", TargetFramework = "net7.0",
                   XmlDocumentationPath = "/cache/shared.package/1.0.0/lib/netstandard2.0/Shared.Package.xml" },
            
            // Package with different XML per framework
            new() { PackageId = "unique.package", Version = "1.0.0", TargetFramework = "net6.0",
                   XmlDocumentationPath = "/cache/unique.package/1.0.0/lib/net6.0/Unique.Package.xml" },
            new() { PackageId = "unique.package", Version = "1.0.0", TargetFramework = "net7.0",
                   XmlDocumentationPath = "/cache/unique.package/1.0.0/lib/net7.0/Unique.Package.xml" },
            
            // Package already in index
            new() { PackageId = "existing.package", Version = "1.0.0", TargetFramework = "net6.0",
                   XmlDocumentationPath = "/cache/existing.package/1.0.0/lib/net6.0/Existing.Package.xml" }
        };

        mockScanner.ScanDirectory(cachePath).Returns(packages.ToImmutableArray());
        mockScanner.GetLatestVersions(Arg.Any<ImmutableArray<NuGetPackageInfo>>())
            .Returns(packages.ToImmutableArray());

        // Existing package already indexed
        var indexedPackages = new Dictionary<string, HashSet<(string, string)>>
        {
            ["existing.package"] = [("1.0.0", "net6.0")]
        };

        var indexedPaths = new HashSet<string>
        {
            "/cache/existing.package/1.0.0/lib/net6.0/Existing.Package.xml"
        };

        mockIndexManager.GetIndexedPackageVersionsWithFramework().Returns(indexedPackages);
        mockIndexManager.GetIndexedXmlPaths().Returns(indexedPaths);
        mockIndexManager.GetEmptyXmlPaths().Returns(new HashSet<string>());

        var indexingResult = CreateIndexingResult(successfulDocs: 300, elapsedMs: 75, bytesProcessed: 30000);
        mockIndexManager.IndexXmlFilesAsync(Arg.Any<List<string>>(), Arg.Any<Action<int>?>())
            .Returns(indexingResult);

        var settings = new NuGetCommand.Settings { IndexPath = "./index", LatestOnly = true };

        // Act
        await command.ExecuteAsync(null!, settings);

        // Assert - Should index 3 files: 1 shared + 2 unique (existing is skipped)
        await mockIndexManager.Received(1).IndexXmlFilesAsync(
            Arg.Is<List<string>>(files =>
                files.Count == 3 &&
                files.Contains("/cache/shared.package/1.0.0/lib/netstandard2.0/Shared.Package.xml") &&
                files.Contains("/cache/unique.package/1.0.0/lib/net6.0/Unique.Package.xml") &&
                files.Contains("/cache/unique.package/1.0.0/lib/net7.0/Unique.Package.xml")),
            Arg.Any<Action<int>?>());
    }

    #endregion

    #region Version Cleanup Logic Tests

    [TestMethod]
    public async Task Execute_WithLatestOnly_DeletesOldVersionsWhenNewVersionFound()
    {
        // Arrange
        string cachePath = "/cache";
        SetupBasicMocks(cachePath);

        var packages = new List<NuGetPackageInfo>
        {
            new() { PackageId = "mypackage", Version = "3.0.0", TargetFramework = "net6.0",
                   XmlDocumentationPath = "/cache/mypackage/3.0.0/lib/net6.0/MyPackage.xml" }
        };

        mockScanner.ScanDirectory(cachePath).Returns(packages.ToImmutableArray());
        mockScanner.GetLatestVersions(Arg.Any<ImmutableArray<NuGetPackageInfo>>())
            .Returns(packages.ToImmutableArray());

        // Old versions in index
        var indexedPackages = new Dictionary<string, HashSet<(string, string)>>
        {
            ["mypackage"] = [("1.0.0", "net6.0"), ("2.0.0", "net6.0")]
        };

        mockIndexManager.GetIndexedPackageVersionsWithFramework().Returns(indexedPackages);
        mockIndexManager.GetIndexedXmlPaths().Returns(new HashSet<string>());
        mockIndexManager.GetEmptyXmlPaths().Returns(new HashSet<string>());
        mockIndexManager.GetTotalDocuments().Returns(200);

        var indexingResult = CreateIndexingResult(successfulDocs: 50, elapsedMs: 25, bytesProcessed: 5000);
        mockIndexManager.IndexXmlFilesAsync(Arg.Any<List<string>>(), Arg.Any<Action<int>?>())
            .Returns(indexingResult);

        var settings = new NuGetCommand.Settings { IndexPath = "./index", LatestOnly = true };

        // Act
        await command.ExecuteAsync(null!, settings);

        // Assert - Should delete old versions
        mockIndexManager.Received(1).DeleteDocumentsByPackageIds(
            Arg.Is<HashSet<string>>(ids => ids.Contains("mypackage")));
    }

    [TestMethod]
    public async Task Execute_WithComplexVersions_HandlesPreReleaseCorrectly()
    {
        // Arrange
        string cachePath = "/cache";
        SetupBasicMocks(cachePath);

        var packages = new List<NuGetPackageInfo>
        {
            new() { PackageId = "prerelease.package", Version = "2.0.0-preview.1", TargetFramework = "net8.0",
                   XmlDocumentationPath = "/cache/prerelease.package/2.0.0-preview.1/lib/net8.0/Package.xml" }
        };

        mockScanner.ScanDirectory(cachePath).Returns(packages.ToImmutableArray());
        mockScanner.GetLatestVersions(Arg.Any<ImmutableArray<NuGetPackageInfo>>())
            .Returns(packages.ToImmutableArray());

        // Stable version in index
        var indexedPackages = new Dictionary<string, HashSet<(string, string)>>
        {
            ["prerelease.package"] = [("1.0.0", "net8.0")]
        };

        mockIndexManager.GetIndexedPackageVersionsWithFramework().Returns(indexedPackages);
        mockIndexManager.GetIndexedXmlPaths().Returns(new HashSet<string>());
        mockIndexManager.GetEmptyXmlPaths().Returns(new HashSet<string>());

        var indexingResult = CreateIndexingResult(successfulDocs: 50, elapsedMs: 25, bytesProcessed: 5000);
        mockIndexManager.IndexXmlFilesAsync(Arg.Any<List<string>>(), Arg.Any<Action<int>?>())
            .Returns(indexingResult);

        var settings = new NuGetCommand.Settings { IndexPath = "./index", LatestOnly = true };

        // Act
        await command.ExecuteAsync(null!, settings);

        // Assert - Should index the preview version
        await mockIndexManager.Received(1).IndexXmlFilesAsync(
            Arg.Is<List<string>>(files => files.Count == 1),
            Arg.Any<Action<int>?>());
    }

    #endregion

    #region Path Normalization Tests

    [TestMethod]
    public async Task Execute_WithMixedPathFormats_NormalizesCorrectly()
    {
        // Arrange
        string cachePath = @"C:\Users\test\.nuget\packages";
        SetupBasicMocks(cachePath);

        var packages = new List<NuGetPackageInfo>
        {
            // Windows-style paths
            new() { PackageId = "package1", Version = "1.0.0", TargetFramework = "net6.0",
                   XmlDocumentationPath = @"C:\Users\test\.nuget\packages\package1\1.0.0\lib\net6.0\Package1.xml" },
            // Forward slashes
            new() { PackageId = "package2", Version = "1.0.0", TargetFramework = "net6.0",
                   XmlDocumentationPath = "C:/Users/test/.nuget/packages/package2/1.0.0/lib/net6.0/Package2.xml" }
        };

        mockScanner.ScanDirectory(cachePath).Returns(packages.ToImmutableArray());
        mockScanner.GetLatestVersions(Arg.Any<ImmutableArray<NuGetPackageInfo>>())
            .Returns(packages.ToImmutableArray());

        // Index has normalized paths
        var indexedPaths = new HashSet<string>
        {
            "C:/Users/test/.nuget/packages/package1/1.0.0/lib/net6.0/Package1.xml"
        };

        mockIndexManager.GetIndexedPackageVersionsWithFramework()
            .Returns(new Dictionary<string, HashSet<(string, string)>>
            {
                ["package1"] = [("1.0.0", "net6.0")]
            });
        mockIndexManager.GetIndexedXmlPaths().Returns(indexedPaths);
        mockIndexManager.GetEmptyXmlPaths().Returns(new HashSet<string>());

        var indexingResult = CreateIndexingResult(successfulDocs: 50, elapsedMs: 25, bytesProcessed: 5000);
        mockIndexManager.IndexXmlFilesAsync(Arg.Any<List<string>>(), Arg.Any<Action<int>?>())
            .Returns(indexingResult);

        var settings = new NuGetCommand.Settings { IndexPath = "./index", LatestOnly = true };

        // Act
        await command.ExecuteAsync(null!, settings);

        // Assert - Should only index package2 (package1 is already indexed despite different path format)
        await mockIndexManager.Received(1).IndexXmlFilesAsync(
            Arg.Is<List<string>>(files =>
                files.Count == 1 &&
                files[0].Contains("package2")),
            Arg.Any<Action<int>?>());
    }

    #endregion

    #region Edge Cases and Error Handling

    [TestMethod]
    public async Task Execute_WithNoPackagesFound_HandlesGracefully()
    {
        // Arrange
        string cachePath = "/cache";
        SetupBasicMocks(cachePath);

        mockScanner.ScanDirectory(cachePath).Returns(ImmutableArray<NuGetPackageInfo>.Empty);

        var settings = new NuGetCommand.Settings { IndexPath = "./index", LatestOnly = true };

        // Act
        int result = await command.ExecuteAsync(null!, settings);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("No packages found");
    }

    [TestMethod]
    public async Task Execute_WithCacheDirectoryNotFound_ReturnsError()
    {
        // Arrange
        string cachePath = "/nonexistent";
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(false);

        var settings = new NuGetCommand.Settings { IndexPath = "./index" };

        // Act
        int result = await command.ExecuteAsync(null!, settings);

        // Assert
        result.ShouldBe(1);
        console.Output.ShouldContain("NuGet cache directory does not exist");
    }

    #endregion

    #region Helper Methods

    private void SetupBasicMocks(string cachePath)
    {
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(true);
    }

    private void SetupEmptyIndex()
    {
        mockIndexManager.GetIndexedPackageVersions().Returns(new Dictionary<string, HashSet<string>>());
        mockIndexManager.GetIndexedPackageVersionsWithFramework().Returns(new Dictionary<string, HashSet<(string, string)>>());
        mockIndexManager.GetIndexedXmlPaths().Returns(new HashSet<string>());
        mockIndexManager.GetEmptyXmlPaths().Returns(new HashSet<string>());
        mockIndexManager.GetTotalDocuments().Returns(0);
    }

    private static IndexingResult CreateIndexingResult(int successfulDocs, int elapsedMs, long bytesProcessed)
    {
        return new IndexingResult
        {
            SuccessfulDocuments = successfulDocs,
            FailedDocuments = 0,
            ElapsedTime = TimeSpan.FromMilliseconds(elapsedMs),
            BytesProcessed = bytesProcessed,
            Errors = ImmutableArray<string>.Empty,
            TotalDocuments = successfulDocs,
            Metrics = new PerformanceMetrics
            {
                AverageBatchCommitTimeMs = 0.5,
                PeakThreadCount = 4,
                PeakWorkingSetBytes = 100_000_000,
                Gen0Collections = 10,
                Gen1Collections = 2,
                Gen2Collections = 0,
                TotalAllocatedBytes = 50_000_000,
                AverageParseTimeMs = 0.1,
                AverageIndexTimeMs = 0.2,
                CpuUsagePercent = 25.0,
                DocumentsPooled = 100,
                StringsInterned = 500
            }
        };
    }

    #endregion

    public void Dispose()
    {
        console?.Dispose();
    }
}