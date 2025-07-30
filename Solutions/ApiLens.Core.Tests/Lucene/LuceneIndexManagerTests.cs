using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;

namespace ApiLens.Core.Tests.Lucene;

[TestClass]
public class LuceneIndexManagerTests : IDisposable
{
    private LuceneIndexManager manager = null!;
    private RAMDirectory directory = null!;

    [TestInitialize]
    public void Setup()
    {
        directory = new RAMDirectory();
        manager = new LuceneIndexManager(directory);
    }

    [TestMethod]
    public void AddDocument_WithValidDocument_AddsToIndex()
    {
        // Arrange
        Document doc =
        [
            new StringField("id", "T:System.String", Field.Store.YES),
            new TextField("name", "String", Field.Store.YES),
            new TextField("content", "System.String represents text", Field.Store.YES)
        ];

        // Act
        manager.AddDocument(doc);
        manager.Commit();

        // Assert
        using DirectoryReader reader = manager.OpenReader();
        reader.NumDocs.ShouldBe(1);
    }

    [TestMethod]
    public void SearchByField_WithMatchingQuery_ReturnsDocument()
    {
        // Arrange
        Document doc =
        [
            new StringField("id", "T:System.String", Field.Store.YES),
            new TextField("name", "String", Field.Store.YES),
            new StringField("namespace", "System", Field.Store.YES),
            new TextField("namespaceText", "System", Field.Store.NO)
        ];
        manager.AddDocument(doc);
        manager.Commit();

        // Act
        List<Document> results = manager.SearchByField("namespaceText", "System", 10);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Get("id").ShouldBe("T:System.String");
    }

    [TestMethod]
    public void UpdateDocument_WithExistingDocument_UpdatesInIndex()
    {
        // Arrange
        Document doc1 =
        [
            new StringField("id", "T:System.String", Field.Store.YES),
            new TextField("summary", "Old summary", Field.Store.YES)
        ];
        manager.AddDocument(doc1);
        manager.Commit();

        Document doc2 =
        [
            new StringField("id", "T:System.String", Field.Store.YES),
            new TextField("summary", "New summary", Field.Store.YES)
        ];

        // Act
        manager.UpdateDocument(new Term("id", "T:System.String"), doc2);
        manager.Commit();

        // Assert
        List<Document> results = manager.SearchByField("id", "T:System.String", 10);
        results.Count.ShouldBe(1);
        results[0].Get("summary").ShouldBe("New summary");
    }

    [TestMethod]
    public void DeleteDocument_WithExistingDocument_RemovesFromIndex()
    {
        // Arrange
        Document doc =
        [
            new StringField("id", "T:System.String", Field.Store.YES),
            new TextField("name", "String", Field.Store.YES)
        ];
        manager.AddDocument(doc);
        manager.Commit();

        // Act
        manager.DeleteDocument(new Term("id", "T:System.String"));
        manager.Commit();

        // Assert
        using DirectoryReader reader = manager.OpenReader();
        reader.NumDocs.ShouldBe(0);
    }

    [TestMethod]
    public void SearchByField_WithComplexQuery_ReturnsMatchingDocuments()
    {
        // Arrange
        Document[] docs =
        [
            [
                new StringField("id", "T:System.Collections.Generic.List`1", Field.Store.YES),
                new TextField("fullName", "System.Collections.Generic.List`1", Field.Store.YES)
            ],
            [
                new StringField("id", "T:System.Collections.Generic.Dictionary`2", Field.Store.YES),
                new TextField("fullName", "System.Collections.Generic.Dictionary`2", Field.Store.YES)
            ],
            [
                new StringField("id", "T:System.String", Field.Store.YES),
                new TextField("fullName", "System.String", Field.Store.YES)
            ]
        ];

        foreach (Document doc in docs)
        {
            manager.AddDocument(doc);
        }
        manager.Commit();

        // Act
        List<Document> results = manager.SearchByField("fullName", "collections.generic", 10);

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldAllBe(r => r.Get("fullName").Contains("Collections.Generic"));
    }

    [TestMethod]
    public void Dispose_WhenCalled_DisposesResources()
    {
        // Arrange
        LuceneIndexManager tempManager = new(new RAMDirectory());
        Document doc = [new StringField("id", "test", Field.Store.YES)];
        tempManager.AddDocument(doc);

        // Act
        tempManager.Dispose();

        // Assert
        // Calling Dispose again should not throw
        tempManager.Dispose();
    }

