using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;
using Lucene.Net.Documents;
using Lucene.Net.Store;

namespace ApiLens.Core.Tests.Querying;

[TestClass]
public class QueryEngineTests : IDisposable
{
    private QueryEngine engine = null!;
    private LuceneIndexManager indexManager = null!;
    private DocumentBuilder documentBuilder = null!;
    private RAMDirectory directory = null!;

    [TestInitialize]
    public void Setup()
    {
        directory = new RAMDirectory();
        indexManager = new LuceneIndexManager(directory);
        documentBuilder = new DocumentBuilder();
        engine = new QueryEngine(indexManager);

        // Add test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        MemberInfo[] members =
        [
            new()
            {
                Id = "T:System.String",
                MemberType = MemberType.Type,
                Name = "String",
                FullName = "System.String",
                Assembly = "System.Runtime",
                Namespace = "System",
                Summary = "Represents text as a sequence of UTF-16 code units."
            },
            new()
            {
                Id = "T:System.Collections.Generic.List`1",
                MemberType = MemberType.Type,
                Name = "List`1",
                FullName = "System.Collections.Generic.List`1",
                Assembly = "System.Collections",
                Namespace = "System.Collections.Generic",
                Summary = "Represents a strongly typed list of objects."
            },
            new()
            {
                Id = "M:System.String.Split(System.Char)",
                MemberType = MemberType.Method,
                Name = "Split",
                FullName = "System.String.Split(System.Char)",
                Assembly = "System.Runtime",
                Namespace = "System",
                Summary = "Splits a string into substrings."
            }
        ];

        foreach (MemberInfo member in members)
        {
            Document doc = documentBuilder.BuildDocument(member);
            indexManager.AddDocument(doc);
        }

        indexManager.Commit();
    }

    [TestMethod]
    public void SearchByName_WithExactMatch_ReturnsMember()
    {
        // Act
        List<MemberInfo> results = engine.SearchByName("String", 10);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("String");
        results[0].Id.ShouldBe("T:System.String");
    }

    [TestMethod]
    public void SearchByContent_WithKeyword_ReturnsMatchingMembers()
    {
        // Act - search for a simpler term
        List<MemberInfo> results = engine.SearchByContent("list`1", 10);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("List`1");
    }

    [TestMethod]
    public void SearchByNamespace_WithPartialMatch_ReturnsMatchingMembers()
    {
        // Act
        List<MemberInfo> results = engine.SearchByNamespace("System", 10);

        // Assert
        results.Count.ShouldBeGreaterThan(0);
        results.ShouldAllBe(m => m.Namespace.StartsWith("System"));
    }

    [TestMethod]
    public void GetById_WithExistingId_ReturnsMember()
    {
        // Act
        MemberInfo? result = engine.GetById("T:System.String");

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("String");
        result.FullName.ShouldBe("System.String");
    }

    [TestMethod]
    public void GetById_WithNonExistingId_ReturnsNull()
    {
        // Act
        MemberInfo? result = engine.GetById("T:NonExistent.Type");

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void GetByType_WithTypeFilter_ReturnsOnlyTypes()
    {
        // Act
        List<MemberInfo> results = engine.GetByType(MemberType.Type, 10);

        // Assert
        results.Count.ShouldBe(2); // String and List`1
        results.ShouldAllBe(m => m.MemberType == MemberType.Type);
    }

    [TestMethod]
    public void GetByType_WithMethodFilter_ReturnsOnlyMethods()
    {
        // Act
        List<MemberInfo> results = engine.GetByType(MemberType.Method, 10);

        // Assert
        results.Count.ShouldBe(1); // Split method
        results[0].Name.ShouldBe("Split");
        results[0].MemberType.ShouldBe(MemberType.Method);
    }

    [TestMethod]
    public void SearchByAssembly_WithMatchingAssembly_ReturnsMembers()
    {
        // Act
        List<MemberInfo> results = engine.SearchByAssembly("System.Runtime", 10);

        // Assert
        results.Count.ShouldBe(2); // String type and Split method
        results.ShouldAllBe(m => m.Assembly == "System.Runtime");
    }

    public void Dispose()
    {
        engine?.Dispose();
        indexManager?.Dispose();
        directory?.Dispose();
    }
}