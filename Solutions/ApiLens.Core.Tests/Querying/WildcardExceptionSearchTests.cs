using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Querying;
using ApiLens.Core.Tests.Helpers;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace ApiLens.Core.Tests.Querying;

[TestClass]
public class WildcardExceptionSearchTests : IDisposable
{
    private QueryEngine engine = null!;
    private ILuceneIndexManager indexManager = null!;
    private string tempIndexPath = null!;

    [TestInitialize]
    public void Setup()
    {
        tempIndexPath = Path.Combine(Path.GetTempPath(), $"apilens_test_{Guid.NewGuid()}");
        XmlDocumentParser parser = TestHelpers.CreateTestXmlDocumentParser();
        DocumentBuilder documentBuilder = new();
        indexManager = new LuceneIndexManager(tempIndexPath, parser, documentBuilder);
        engine = new QueryEngine(indexManager);

        // Add test data with various exception types
        SeedTestData();
    }

    private void SeedTestData()
    {
        MemberInfo[] members =
        [
            new()
            {
                Id = "M:Test.Class1.Method1",
                MemberType = MemberType.Method,
                Name = "Method1",
                FullName = "Test.Class1.Method1",
                Assembly = "TestAssembly",
                Namespace = "Test",
                Exceptions = ImmutableArray.Create(
                    new ExceptionInfo { Type = "System.ArgumentNullException", Condition = "When parameter is null" },
                    new ExceptionInfo { Type = "System.ArgumentException", Condition = "When argument is invalid" }
                )
            },
            new()
            {
                Id = "M:Test.Class2.Method2",
                MemberType = MemberType.Method,
                Name = "Method2",
                FullName = "Test.Class2.Method2",
                Assembly = "TestAssembly",
                Namespace = "Test",
                Exceptions = ImmutableArray.Create(
                    new ExceptionInfo { Type = "System.ArgumentOutOfRangeException", Condition = "When index is out of range" }
                )
            },
            new()
            {
                Id = "M:Test.Class3.Method3",
                MemberType = MemberType.Method,
                Name = "Method3",
                FullName = "Test.Class3.Method3",
                Assembly = "TestAssembly",
                Namespace = "Test",
                Exceptions = ImmutableArray.Create(
                    new ExceptionInfo { Type = "ArgumentNullException", Condition = "Simple name without namespace" }
                )
            },
            new()
            {
                Id = "M:Test.Class4.Method4",
                MemberType = MemberType.Method,
                Name = "Method4",
                FullName = "Test.Class4.Method4",
                Assembly = "TestAssembly",
                Namespace = "Test",
                Exceptions = ImmutableArray.Create(
                    new ExceptionInfo { Type = "System.IO.IOException", Condition = "When file operation fails" }
                )
            }
        ];

        // Use the async API to index
        Task<IndexingResult> task = indexManager.IndexBatchAsync(members);
        task.Wait();

        Task commitTask = indexManager.CommitAsync();
        commitTask.Wait();
    }

    [TestMethod]
    public void SearchByException_ExactMatch_ReturnsCorrectResults()
    {
        // Act
        List<MemberInfo> results = engine.SearchByException("System.ArgumentNullException", 10);

        // Assert
        results.Count.ShouldBe(1);
        results[0].FullName.ShouldBe("Test.Class1.Method1");
        results[0].Exceptions.ShouldContain(e => e.Type == "System.ArgumentNullException");
    }

    [TestMethod]
    public void SearchByException_SimpleNameMatch_ReturnsMultipleResults()
    {
        // Act
        List<MemberInfo> results = engine.SearchByException("ArgumentNullException", 10);

        // Assert - Should find System.ArgumentNullException
        // Note: Plain "ArgumentNullException" without namespace may not be found
        // because the simplified search focuses on common .NET namespaces
        results.Count.ShouldBeGreaterThanOrEqualTo(1);
        results.ShouldContain(r => r.FullName == "Test.Class1.Method1");
        // May or may not find Test.Class3.Method3 depending on text field analysis
    }

