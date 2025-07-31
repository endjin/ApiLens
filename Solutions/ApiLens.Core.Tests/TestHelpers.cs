using ApiLens.Core.Helpers;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Services;
using Lucene.Net.Documents;
using Lucene.Net.Search;

namespace ApiLens.Core.Tests;

/// <summary>
/// Simple file system service implementation for testing.
/// </summary>
internal class SimpleFileSystemService : IFileSystemService
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public IEnumerable<string> GetFiles(string path, string pattern, bool recursive) =>
        Directory.GetFiles(path, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    public string CombinePath(params string[] paths) => Path.Combine(paths);
    public FileInfo GetFileInfo(string path) => new FileInfo(path);
    public DirectoryInfo GetDirectoryInfo(string path) => new DirectoryInfo(path);
    public string GetUserNuGetCachePath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
    public string GetFileName(string path) => Path.GetFileName(path);
    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);
    public IEnumerable<FileInfo> EnumerateFiles(string path, string? pattern = null, bool recursive = false) =>
        new DirectoryInfo(path).EnumerateFiles(pattern ?? "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    public Stream OpenRead(string path) => File.OpenRead(path);
    public Task<Stream> OpenReadAsync(string path) => Task.FromResult<Stream>(File.OpenRead(path));
}

/// <summary>
/// Helper methods for tests to work with the new async batch-based API.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a test instance of LuceneIndexManager with in-memory storage.
    /// </summary>
    public static ILuceneIndexManager CreateTestIndexManager()
    {
        var mockFileSystem = Substitute.For<IFileSystemService>();
        var mockFileHashHelper = Substitute.For<IFileHashHelper>();
        XmlDocumentParser parser = new(mockFileHashHelper, mockFileSystem);
        DocumentBuilder documentBuilder = new();
        string tempPath = Path.Combine(Path.GetTempPath(), $"apilens_test_{Guid.NewGuid()}");
        return new LuceneIndexManager(tempPath, parser, documentBuilder);
    }

    /// <summary>
    /// Creates a test instance of XmlDocumentParser with mock dependencies.
    /// </summary>
    public static XmlDocumentParser CreateTestXmlDocumentParser()
    {
        var mockFileSystem = Substitute.For<IFileSystemService>();
        var mockFileHashHelper = Substitute.For<IFileHashHelper>();
        return new XmlDocumentParser(mockFileHashHelper, mockFileSystem);
    }

    /// <summary>
    /// Creates a test instance of LuceneIndexManager with real file system services for tests that need file access.
    /// </summary>
    public static ILuceneIndexManager CreateTestIndexManagerWithRealFileSystem()
    {
        var realFileSystem = new SimpleFileSystemService();
        var realFileHashHelper = new FileHashHelper(realFileSystem);
        XmlDocumentParser parser = new(realFileHashHelper, realFileSystem);
        DocumentBuilder documentBuilder = new();
        string tempPath = Path.Combine(Path.GetTempPath(), $"apilens_test_{Guid.NewGuid()}");
        return new LuceneIndexManager(tempPath, parser, documentBuilder);
    }

    /// <summary>
    /// Indexes documents by converting them to MemberInfo objects.
    /// </summary>
    public static async Task IndexDocumentsAsync(ILuceneIndexManager manager, params Document[] documents)
    {
        List<MemberInfo> members = documents.Select(ConvertDocumentToMember).ToList();
        await manager.IndexBatchAsync(members);
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

    /// <summary>
    /// Converts a Lucene Document to MemberInfo for indexing.
    /// </summary>
    private static MemberInfo ConvertDocumentToMember(Document document)
    {
        // Extract required fields with defaults
        string id = document.Get("id") ?? $"T:Test.{Guid.NewGuid()}";
        string name = document.Get("name") ?? "TestMember";
        string namespaceName = document.Get("namespace") ?? "TestNamespace";
        string assembly = document.Get("assembly") ?? "TestAssembly";
        string memberTypeStr = document.Get("memberType") ?? "Type";
        string fullName = document.Get("fullName") ?? $"{namespaceName}.{name}";

        if (!Enum.TryParse<MemberType>(memberTypeStr, out MemberType memberType))
        {
            memberType = MemberType.Type;
        }

        return new MemberInfo
        {
            Id = id,
            Name = name,
            FullName = fullName,
            MemberType = memberType,
            Namespace = namespaceName,
            Assembly = assembly,
            Summary = document.Get("summary"),
            Remarks = document.Get("remarks"),
            Returns = document.Get("returns"),
            SeeAlso = document.Get("seeAlso")
        };
    }

    /// <summary>
    /// Creates a Document for testing (to maintain compatibility with existing tests).
    /// </summary>
    public static Document CreateTestDocument(
        string id,
        string name,
        string? content = null,
        string? namespaceName = null,
        string? assembly = null)
    {
        Document doc =
        [
            new StringField("id", id, Field.Store.YES),
            new TextField("name", name, Field.Store.YES),
            new TextField("nameText", name, Field.Store.NO),
            new StringField("memberType", "Type", Field.Store.YES),
            new StringField("namespace", namespaceName ?? "TestNamespace", Field.Store.YES),
            new TextField("namespaceText", namespaceName ?? "TestNamespace", Field.Store.NO),
            new StringField("assembly", assembly ?? "TestAssembly", Field.Store.YES),
            new StringField("fullName", $"{namespaceName ?? "TestNamespace"}.{name}", Field.Store.YES),
            new TextField("fullNameText", $"{namespaceName ?? "TestNamespace"}.{name}", Field.Store.NO)
        ];

        if (!string.IsNullOrEmpty(content))
        {
            doc.Add(new TextField("content", content, Field.Store.YES));
            doc.Add(new TextField("summary", content, Field.Store.YES));
        }

        return doc;
    }

    /// <summary>
    /// Waits for async indexing to complete and commits.
    /// </summary>
    public static async Task CommitAndWaitAsync(ILuceneIndexManager manager)
    {
        await manager.CommitAsync();
        // Small delay to ensure commit is fully processed
        await Task.Delay(100);
    }
}