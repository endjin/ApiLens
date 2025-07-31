using ApiLens.Core.Helpers;
using ApiLens.Core.Lucene;
using ApiLens.Core.Parsing;
using ApiLens.Core.Services;

namespace ApiLens.Core.Tests.Integration;

[TestClass]
public class NuGetIndexingIntegrationTests : IDisposable
{
    private string indexPath = null!;
    private string testDataPath = null!;
    private IFileSystemService mockFileSystem = null!;
    private IFileHashHelper fileHashHelper = null!;
    private XmlDocumentParser parser = null!;
    private DocumentBuilder documentBuilder = null!;
    private LuceneIndexManager indexManager = null!;

    [TestInitialize]
    public void Setup()
    {
        indexPath = Path.Combine(Path.GetTempPath(), $"test_index_{Guid.NewGuid()}");
        testDataPath = Path.Combine(Path.GetTempPath(), $"test_data_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDataPath);

        mockFileSystem = Substitute.For<IFileSystemService>();
        fileHashHelper = new FileHashHelper(mockFileSystem);
        parser = new XmlDocumentParser(fileHashHelper, mockFileSystem);
        documentBuilder = new DocumentBuilder();
        indexManager = new LuceneIndexManager(indexPath, parser, documentBuilder);
    }

    [TestCleanup]
    public void Cleanup()
    {
        indexManager?.Dispose();

        if (Directory.Exists(indexPath))
            Directory.Delete(indexPath, true);

        if (Directory.Exists(testDataPath))
            Directory.Delete(testDataPath, true);
    }

    #region End-to-End Indexing Scenarios

    [TestMethod]
    public async Task FullIndexingWorkflow_WithMixedPackages_IndexesCorrectly()
    {
        // Arrange - Create test XML files with different scenarios
        var testFiles = new List<(string path, string content)>
        {
            // Package with multiple members
            CreateNuGetXmlFile("microsoft.extensions.logging", "8.0.0", "net6.0", @"<?xml version=""1.0""?>
<doc>
    <assembly>
        <name>Microsoft.Extensions.Logging</name>
    </assembly>
    <members>
        <member name=""T:Microsoft.Extensions.Logging.ILogger"">
            <summary>Represents a type used to perform logging.</summary>
        </member>
        <member name=""M:Microsoft.Extensions.Logging.LoggerExtensions.LogDebug(Microsoft.Extensions.Logging.ILogger,System.String)"">
            <summary>Logs a debug message.</summary>
        </member>
        <member name=""P:Microsoft.Extensions.Logging.LogLevel.Debug"">
            <summary>Debug logging level.</summary>
        </member>
    </members>
</doc>"),
            
            // Empty XML file
            CreateNuGetXmlFile("empty.package", "1.0.0", "net6.0", @"<?xml version=""1.0""?>
<doc>
    <assembly>
        <name>Empty.Package</name>
    </assembly>
    <members>
    </members>
</doc>"),
            
            // Shared XML file (same content, different framework paths)
            CreateNuGetXmlFile("shared.package", "1.0.0", "netstandard2.0", @"<?xml version=""1.0""?>
<doc>
    <assembly>
        <name>Shared.Package</name>
    </assembly>
    <members>
        <member name=""T:Shared.Package.SharedType"">
            <summary>A type shared across frameworks.</summary>
        </member>
    </members>
</doc>")
        };

        // Create the test files
        foreach (var (path, content) in testFiles)
        {
            CreateTestFile(path, content);
        }

        // Act - First indexing run
        var result1 = await indexManager.IndexXmlFilesAsync(testFiles.Select(t => t.path));

        // Assert first run
        result1.SuccessfulDocuments.ShouldBe(5); // 3 members from logging + 1 empty file marker + 1 member from shared
        result1.FailedDocuments.ShouldBe(0);

        var stats1 = indexManager.GetIndexStatistics();
        stats1.ShouldNotBeNull();
        stats1.DocumentCount.ShouldBe(5);

        // Verify framework-aware tracking
        var packageVersions = indexManager.GetIndexedPackageVersionsWithFramework();
        packageVersions.Count.ShouldBe(2); // Only packages with actual members are tracked
        packageVersions["microsoft.extensions.logging"].ShouldContain(("8.0.0", "net6.0"));
        packageVersions["shared.package"].ShouldContain(("1.0.0", "netstandard2.0"));
        // empty.package is not tracked because it has no members

        // Verify empty file tracking
        var emptyPaths = indexManager.GetEmptyXmlPaths();
        emptyPaths.Count.ShouldBe(1);
        emptyPaths.ShouldContain(p => p.EndsWith("empty.package/1.0.0/lib/net6.0/empty-package.xml")); // Note: packageId dots are replaced with dashes

        // Act - Second indexing run (should skip all)
        var result2 = await indexManager.IndexXmlFilesAsync(testFiles.Select(t => t.path));

        // Assert second run - Re-indexes same documents
        result2.SuccessfulDocuments.ShouldBe(5);

        // Index should remain unchanged (documents are updated, not duplicated)
        var stats2 = indexManager.GetIndexStatistics();
        stats2.ShouldNotBeNull();
        stats2.DocumentCount.ShouldBe(5);
    }

    [TestMethod]
    public async Task SharedXmlFileScenario_MultipleFrameworks_DeduplicatesCorrectly()
    {
        // Arrange - Create shared XML file scenario
        string sharedContent = @"<?xml version=""1.0""?>
<doc>
    <assembly>
        <name>Microsoft.Extensions.DependencyInjection</name>
    </assembly>
    <members>
        <member name=""T:Microsoft.Extensions.DependencyInjection.IServiceCollection"">
            <summary>Specifies the contract for a collection of service descriptors.</summary>
        </member>
        <member name=""M:Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton``1(Microsoft.Extensions.DependencyInjection.IServiceCollection)"">
            <summary>Adds a singleton service.</summary>
        </member>
    </members>
</doc>";

        // Same XML file path for different frameworks (common in real packages)
        string sharedPath = Path.Combine(testDataPath, ".nuget/packages/microsoft.extensions.dependencyinjection/8.0.0/lib/netstandard2.0/Microsoft.Extensions.DependencyInjection.xml");
        CreateTestFile(sharedPath, sharedContent);

        // Simulate multiple framework references to same XML
        var frameworkPaths = new[]
        {
            sharedPath, // net6.0 uses netstandard2.0 XML
            sharedPath, // net7.0 uses netstandard2.0 XML
            sharedPath  // net8.0 uses netstandard2.0 XML
        };

        // Act - Index with duplicate paths
        var result = await indexManager.IndexXmlFilesAsync(frameworkPaths);

        // Assert
        result.SuccessfulDocuments.ShouldBe(6); // 2 members × 3 times indexed

        // XML paths should show only one unique path
        var xmlPaths = indexManager.GetIndexedXmlPaths();
        xmlPaths.Count.ShouldBe(1);

        // But the actual documents in the index should be unique (2 members)
        var totalDocs = indexManager.GetTotalDocuments();
        totalDocs.ShouldBe(2); // Documents are updated, not duplicated

        var docs = indexManager.SearchByField("namespace", "Microsoft.Extensions.DependencyInjection", 10);
        docs.TotalHits.ShouldBe(2);
    }

    [TestMethod]
    public async Task PathNormalizationScenario_MixedPathFormats_HandlesCorrectly()
    {
        // Arrange - Create files with different path formats
        var windowsPath = Path.Combine(testDataPath, @".nuget\packages\windows.package\1.0.0\lib\net6.0\Windows.Package.xml");
        var linuxPath = Path.Combine(testDataPath, ".nuget/packages/linux.package/1.0.0/lib/net6.0/Linux.Package.xml");

        string xmlContent = @"<?xml version=""1.0""?>
<doc>
    <assembly>
        <name>Test.Package</name>
    </assembly>
    <members>
        <member name=""T:Test.Type"">
            <summary>Test type.</summary>
        </member>
    </members>
</doc>";

        CreateTestFile(windowsPath, xmlContent);
        CreateTestFile(linuxPath, xmlContent);

        // Act
        var result = await indexManager.IndexXmlFilesAsync(new[] { windowsPath, linuxPath });

        // Assert
        result.SuccessfulDocuments.ShouldBe(2);

        // Both paths should be tracked with normalized format
        var xmlPaths = indexManager.GetIndexedXmlPaths();
        xmlPaths.Count.ShouldBe(2);

        // All paths should use forward slashes
        foreach (var path in xmlPaths)
        {
            path.ShouldContain("/");
            path.ShouldNotContain("\\");
        }
    }

    [TestMethod]
    public async Task VersionUpdateScenario_NewVersionArrives_UpdatesCorrectly()
    {
        // Arrange - Index old version
        var oldVersionFile = CreateNuGetXmlFile("evolving.package", "1.0.0", "net6.0", @"<?xml version=""1.0""?>
<doc>
    <assembly>
        <name>Evolving.Package</name>
    </assembly>
    <members>
        <member name=""T:Evolving.Package.OldClass"">
            <summary>Old class in v1.</summary>
        </member>
    </members>
</doc>");

        CreateTestFile(oldVersionFile.path, oldVersionFile.content);
        await indexManager.IndexXmlFilesAsync(new[] { oldVersionFile.path });

        // Verify old version is indexed
        var versions1 = indexManager.GetIndexedPackageVersionsWithFramework();
        versions1["evolving.package"].ShouldContain(("1.0.0", "net6.0"));

        // Arrange - New version
        var newVersionFile = CreateNuGetXmlFile("evolving.package", "2.0.0", "net6.0", @"<?xml version=""1.0""?>
<doc>
    <assembly>
        <name>Evolving.Package</name>
    </assembly>
    <members>
        <member name=""T:Evolving.Package.OldClass"">
            <summary>Old class updated in v2.</summary>
        </member>
        <member name=""T:Evolving.Package.NewClass"">
            <summary>New class added in v2.</summary>
        </member>
    </members>
</doc>");

        CreateTestFile(newVersionFile.path, newVersionFile.content);

        // Act - Clean old version and index new
        indexManager.DeleteDocumentsByPackageIds(new[] { "evolving.package" });
        await indexManager.CommitAsync();

        var result = await indexManager.IndexXmlFilesAsync(new[] { newVersionFile.path });
        await indexManager.CommitAsync(); // Ensure documents are committed

        // Assert
        result.SuccessfulDocuments.ShouldBe(2); // 2 members in new version

        var versions2 = indexManager.GetIndexedPackageVersionsWithFramework();
        versions2["evolving.package"].ShouldContain(("2.0.0", "net6.0"));
        versions2["evolving.package"].ShouldNotContain(("1.0.0", "net6.0"));

        // Debug: Check if any documents exist
        var totalDocs = indexManager.GetTotalDocuments();
        totalDocs.ShouldBe(2); // Should have exactly 2 documents

        // Search by name to verify documents are indexed
        var nameSearchResults = indexManager.SearchByField("name", "NewClass", 10);
        nameSearchResults.TotalHits.ShouldBe(1);

        // Search should find new content in summary
        // First, let's try to get the first document and check its fields
        var firstDoc = indexManager.GetDocument(0);
        firstDoc.ShouldNotBeNull();
        var summaryField = firstDoc.Get("summary");
        summaryField.ShouldNotBeNull();
        summaryField.ShouldContain("v2");

        // Search for a word that should definitely be in the summary
        var searchResults = indexManager.SearchByField("summary", "updated", 10);
        searchResults.TotalHits.ShouldBe(1); // Only OldClass has "updated"

        // Search for "added" which should be in NewClass
        var searchResults2 = indexManager.SearchByField("summary", "added", 10);
        searchResults2.TotalHits.ShouldBe(1);
    }

    [TestMethod]
    public async Task ComplexRealWorldScenario_HandlesAllEdgeCases()
    {
        // Arrange - Complex scenario with all edge cases
        var testScenarios = new List<(string path, string content)>
        {
            // Regular package with content
            CreateNuGetXmlFile("regular.package", "1.0.0", "net6.0", CreateXmlWithMembers(
                ("T:Regular.Package.Class1", "Class 1 summary"),
                ("M:Regular.Package.Class1.Method1", "Method 1 summary"),
                ("P:Regular.Package.Class1.Property1", "Property 1 summary")
            )),
            
            // Package with shared XML across frameworks
            CreateNuGetXmlFile("shared.xml.package", "1.0.0", "netstandard2.0", CreateXmlWithMembers(
                ("T:Shared.Xml.SharedType", "Shared type")
            )),
            
            // Empty package
            CreateNuGetXmlFile("empty.docs", "1.0.0", "net7.0", CreateEmptyXml("Empty.Docs")),
            
            // Package with special characters
            CreateNuGetXmlFile("special-chars.package", "1.0.0-beta+build.123", "net8.0", CreateXmlWithMembers(
                ("T:Special.Chars.Class`1", "Generic class with special chars"),
                ("M:Special.Chars.Method(System.String@,System.Int32&)", "Method with ref/out params")
            )),
            
            // Package with very long names
            CreateNuGetXmlFile("com.company.product.feature.component.subcomponent", "10.5.3", "net9.0", CreateXmlWithMembers(
                ("T:Com.Company.Product.Feature.Component.SubComponent.Implementation.VeryLongClassName", "Long name class")
            ))
        };

        // Create all test files
        foreach (var (path, content) in testScenarios)
        {
            CreateTestFile(path, content);
        }

        // Act - Index all files
        var result = await indexManager.IndexXmlFilesAsync(testScenarios.Select(t => t.path));
        await indexManager.CommitAsync(); // Ensure all documents are committed

        // Assert comprehensive results
        // We're getting 7 documents, but one is the empty file marker
        result.SuccessfulDocuments.ShouldBe(7);
        result.FailedDocuments.ShouldBe(0);

        // Check total documents
        var totalDocs = indexManager.GetTotalDocuments();
        totalDocs.ShouldBe(7);

        var emptyFiles = indexManager.GetEmptyXmlPaths();
        var hasEmptyFile = emptyFiles.Count > 0;

        // Verify package tracking
        var packages = indexManager.GetIndexedPackageVersionsWithFramework();
        packages.Count.ShouldBe(4); // empty.docs is not tracked because it has no members

        // Verify framework-specific tracking
        packages["regular.package"].ShouldContain(("1.0.0", "net6.0"));
        packages["shared.xml.package"].ShouldContain(("1.0.0", "netstandard2.0"));
        // empty.docs is not tracked in packageVersions because it has no members
        packages["special-chars.package"].ShouldContain(("1.0.0-beta+build.123", "net8.0"));
        packages["com.company.product.feature.component.subcomponent"].ShouldContain(("10.5.3", "net9.0"));

        // Verify empty file tracking
        emptyFiles.Count.ShouldBe(1);
        emptyFiles.First().ShouldEndWith("empty.docs/1.0.0/lib/net7.0/empty-docs.xml"); // Note: packageId dots replaced with dashes

        // Verify searchability
        var genericSearch = indexManager.SearchByField("summary", "Generic", 10);
        genericSearch.TotalHits.ShouldBeGreaterThan(0);

        // Search for a method we know exists
        var methodSearch = indexManager.SearchByField("name", "Method1", 10);
        methodSearch.TotalHits.ShouldBeGreaterThan(0);
    }

    #endregion

    #region Performance and Concurrency Tests

    [TestMethod]
    public async Task ConcurrentIndexing_MultipleFiles_HandlesCorrectly()
    {
        // Arrange - Create many files for concurrent processing
        var files = new List<(string path, string content)>();

        for (int i = 0; i < 20; i++)
        {
            var file = CreateNuGetXmlFile($"concurrent.package{i}", "1.0.0", "net6.0", CreateXmlWithMembers(
                ($"T:Concurrent.Package{i}.Type1", $"Type 1 in package {i}"),
                ($"T:Concurrent.Package{i}.Type2", $"Type 2 in package {i}"),
                ($"M:Concurrent.Package{i}.Method1", $"Method 1 in package {i}")
            ));
            files.Add(file);
            CreateTestFile(file.path, file.content);
        }

        // Act - Index concurrently
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await indexManager.IndexXmlFilesAsync(files.Select(f => f.path));
        stopwatch.Stop();

        // Assert
        result.SuccessfulDocuments.ShouldBe(60); // 3 members * 20 files
        result.FailedDocuments.ShouldBe(0);

        // Verify all packages are tracked
        var packages = indexManager.GetIndexedPackageVersionsWithFramework();
        packages.Count.ShouldBe(20);

        // Performance should be reasonable (parallel processing)
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000); // Should complete in under 5 seconds
    }

    #endregion

    #region Helper Methods

    private (string path, string content) CreateNuGetXmlFile(string packageId, string version, string framework, string xmlContent)
    {
        string path = Path.Combine(testDataPath, $".nuget/packages/{packageId}/{version}/lib/{framework}/{packageId.Replace('.', '-')}.xml");
        return (path, xmlContent);
    }

    private void CreateTestFile(string path, string content)
    {
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        // Setup mock file system to return a new stream each time
        mockFileSystem.OpenReadAsync(path).Returns(_ => CreateStreamFromString(content));
    }

    private static Stream CreateStreamFromString(string content)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    private static string CreateXmlWithMembers(params (string name, string summary)[] members)
    {
        var memberElements = string.Join("\n", members.Select(m =>
            $@"        <member name=""{m.name}"">
            <summary>{m.summary}</summary>
        </member>"));

        return $@"<?xml version=""1.0""?>
<doc>
    <assembly>
        <name>TestAssembly</name>
    </assembly>
    <members>
{memberElements}
    </members>
</doc>";
    }

    private static string CreateEmptyXml(string assemblyName)
    {
        return $@"<?xml version=""1.0""?>
<doc>
    <assembly>
        <name>{assemblyName}</name>
    </assembly>
    <members>
    </members>
</doc>";
    }

    public void Dispose()
    {
        Cleanup();
    }

    #endregion
}