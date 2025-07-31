using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Tests.Extensions;
using ApiLens.Core.Tests.Helpers;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using NSubstitute;
using System.Linq;

namespace ApiLens.Core.Tests.Lucene;

[TestClass]
public class LuceneIndexManagerAdvancedTests : IDisposable
{
    private string indexPath = null!;
    private IXmlDocumentParser mockParser = null!;
    private IDocumentBuilder mockDocumentBuilder = null!;
    private LuceneIndexManager indexManager = null!;

    [TestInitialize]
    public void Setup()
    {
        indexPath = Path.Combine(Path.GetTempPath(), $"test_index_{Guid.NewGuid()}");
        mockParser = Substitute.For<IXmlDocumentParser>();
        mockDocumentBuilder = Substitute.For<IDocumentBuilder>();
        indexManager = new LuceneIndexManager(indexPath, mockParser, mockDocumentBuilder);
    }

    [TestCleanup]
    public void Cleanup()
    {
        indexManager?.Dispose();
        if (Directory.Exists(indexPath))
        {
            Directory.Delete(indexPath, true);
        }
    }

    #region Framework-Aware Package Tracking Tests

    [TestMethod]
    public async Task GetIndexedPackageVersionsWithFramework_ReturnsCorrectFrameworkInfo()
    {
        // Arrange - Index members with different frameworks
        var members = new List<MemberInfo>
        {
            CreateMemberInfo("microsoft.extensions.logging", "8.0.0", "net6.0"),
            CreateMemberInfo("microsoft.extensions.logging", "8.0.0", "net7.0"),
            CreateMemberInfo("microsoft.extensions.logging", "8.0.0", "net8.0"),
            CreateMemberInfo("newtonsoft.json", "13.0.3", "net6.0"),
            CreateMemberInfo("newtonsoft.json", "13.0.3", "netstandard2.0")
        };

        // Setup document builder to create proper documents
        foreach (var member in members)
        {
            var doc = CreateDocument(member);
            mockDocumentBuilder.BuildDocument(member).Returns(doc);
        }

        await indexManager.IndexBatchAsync(members);

        // Act
        var packageVersionsWithFramework = indexManager.GetIndexedPackageVersionsWithFramework();

        // Assert
        packageVersionsWithFramework.Count.ShouldBe(2);
        
        // Microsoft.Extensions.Logging should have 3 framework entries
        var msLogging = packageVersionsWithFramework["microsoft.extensions.logging"];
        msLogging.Count.ShouldBe(3);
        msLogging.ShouldContain(("8.0.0", "net6.0"));
        msLogging.ShouldContain(("8.0.0", "net7.0"));
        msLogging.ShouldContain(("8.0.0", "net8.0"));

        // Newtonsoft.Json should have 2 framework entries
        var newtonsoftJson = packageVersionsWithFramework["newtonsoft.json"];
        newtonsoftJson.Count.ShouldBe(2);
        newtonsoftJson.ShouldContain(("13.0.3", "net6.0"));
        newtonsoftJson.ShouldContain(("13.0.3", "netstandard2.0"));
    }

    [TestMethod]
    public async Task GetIndexedPackageVersionsWithFramework_HandlesLegacyEntriesWithoutFramework()
    {
        // Arrange - Mix of new entries with framework and legacy without
        var members = new List<MemberInfo>
        {
            CreateMemberInfo("oldpackage", "1.0.0", null), // Legacy entry
            CreateMemberInfo("oldpackage", "2.0.0", "net6.0"), // New entry with framework
            CreateMemberInfo("newpackage", "1.0.0", "net7.0")
        };

        foreach (var member in members)
        {
            var doc = CreateDocument(member);
            mockDocumentBuilder.BuildDocument(member).Returns(doc);
        }

        await indexManager.IndexBatchAsync(members);

        // Act
        var packageVersionsWithFramework = indexManager.GetIndexedPackageVersionsWithFramework();

        // Assert
        var oldPackage = packageVersionsWithFramework["oldpackage"];
        oldPackage.Count.ShouldBe(2);
        oldPackage.ShouldContain(("1.0.0", "unknown")); // Legacy entries get "unknown"
        oldPackage.ShouldContain(("2.0.0", "net6.0"));
    }

    #endregion

    #region Empty File Tracking Tests