    [TestMethod]
    public void DeleteAll_RemovesAllDocuments()
    {
        // Arrange
        Document[] docs =
        [
            [new StringField("id", "doc1", Field.Store.YES)],
            [new StringField("id", "doc2", Field.Store.YES)],
            [new StringField("id", "doc3", Field.Store.YES)]
        ];

        foreach (Document doc in docs)
        {
            manager.AddDocument(doc);
        }
        manager.Commit();

        // Act
        manager.DeleteAll();
        manager.Commit();

        // Assert
        using DirectoryReader reader = manager.OpenReader();
        reader.NumDocs.ShouldBe(0);
    }

    [TestMethod]
    public void SearchByIntRange_WithMatchingDocuments_ReturnsResults()
    {
        // Arrange
        Document[] docs =
        [
            [
                new StringField("id", "method1", Field.Store.YES),
                new Int32Field("complexity", 5, Field.Store.YES)
            ],
            [
                new StringField("id", "method2", Field.Store.YES),
                new Int32Field("complexity", 15, Field.Store.YES)
            ],
            [
                new StringField("id", "method3", Field.Store.YES),
                new Int32Field("complexity", 25, Field.Store.YES)
            ]
        ];

        foreach (Document doc in docs)
        {
            manager.AddDocument(doc);
        }
        manager.Commit();

        // Act
        List<Document> results = manager.SearchByIntRange("complexity", 10, 20, 10);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Get("id").ShouldBe("method2");
    }

    [TestMethod]
    public void SearchByFieldExists_WithExistingField_ReturnsDocuments()
    {
        // Arrange
        Document[] docs =
        [
            [
                new StringField("id", "doc1", Field.Store.YES),
                new StringField("hasExamples", "true", Field.Store.YES)
            ],
            [
                new StringField("id", "doc2", Field.Store.YES)
            ],
            [
                new StringField("id", "doc3", Field.Store.YES),
                new StringField("hasExamples", "false", Field.Store.YES)
            ]
        ];

        foreach (Document doc in docs)
        {
            manager.AddDocument(doc);
        }
        manager.Commit();

        // Act
        List<Document> results = manager.SearchByFieldExists("hasExamples", 10);

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldContain(d => d.Get("id") == "doc1");
        results.ShouldContain(d => d.Get("id") == "doc3");
    }

    [TestMethod]
    public void GetIndexStatistics_WithDocuments_ReturnsStats()
    {
        // Arrange
        Document[] docs =
        [
            [
                new StringField("id", "doc1", Field.Store.YES),
                new TextField("content", "sample content", Field.Store.YES)
            ],
            [
                new StringField("id", "doc2", Field.Store.YES),
                new TextField("content", "more content", Field.Store.YES),
                new StringField("type", "class", Field.Store.YES)
            ]
        ];

        foreach (Document doc in docs)
        {
            manager.AddDocument(doc);
        }
        manager.Commit();

        // Act
        IndexStatistics? stats = manager.GetIndexStatistics();

        // Assert
        stats.ShouldNotBeNull();
        stats.DocumentCount.ShouldBe(2);
        stats.FieldCount.ShouldBeGreaterThan(0);
        stats.IndexPath.ShouldNotBeNull();
    }

    [TestMethod]
    public void GetIndexStatistics_WithEmptyIndex_ReturnsZeroStats()
    {
        // Act
        IndexStatistics? stats = manager.GetIndexStatistics();

        // Assert
        stats.ShouldNotBeNull();
        stats.DocumentCount.ShouldBe(0);
        stats.IndexPath.ShouldNotBeNull();
    }

    [TestMethod]
    public void SearchByIntRange_WithInvalidFieldName_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => manager.SearchByIntRange("", 0, 10, 10));
        Should.Throw<ArgumentException>(() => manager.SearchByIntRange(null!, 0, 10, 10));
    }

    [TestMethod]
    public void SearchByIntRange_WithInvalidMaxResults_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => manager.SearchByIntRange("field", 0, 10, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => manager.SearchByIntRange("field", 0, 10, -1));
    }

    [TestMethod]
    public void SearchByFieldExists_WithInvalidParameters_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => manager.SearchByFieldExists("", 10));
        Should.Throw<ArgumentException>(() => manager.SearchByFieldExists(null!, 10));
        Should.Throw<ArgumentOutOfRangeException>(() => manager.SearchByFieldExists("field", 0));
        Should.Throw<ArgumentOutOfRangeException>(() => manager.SearchByFieldExists("field", -1));
    }

    public void Dispose()
    {
        manager?.Dispose();
        directory?.Dispose();
    }
}