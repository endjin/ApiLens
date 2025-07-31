using System.Collections.Immutable;
using ApiLens.Cli.Commands;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Services;
using NSubstitute;
using Spectre.Console;
using Spectre.Console.Testing;

namespace ApiLens.Cli.Tests.Commands;

[TestClass]
public sealed class NuGetCommandSkipCountFixTests : IDisposable
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

    [TestMethod]
    public async Task Execute_WithLatestOnly_HandlesSharedXmlFiles_CorrectSkipCount()
    {
        // Arrange - This test validates the fix for the skip count issue
        // Scenario: Multiple frameworks share the same XML documentation file
        string cachePath = "/cache";
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(true);

        // Create packages where multiple frameworks SHARE the same XML file
        // This is common in real NuGet packages
        List<NuGetPackageInfo> allPackages =
        [
            // Microsoft.Extensions.Logging: Same XML file for all frameworks
            new() { PackageId = "microsoft.extensions.logging", Version = "8.0.0", TargetFramework = "net6.0", 
                   XmlDocumentationPath = "/cache/microsoft.extensions.logging/8.0.0/lib/netstandard2.0/Microsoft.Extensions.Logging.xml" },
            new() { PackageId = "microsoft.extensions.logging", Version = "8.0.0", TargetFramework = "net7.0", 
                   XmlDocumentationPath = "/cache/microsoft.extensions.logging/8.0.0/lib/netstandard2.0/Microsoft.Extensions.Logging.xml" },
            new() { PackageId = "microsoft.extensions.logging", Version = "8.0.0", TargetFramework = "net8.0", 
                   XmlDocumentationPath = "/cache/microsoft.extensions.logging/8.0.0/lib/netstandard2.0/Microsoft.Extensions.Logging.xml" },
            
            // Newtonsoft.Json: Different XML files for different frameworks
            new() { PackageId = "newtonsoft.json", Version = "13.0.3", TargetFramework = "net6.0", 
                   XmlDocumentationPath = "/cache/newtonsoft.json/13.0.3/lib/net6.0/Newtonsoft.Json.xml" },
            new() { PackageId = "newtonsoft.json", Version = "13.0.3", TargetFramework = "netstandard2.0", 
                   XmlDocumentationPath = "/cache/newtonsoft.json/13.0.3/lib/netstandard2.0/Newtonsoft.Json.xml" }
        ];

        mockScanner.ScanDirectory(cachePath).Returns(allPackages.ToImmutableArray());

        // GetLatestVersions returns all of them (grouped by package+framework)
        mockScanner.GetLatestVersions(Arg.Any<ImmutableArray<NuGetPackageInfo>>())
            .Returns(allPackages.ToImmutableArray());

        // Empty index
        mockIndexManager.GetIndexedPackageVersions().Returns(new Dictionary<string, HashSet<string>>());
        mockIndexManager.GetIndexedPackageVersionsWithFramework().Returns(new Dictionary<string, HashSet<(string, string)>>());
        mockIndexManager.GetIndexedXmlPaths().Returns(new HashSet<string>());
        mockIndexManager.GetEmptyXmlPaths().Returns(new HashSet<string>());
        mockIndexManager.GetTotalDocuments().Returns(0);

        // Setup indexing result
        var indexingResult = CreateIndexingResult(successfulDocs: 100, elapsedMs: 50, bytesProcessed: 10000);
        mockIndexManager.IndexXmlFilesAsync(Arg.Any<List<string>>(), Arg.Any<Action<int>?>())
            .Returns(indexingResult);

        mockIndexManager.GetIndexStatistics().Returns(new IndexStatistics
        {
            DocumentCount = 100,
            TotalSizeInBytes = 10000,
            IndexPath = "./index",
            FieldCount = 10,
            FileCount = 3  // 3 unique XML files
        });

        var settings = new NuGetCommand.Settings
        {
            IndexPath = "./index",
            LatestOnly = true
        };

        // Act
        int result = await command.ExecuteAsync(null!, settings);

        // Assert
        result.ShouldBe(0);
        
        // Verify that we're indexing the correct number of UNIQUE XML files
        await mockIndexManager.Received(1).IndexXmlFilesAsync(
            Arg.Is<List<string>>(files => files.Count == 3), // 3 unique XML files, not 5 packages
            Arg.Any<Action<int>?>());
    }

    [TestMethod]
    public async Task Execute_WithLatestOnly_DifferentXmlPerFramework_IndexesAll()
    {
        // Arrange - Test case where each framework has different XML documentation
        string cachePath = "/cache";
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(true);

        // Create packages where each framework has its own XML file
        List<NuGetPackageInfo> allPackages =
        [
            new() { PackageId = "mypackage", Version = "1.0.0", TargetFramework = "net48", 
                   XmlDocumentationPath = "/cache/mypackage/1.0.0/lib/net48/MyPackage.xml" },
            new() { PackageId = "mypackage", Version = "1.0.0", TargetFramework = "netcoreapp3.1", 
                   XmlDocumentationPath = "/cache/mypackage/1.0.0/lib/netcoreapp3.1/MyPackage.xml" },
            new() { PackageId = "mypackage", Version = "1.0.0", TargetFramework = "net6.0", 
                   XmlDocumentationPath = "/cache/mypackage/1.0.0/lib/net6.0/MyPackage.xml" }
        ];

        mockScanner.ScanDirectory(cachePath).Returns(allPackages.ToImmutableArray());
        mockScanner.GetLatestVersions(Arg.Any<ImmutableArray<NuGetPackageInfo>>())
            .Returns(allPackages.ToImmutableArray());

        // Empty index
        mockIndexManager.GetIndexedPackageVersions().Returns(new Dictionary<string, HashSet<string>>());
        mockIndexManager.GetIndexedPackageVersionsWithFramework().Returns(new Dictionary<string, HashSet<(string, string)>>());
        mockIndexManager.GetIndexedXmlPaths().Returns(new HashSet<string>());
        mockIndexManager.GetEmptyXmlPaths().Returns(new HashSet<string>());
        mockIndexManager.GetTotalDocuments().Returns(0);

        var indexingResult = CreateIndexingResult(successfulDocs: 150, elapsedMs: 75, bytesProcessed: 15000);
        mockIndexManager.IndexXmlFilesAsync(Arg.Any<List<string>>(), Arg.Any<Action<int>?>())
            .Returns(indexingResult);

        mockIndexManager.GetIndexStatistics().Returns(new IndexStatistics
        {
            DocumentCount = 150,
            TotalSizeInBytes = 15000,
            IndexPath = "./index",
            FieldCount = 10,
            FileCount = 3
        });

        var settings = new NuGetCommand.Settings
        {
            IndexPath = "./index",
            LatestOnly = true
        };

        // Act
        int result = await command.ExecuteAsync(null!, settings);

        // Assert
        result.ShouldBe(0);
        
        // Should index all 3 XML files since they're all different
        await mockIndexManager.Received(1).IndexXmlFilesAsync(
            Arg.Is<List<string>>(files => files.Count == 3 && 
                files.Contains("/cache/mypackage/1.0.0/lib/net48/MyPackage.xml") &&
                files.Contains("/cache/mypackage/1.0.0/lib/netcoreapp3.1/MyPackage.xml") &&
                files.Contains("/cache/mypackage/1.0.0/lib/net6.0/MyPackage.xml")),
            Arg.Any<Action<int>?>());
    }

    [TestMethod]
    public async Task Execute_WithLatestOnly_CorrectlyReportsSkipCount()
    {
        // Arrange - Verify skip count is based on unique XML files, not package entries
        string cachePath = "/cache";
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(true);

        // 10 package entries, but only 4 unique XML files
        List<NuGetPackageInfo> allPackages = [];
        
        // Package 1: 3 frameworks, 1 XML file
        for (int i = 0; i < 3; i++)
        {
            allPackages.Add(new() 
            { 
                PackageId = "package1", 
                Version = "1.0.0", 
                TargetFramework = $"net{6 + i}.0",
                XmlDocumentationPath = "/cache/package1/1.0.0/lib/netstandard2.0/Package1.xml" // Same file
            });
        }
        
        // Package 2: 3 frameworks, 3 XML files
        for (int i = 0; i < 3; i++)
        {
            allPackages.Add(new() 
            { 
                PackageId = "package2", 
                Version = "2.0.0", 
                TargetFramework = $"net{6 + i}.0",
                XmlDocumentationPath = $"/cache/package2/2.0.0/lib/net{6 + i}.0/Package2.xml" // Different files
            });
        }

        mockScanner.ScanDirectory(cachePath).Returns(allPackages.ToImmutableArray());
        mockScanner.GetLatestVersions(Arg.Any<ImmutableArray<NuGetPackageInfo>>())
            .Returns(allPackages.ToImmutableArray());

        // Index already contains package1
        Dictionary<string, HashSet<string>> indexedPackages = new()
        {
            ["package1"] = ["1.0.0"]
        };
        mockIndexManager.GetIndexedPackageVersions().Returns(indexedPackages);
        
        // Framework-aware tracking - package1 has all 3 frameworks indexed
        Dictionary<string, HashSet<(string, string)>> indexedPackagesWithFramework = new()
        {
            ["package1"] = [("1.0.0", "net6.0"), ("1.0.0", "net7.0"), ("1.0.0", "net8.0")]
        };
        mockIndexManager.GetIndexedPackageVersionsWithFramework().Returns(indexedPackagesWithFramework);
        mockIndexManager.GetIndexedXmlPaths().Returns(new HashSet<string> { "/cache/package1/1.0.0/lib/netstandard2.0/Package1.xml" });
        mockIndexManager.GetEmptyXmlPaths().Returns(new HashSet<string>());
        mockIndexManager.GetTotalDocuments().Returns(100); // Some documents already in index

        var indexingResult = CreateIndexingResult(successfulDocs: 30, elapsedMs: 25, bytesProcessed: 3000);
        mockIndexManager.IndexXmlFilesAsync(Arg.Any<List<string>>(), Arg.Any<Action<int>?>())
            .Returns(indexingResult);

        mockIndexManager.GetIndexStatistics().Returns(new IndexStatistics
        {
            DocumentCount = 130,
            TotalSizeInBytes = 13000,
            IndexPath = "./index",
            FieldCount = 10,
            FileCount = 4
        });

        var settings = new NuGetCommand.Settings
        {
            IndexPath = "./index",
            LatestOnly = true
        };

        // Act
        int result = await command.ExecuteAsync(null!, settings);

        // Assert
        result.ShouldBe(0);
        
        // Should skip 1 file (package1's shared XML) and index 3 files (package2's individual XMLs)
        await mockIndexManager.Received(1).IndexXmlFilesAsync(
            Arg.Is<List<string>>(files => files.Count == 3), // Only package2's 3 XML files
            Arg.Any<Action<int>?>());
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

    [TestCleanup]
    public void Cleanup()
    {
        console?.Dispose();
    }

    public void Dispose()
    {
        console?.Dispose();
    }
}