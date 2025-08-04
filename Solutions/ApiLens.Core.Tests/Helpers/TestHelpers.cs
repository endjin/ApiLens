using ApiLens.Core.Helpers;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Services;
using Lucene.Net.Documents;
using Lucene.Net.Search;

namespace ApiLens.Core.Tests.Helpers;

/// <summary>
/// Helper methods for tests to work with the new async batch-based API.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a test instance of XmlDocumentParser with mock dependencies.
    /// </summary>
    public static XmlDocumentParser CreateTestXmlDocumentParser()
    {
        IFileSystemService? mockFileSystem = Substitute.For<IFileSystemService>();
        IFileHashHelper? mockFileHashHelper = Substitute.For<IFileHashHelper>();
        return new XmlDocumentParser(mockFileHashHelper, mockFileSystem);
    }

    /// <summary>
    /// Creates a test instance of LuceneIndexManager with real file system services for tests that need file access.
    /// </summary>
    public static ILuceneIndexManager CreateTestIndexManagerWithRealFileSystem()
    {
        SimpleFileSystemService realFileSystem = new();
        FileHashHelper realFileHashHelper = new(realFileSystem);
        XmlDocumentParser parser = new(realFileHashHelper, realFileSystem);
        DocumentBuilder documentBuilder = new();
        string tempPath = Path.Combine(Path.GetTempPath(), $"apilens_test_{Guid.NewGuid()}");
        return new LuceneIndexManager(tempPath, parser, documentBuilder);
    }

    /// <summary>
    /// Converts TopDocs to a list of Documents for easier assertions.
    /// </summary>
    public static List<Document> ConvertTopDocsToDocuments(ILuceneIndexManager manager, TopDocs topDocs)
    {
        List<Document> documents = [];
        foreach (ScoreDoc? scoreDoc in topDocs.ScoreDocs)
        {
            Document? doc = manager.GetDocument(scoreDoc.Doc);
            if (doc != null)
            {
                documents.Add(doc);
            }
        }
        return documents;
    }

    /// <summary>
    /// Creates a simple MemberInfo for testing.
    /// </summary>
    public static MemberInfo CreateTestMember(
        string id,
        string name,
        MemberType memberType = MemberType.Type,
        string? namespaceName = null,
        string? assembly = null,
        string? summary = null)
    {
        return new MemberInfo
        {
            Id = id,
            Name = name,
            FullName = $"{namespaceName ?? "TestNamespace"}.{name}",
            MemberType = memberType,
            Namespace = namespaceName ?? "TestNamespace",
            Assembly = assembly ?? "TestAssembly",
            Summary = summary
        };
    }
}