    [TestMethod]
    public async Task IndexXmlFilesAsync_WithEmptyFile_CreatesEmptyFileMarker()
    {
        // Arrange
        string emptyXmlPath = "/test/empty.xml";
        string normalXmlPath = "/test/normal.xml";

        // Empty file returns no members
        mockParser.ParseXmlFileStreamAsync(emptyXmlPath, Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<MemberInfo>());

        // Normal file returns one member
        var normalMember = CreateMemberInfo("normalpackage", "1.0.0", "net6.0");
        mockParser.ParseXmlFileStreamAsync(normalXmlPath, Arg.Any<CancellationToken>())
            .Returns(CreateAsyncEnumerable(normalMember));

        mockDocumentBuilder.BuildDocument(normalMember).Returns(CreateDocument(normalMember));

        // Act
        var result = await indexManager.IndexXmlFilesAsync(new[] { emptyXmlPath, normalXmlPath });
        await indexManager.CommitAsync();

        // Debug - check what was indexed
        var totalDocs = indexManager.GetTotalDocuments();
        var stats = indexManager.GetIndexStatistics();

        // Assert
        result.SuccessfulDocuments.ShouldBeGreaterThan(0); // Should have at least the normal document
        totalDocs.ShouldBeGreaterThan(0);
        
        var emptyPaths = indexManager.GetEmptyXmlPaths();
        emptyPaths.Count.ShouldBe(1);
        emptyPaths.ShouldContain("/test/empty.xml");

        // Verify empty file document was created
        var emptyFileDoc = SearchForEmptyFileDocument(emptyXmlPath);
        emptyFileDoc.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task GetEmptyXmlPaths_ReturnsAllEmptyFilePaths()
    {
        // Arrange - Index multiple empty files
        var emptyFiles = new[] { "/test/empty1.xml", "/test/empty2.xml", "/test/subdir/empty3.xml" };
        
        foreach (var file in emptyFiles)
        {
            mockParser.ParseXmlFileStreamAsync(file, Arg.Any<CancellationToken>())
                .Returns(AsyncEnumerable.Empty<MemberInfo>());
        }

        await indexManager.IndexXmlFilesAsync(emptyFiles);
        await indexManager.CommitAsync();

        // Act
        var emptyPaths = indexManager.GetEmptyXmlPaths();

        // Assert
        emptyPaths.Count.ShouldBe(3);
        foreach (var file in emptyFiles)
        {
            emptyPaths.ShouldContain(file);
        }
    }

    [TestMethod]
    public async Task GetEmptyXmlPaths_WithPathNormalization_ReturnsNormalizedPaths()
    {
        // Arrange - Windows-style path
        string windowsPath = @"C:\test\empty.xml";
        
        mockParser.ParseXmlFileStreamAsync(windowsPath, Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<MemberInfo>());

        await indexManager.IndexXmlFilesAsync(new[] { windowsPath });
        await indexManager.CommitAsync();

        // Act
        var emptyPaths = indexManager.GetEmptyXmlPaths();

        // Assert
        emptyPaths.Count.ShouldBe(1);
        // Path should be normalized with forward slashes
        emptyPaths.First().ShouldBe("C:/test/empty.xml");
    }

    #endregion

    #region XML Path Tracking Tests

    [TestMethod]
    public async Task GetIndexedXmlPaths_ReturnsUniquePathsAcrossAllDocuments()
    {
        // Arrange - Multiple members from same XML files
        var members = new List<MemberInfo>
        {
            CreateMemberInfo("package1", "1.0.0", "net6.0", "/cache/package1/1.0.0/lib/net6.0/Package1.xml"),
            CreateMemberInfo("package1", "1.0.0", "net6.0", "/cache/package1/1.0.0/lib/net6.0/Package1.xml"), // Same file
            CreateMemberInfo("package2", "2.0.0", "net7.0", "/cache/package2/2.0.0/lib/net7.0/Package2.xml")
        };

        foreach (var member in members)
        {
            var doc = CreateDocument(member);
            mockDocumentBuilder.BuildDocument(member).Returns(doc);
        }

        await indexManager.IndexBatchAsync(members);

        // Act
        var xmlPaths = indexManager.GetIndexedXmlPaths();

        // Assert
        xmlPaths.Count.ShouldBe(2); // Only unique paths
        xmlPaths.ShouldContain("/cache/package1/1.0.0/lib/net6.0/Package1.xml");
        xmlPaths.ShouldContain("/cache/package2/2.0.0/lib/net7.0/Package2.xml");
    }

    [TestMethod]
    public async Task GetIndexedXmlPaths_WithSharedXmlFiles_ReturnsPathOnce()
    {
        // Arrange - Multiple frameworks sharing same XML
        var sharedPath = "/cache/microsoft.extensions.logging/8.0.0/lib/netstandard2.0/Microsoft.Extensions.Logging.xml";
        var members = new List<MemberInfo>
        {
            CreateMemberInfo("microsoft.extensions.logging", "8.0.0", "net6.0", sharedPath),
            CreateMemberInfo("microsoft.extensions.logging", "8.0.0", "net7.0", sharedPath),
            CreateMemberInfo("microsoft.extensions.logging", "8.0.0", "net8.0", sharedPath)
        };

        foreach (var member in members)
        {
            var doc = CreateDocument(member);
            mockDocumentBuilder.BuildDocument(member).Returns(doc);
        }

        await indexManager.IndexBatchAsync(members);

        // Act
        var xmlPaths = indexManager.GetIndexedXmlPaths();

        // Assert
        xmlPaths.Count.ShouldBe(1);
        xmlPaths.ShouldContain(sharedPath);
    }

    #endregion

    #region Complex Version Comparison Tests

    [TestMethod]
    public async Task DeleteDocumentsByPackageIds_RemovesAllVersionsAndFrameworks()
    {
        // Arrange
        var members = new List<MemberInfo>
        {
            CreateMemberInfo("package1", "1.0.0", "net6.0"),
            CreateMemberInfo("package1", "1.0.0", "net7.0"),
            CreateMemberInfo("package1", "2.0.0", "net6.0"),
            CreateMemberInfo("package2", "1.0.0", "net6.0")
        };

        foreach (var member in members)
        {
            var doc = CreateDocument(member);
            mockDocumentBuilder.BuildDocument(member).Returns(doc);
        }

        await indexManager.IndexBatchAsync(members);
        await indexManager.CommitAsync();

        // Act
        indexManager.DeleteDocumentsByPackageIds(new[] { "package1" });
        await indexManager.CommitAsync();

        // Assert
        var remainingPackages = indexManager.GetIndexedPackageVersionsWithFramework();
        remainingPackages.Count.ShouldBe(1);
        remainingPackages.ShouldContainKey("package2");
        remainingPackages.ShouldNotContainKey("package1");
    }

    #endregion

    #region Performance and Concurrency Tests

    [TestMethod]
    public async Task IndexXmlFilesAsync_WithConcurrentFiles_HandlesCorrectly()
    {
        // Arrange - Simulate concurrent indexing of multiple files
        int fileCount = 10;
        var files = Enumerable.Range(1, fileCount).Select(i => $"/test/file{i}.xml").ToList();
        
        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var members = Enumerable.Range(1, 5).Select(j => 
                CreateMemberInfo($"package{i + 1}", "1.0.0", "net6.0", file, $"Type{j}")).ToList();
            
            mockParser.ParseXmlFileStreamAsync(file, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncEnumerable(members.ToArray()));

            // Use Arg.Any<MemberInfo>() to match any member
            mockDocumentBuilder.BuildDocument(Arg.Any<MemberInfo>())
                .Returns(args => CreateDocument((MemberInfo)args[0]));
        }

        // Act
        var result = await indexManager.IndexXmlFilesAsync(files);
        await indexManager.CommitAsync();

        // Assert
        if (result.Errors.Any())
        {
            throw new Exception($"Indexing errors: {string.Join(", ", result.Errors)}");
        }
        
        // Debug output
        var afterIndexCount = indexManager.GetTotalDocuments();
        
        result.SuccessfulDocuments.ShouldBe(fileCount * 5); // 5 members per file
        result.FailedDocuments.ShouldBe(0);
        
        var totalDocs = indexManager.GetTotalDocuments();
        totalDocs.ShouldBe(fileCount * 5);
    }

