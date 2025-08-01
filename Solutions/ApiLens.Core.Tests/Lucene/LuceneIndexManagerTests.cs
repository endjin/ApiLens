using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Tests.Helpers;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;

namespace ApiLens.Core.Tests.Lucene;

[TestClass]
public class LuceneIndexManagerTests : IDisposable
{
    private ILuceneIndexManager manager = null!;
    private string tempIndexPath = null!;

    [TestInitialize]
    public void Setup()
    {
        tempIndexPath = Path.Combine(Path.GetTempPath(), $"apilens_test_{Guid.NewGuid()}");
        manager = TestHelpers.CreateTestIndexManagerWithRealFileSystem();
    }

    [TestMethod]
    public async Task IndexBatchAsync_WithValidDocuments_AddsToIndex()
    {
        // Arrange
        MemberInfo[] members =
        [
            TestHelpers.CreateTestMember("T:System.String", "String", MemberType.Type, "System"),
            TestHelpers.CreateTestMember("T:System.Int32", "Int32", MemberType.Type, "System")
        ];

        // Act
        IndexingResult result = await manager.IndexBatchAsync(members);

        // Assert
        result.SuccessfulDocuments.ShouldBe(2);
        result.FailedDocuments.ShouldBe(0);
        manager.GetTotalDocuments().ShouldBe(2);
    }

    [TestMethod]
    public async Task SearchByField_WithMatchingQuery_ReturnsDocuments()
    {
        // Arrange
        MemberInfo[] members =
        [
            TestHelpers.CreateTestMember("T:System.String", "String", MemberType.Type, "System", summary: "Represents text"),
            TestHelpers.CreateTestMember("T:System.Int32", "Int32", MemberType.Type, "System", summary: "Represents a 32-bit integer")
        ];
        await manager.IndexBatchAsync(members);
        await manager.CommitAsync();

        // Act
        TopDocs topDocs = manager.SearchByField("nameText", "String", 10);
        List<Document> documents = TestHelpers.ConvertTopDocsToDocuments(manager, topDocs);

        // Assert
        topDocs.TotalHits.ShouldBe(1);
        documents.Count.ShouldBe(1);
        documents[0].Get("name").ShouldBe("String");
    }

    [TestMethod]
    public async Task DeleteDocument_WithValidTerm_RemovesFromIndex()
    {
        // Arrange
        MemberInfo[] members =
        [
            TestHelpers.CreateTestMember("T:System.String", "String"),
            TestHelpers.CreateTestMember("T:System.Int32", "Int32")
        ];
        await manager.IndexBatchAsync(members);
        await manager.CommitAsync();

        // Act
        manager.DeleteDocument(new Term("id", "T:System.String"));
        await manager.CommitAsync();

        // Assert
        manager.GetTotalDocuments().ShouldBe(1);
    }

    [TestMethod]
    public async Task DeleteAll_RemovesAllDocuments()
    {
        // Arrange
        MemberInfo[] members =
        [
            TestHelpers.CreateTestMember("T:System.String", "String"),
            TestHelpers.CreateTestMember("T:System.Int32", "Int32")
        ];
        await manager.IndexBatchAsync(members);
        await manager.CommitAsync();

        // Act
        manager.DeleteAll();
        await manager.CommitAsync();

        // Assert
        manager.GetTotalDocuments().ShouldBe(0);
    }

    [TestMethod]
    public async Task SearchByIntRange_WithValidRange_ReturnsMatchingDocuments()
    {
        // Arrange
        List<MemberInfo> members = [];
        for (int i = 1; i <= 10; i++)
        {
            MemberInfo member = TestHelpers.CreateTestMember($"M:Test.Method{i}", $"Method{i}", MemberType.Method);
            member = member with
            {
                Complexity = new ComplexityMetrics
                {
                    ParameterCount = i,
                    CyclomaticComplexity = i * 2,
                    DocumentationLineCount = 10
                }
            };
            members.Add(member);
        }
        await manager.IndexBatchAsync(members);
        await manager.CommitAsync();

        // Act
        List<Document> documents = manager.SearchByIntRange("parameterCount", 3, 7, 10);

        // Assert
        documents.Count.ShouldBe(5); // Methods 3, 4, 5, 6, 7
    }