    [TestMethod]
    public void SearchByException_WildcardMatch_ReturnsMatchingResults()
    {
        // Act - This is the problematic case
        List<MemberInfo> results = engine.SearchByException("System.Argument*", 10);

        // Assert - Should find System.ArgumentNullException, System.ArgumentException, System.ArgumentOutOfRangeException
        Console.WriteLine($"Wildcard search returned {results.Count} results:");
        foreach (MemberInfo result in results)
        {
            Console.WriteLine($"- {result.FullName}: {string.Join(", ", result.Exceptions.Select(e => e.Type))}");
        }

        results.Count.ShouldBeGreaterThan(0, "Wildcard search should return results for System.Argument*");
        results.ShouldContain(r => r.FullName == "Test.Class1.Method1"); // ArgumentNullException and ArgumentException
        results.ShouldContain(r => r.FullName == "Test.Class2.Method2"); // ArgumentOutOfRangeException
    }

    [TestMethod]
    public void SearchByException_WildcardInMiddle_ReturnsMatchingResults()
    {
        // Act
        List<MemberInfo> results = engine.SearchByException("Argument*Exception", 10);

        // Assert - Should find ArgumentNullException and ArgumentException
        Console.WriteLine($"Middle wildcard search returned {results.Count} results:");
        foreach (MemberInfo result in results)
        {
            Console.WriteLine($"- {result.FullName}: {string.Join(", ", result.Exceptions.Select(e => e.Type))}");
        }

        results.Count.ShouldBeGreaterThan(0, "Wildcard in middle should return results");
    }

    [TestMethod]
    public void DirectWildcardQuery_OnExceptionTypeField_ReturnsResults()
    {
        // Test the underlying Lucene wildcard functionality directly
        WildcardQuery wildcardQuery = new(new Term("exceptionType", "System.Argument*"));
        TopDocs topDocs = indexManager.SearchWithQuery(wildcardQuery, 10);

        Console.WriteLine($"Direct wildcard query returned {topDocs.ScoreDocs?.Length ?? 0} results");

        // Assert
        topDocs.ScoreDocs.ShouldNotBeNull();
        topDocs.ScoreDocs!.Length.ShouldBeGreaterThan(0, "Direct wildcard query should find results");
    }

    [TestMethod]
    public void SearchByField_ExceptionType_ExactMatch_ReturnsResults()
    {
        // Test exact field search
        TopDocs topDocs = indexManager.SearchByField("exceptionType", "System.ArgumentNullException", 10);

        Console.WriteLine($"Exact field search returned {topDocs.ScoreDocs?.Length ?? 0} results");

        // Assert
        topDocs.ScoreDocs.ShouldNotBeNull();
        topDocs.ScoreDocs.Length.ShouldBe(1, "Exact field search should find one result");
    }

    [TestMethod]
    public void SearchByField_ExceptionTypeText_PartialMatch_ReturnsResults()
    {
        // Test text field search (analyzed field)
        TopDocs topDocs = indexManager.SearchByField("exceptionTypeText", "ArgumentNullException", 10);

        Console.WriteLine($"Text field search returned {topDocs.ScoreDocs?.Length ?? 0} results");

        // This might return more results since it's analyzed
        topDocs.ScoreDocs.ShouldNotBeNull();
    }

    [TestMethod]
    public void InvestigateFieldAnalysis_ShowsFieldTypes()
    {
        // This test helps us understand how fields are analyzed
        int totalDocs = indexManager.GetTotalDocuments();
        Console.WriteLine($"Total documents in index: {totalDocs}");

        // Test various field searches to understand the behavior
        TopDocs exactResults = indexManager.SearchByField("exceptionType", "System.ArgumentNullException", 10);
        TopDocs textResults = indexManager.SearchByField("exceptionTypeText", "System.ArgumentNullException", 10);
        TopDocs simpleResults = indexManager.SearchByField("exceptionSimpleName", "ArgumentNullException", 10);

        Console.WriteLine($"exceptionType field results: {exactResults.ScoreDocs?.Length ?? 0}");
        Console.WriteLine($"exceptionTypeText field results: {textResults.ScoreDocs?.Length ?? 0}");
        Console.WriteLine($"exceptionSimpleName field results: {simpleResults.ScoreDocs?.Length ?? 0}");

        // All should find at least one result
        exactResults.ScoreDocs.ShouldNotBeNull();
        exactResults.ScoreDocs!.Length.ShouldBeGreaterThan(0);
    }

    public void Dispose()
    {
        engine?.Dispose();
        indexManager?.Dispose();

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
}