    #endregion

    #region Helper Methods

    private static MemberInfo CreateMemberInfo(string packageId, string version, string? framework, string? sourcePath = null, string? typeSuffix = null)
    {
        var typeName = typeSuffix != null ? $"Test.{typeSuffix}" : "Test.Type";
        return new MemberInfo
        {
            Id = $"T:{typeName}|{packageId}|{version}|{framework ?? "unknown"}",
            Name = typeName,
            FullName = typeName,
            MemberType = MemberType.Type,
            Assembly = "Test",
            Namespace = "Test",
            PackageId = packageId,
            PackageVersion = version,
            TargetFramework = framework ?? "unknown",
            IsFromNuGetCache = true,
            SourceFilePath = sourcePath ?? $"/cache/{packageId}/{version}/lib/{framework ?? "unknown"}/{packageId}.xml",
            IndexedAt = DateTime.UtcNow
        };
    }

    private static Document CreateDocument(MemberInfo member)
    {
        var doc = new Document
        {
            new StringField("id", member.Id, Field.Store.YES),
            new StringField("memberType", member.MemberType.ToString(), Field.Store.YES),
            new StringField("name", member.Name, Field.Store.YES),
            new StringField("fullName", member.FullName, Field.Store.YES),
            new StringField("assembly", member.Assembly, Field.Store.YES),
            new StringField("namespace", member.Namespace, Field.Store.YES)
        };

        if (!string.IsNullOrEmpty(member.PackageId))
            doc.Add(new StringField("packageId", member.PackageId, Field.Store.YES));

        if (!string.IsNullOrEmpty(member.PackageVersion))
            doc.Add(new StringField("packageVersion", member.PackageVersion, Field.Store.YES));

        if (!string.IsNullOrEmpty(member.TargetFramework))
            doc.Add(new StringField("targetFramework", member.TargetFramework, Field.Store.YES));

        if (!string.IsNullOrEmpty(member.SourceFilePath))
            doc.Add(new StringField("sourceFilePath", member.SourceFilePath, Field.Store.YES));

        return doc;
    }

    private Document? SearchForEmptyFileDocument(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);
        var searchTerm = $"EMPTY_FILE|{normalizedPath}";
        var results = indexManager.SearchByField("id", searchTerm, 1);
        
        if (results.TotalHits > 0)
        {
            return indexManager.GetDocument(results.ScoreDocs[0].Doc);
        }
        
        return null;
    }

    private static string NormalizePath(string path)
    {
        // Match the implementation in LuceneIndexManager
        return path.Replace('\\', '/');
    }

    private static async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    public void Dispose()
    {
        Cleanup();
    }

    #endregion
}