    [TestMethod]
    public async Task GetDocument_WithValidDocId_ReturnsDocument()
    {
        // Arrange
        MemberInfo member = TestHelpers.CreateTestMember("T:System.String", "String");
        await manager.IndexBatchAsync([member]);
        await manager.CommitAsync();

        // Search to get doc ID
        TopDocs topDocs = manager.SearchByField("id", "T:System.String", 1);
        topDocs.TotalHits.ShouldBe(1);

        // Act
        Document? document = manager.GetDocument(topDocs.ScoreDocs[0].Doc);

        // Assert
        document.ShouldNotBeNull();
        document.Get("name").ShouldBe("String");
    }

    [TestMethod]
    public async Task GetIndexStatistics_ReturnsValidStatistics()
    {
        // Arrange
        MemberInfo[] members =
        [
            TestHelpers.CreateTestMember("T:System.String", "String"),
            TestHelpers.CreateTestMember("T:System.Int32", "Int32")
        ];
        await manager.IndexBatchAsync(members);
        await manager.CommitAsync();

        // Act
        IndexStatistics? stats = manager.GetIndexStatistics();

        // Assert
        stats.ShouldNotBeNull();
        stats.DocumentCount.ShouldBe(2);
        stats.TotalSizeInBytes.ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public async Task SearchWithQuery_WithComplexQuery_ReturnsMatchingDocuments()
    {
        // Arrange
        MemberInfo[] members =
        [
            TestHelpers.CreateTestMember("T:System.String", "String", MemberType.Type, "System"),
            TestHelpers.CreateTestMember("T:System.Text.StringBuilder", "StringBuilder", MemberType.Type, "System.Text")
        ];
        await manager.IndexBatchAsync(members);
        await manager.CommitAsync();

        // Act
        QueryParser parser = new(global::Lucene.Net.Util.LuceneVersion.LUCENE_48, "nameText", new WhitespaceAnalyzer(global::Lucene.Net.Util.LuceneVersion.LUCENE_48))
        {
            LowercaseExpandedTerms = false // Don't lowercase wildcard terms
        };
        Query? query = parser.Parse("String*");
        TopDocs topDocs = manager.SearchWithQuery(query, 10);

        // Assert
        topDocs.TotalHits.ShouldBe(2); // Both String and StringBuilder match
    }

    [TestMethod]
    public async Task IndexXmlFilesAsync_WithValidFiles_IndexesAllDocuments()
    {
        // Arrange
        string testFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");
        string xmlContent = """
                            <?xml version="1.0"?>
                            <doc>
                                <assembly>
                                    <name>TestAssembly</name>
                                </assembly>
                                <members>
                                    <member name="T:TestNamespace.TestClass">
                                        <summary>Test class summary</summary>
                                    </member>
                                    <member name="M:TestNamespace.TestClass.TestMethod">
                                        <summary>Test method summary</summary>
                                    </member>
                                </members>
                            </doc>
                            """;
        await File.WriteAllTextAsync(testFile, xmlContent);

        try
        {
            // Act
            IndexingResult result = await manager.IndexXmlFilesAsync([testFile]);

            // Assert
            result.SuccessfulDocuments.ShouldBe(2);
            result.FailedDocuments.ShouldBe(0);
            result.TotalDocuments.ShouldBe(2);
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [TestMethod]
    public void GetPerformanceMetrics_ReturnsValidMetrics()
    {
        // Act
        PerformanceMetrics metrics = manager.GetPerformanceMetrics();

        // Assert
        metrics.ShouldNotBeNull();
        metrics.Gen0Collections.ShouldBeGreaterThanOrEqualTo(0);
        metrics.PeakWorkingSetBytes.ShouldBeGreaterThan(0);
    }

    [TestCleanup]
    public void Cleanup()
    {
        manager?.Dispose();

        if (!string.IsNullOrEmpty(tempIndexPath) && Directory.Exists(tempIndexPath))
        {
            try
            {
                Directory.Delete(tempIndexPath, true);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // Ignore cleanup errors
            }
        }
    }

    public void Dispose()
    {
        Cleanup();
    }
}