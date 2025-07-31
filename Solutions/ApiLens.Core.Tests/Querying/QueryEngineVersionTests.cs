using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;
using Lucene.Net.Documents;
using Lucene.Net.Search;

namespace ApiLens.Core.Tests.Querying;

[TestClass]
public class QueryEngineVersionTests : IDisposable
{
    private ILuceneIndexManager mockIndexManager = null!;
    private QueryEngine queryEngine = null!;

    private static TopDocs CreateTopDocsWithDocument(Document doc)
    {
        // Create a mock TopDocs with one document
        ScoreDoc[] scoreDocs = [new(0, 1.0f)];
        // TopDocs constructor: (int totalHits, ScoreDoc[] scoreDocs, float maxScore)
        return new TopDocs(1, scoreDocs, 1.0f);
    }

    [TestInitialize]
    public void Setup()
    {
        mockIndexManager = Substitute.For<ILuceneIndexManager>();
        queryEngine = new QueryEngine(mockIndexManager);
    }

    [TestMethod]
    public void SearchByName_WithVersionFields_ReturnsVersionInfo()
    {
        // Arrange
        Document doc =
        [
            new StringField("id", "M:System.String.Concat", Field.Store.YES),
            new StringField("memberType", "Method", Field.Store.YES),
            new StringField("name", "Concat", Field.Store.YES),
            new StringField("fullName", "System.String.Concat", Field.Store.YES),
            new StringField("assembly", "System.Runtime", Field.Store.YES),
            new StringField("namespace", "System", Field.Store.YES),
            new StringField("packageId", "System.Runtime", Field.Store.YES),
            new StringField("packageVersion", "8.0.0", Field.Store.YES),
            new StringField("targetFramework", "net8.0", Field.Store.YES),
            new StringField("isFromNuGetCache", "true", Field.Store.YES),
            new StringField("sourceFilePath",
                "/home/user/.nuget/packages/system.runtime/8.0.0/lib/net8.0/System.Runtime.xml", Field.Store.YES)
        ];

        TopDocs topDocs = CreateTopDocsWithDocument(doc);
        mockIndexManager.SearchByField("nameText", "Concat", 10).Returns(topDocs);
        mockIndexManager.GetDocument(0).Returns(doc);

        // Act
        List<MemberInfo> results = queryEngine.SearchByName("Concat", 10);

        // Assert
        results.Count.ShouldBe(1);
        MemberInfo member = results[0];
        member.PackageId.ShouldBe("System.Runtime");
        member.PackageVersion.ShouldBe("8.0.0");
        member.TargetFramework.ShouldBe("net8.0");
        member.IsFromNuGetCache.ShouldBeTrue();
        member.SourceFilePath.ShouldBe("/home/user/.nuget/packages/system.runtime/8.0.0/lib/net8.0/System.Runtime.xml");
    }

    [TestMethod]
    public void SearchByName_WithoutVersionFields_ReturnsNullVersionInfo()
    {
        // Arrange
        Document doc =
        [
            new StringField("id", "M:System.String.Concat", Field.Store.YES),
            new StringField("memberType", "Method", Field.Store.YES),
            new StringField("name", "Concat", Field.Store.YES),
            new StringField("fullName", "System.String.Concat", Field.Store.YES),
            new StringField("assembly", "System.Runtime", Field.Store.YES),
            new StringField("namespace", "System", Field.Store.YES)
            // No version fields
        ];
        // No version fields

        TopDocs topDocs = CreateTopDocsWithDocument(doc);
        mockIndexManager.SearchByField("nameText", "Concat", 10).Returns(topDocs);
        mockIndexManager.GetDocument(0).Returns(doc);

        // Act
        List<MemberInfo> results = queryEngine.SearchByName("Concat", 10);

        // Assert
        results.Count.ShouldBe(1);
        MemberInfo member = results[0];
        member.PackageId.ShouldBeNull();
        member.PackageVersion.ShouldBeNull();
        member.TargetFramework.ShouldBeNull();
        member.IsFromNuGetCache.ShouldBeFalse();
        member.SourceFilePath.ShouldBeNull();
    }

    [TestMethod]
    public void SearchByName_WithEmptyVersionFields_ReturnsNullVersionInfo()
    {
        // Arrange
        Document doc =
        [
            new StringField("id", "M:System.String.Concat", Field.Store.YES),
            new StringField("memberType", "Method", Field.Store.YES),
            new StringField("name", "Concat", Field.Store.YES),
            new StringField("fullName", "System.String.Concat", Field.Store.YES),
            new StringField("assembly", "System.Runtime", Field.Store.YES),
            new StringField("namespace", "System", Field.Store.YES),
            new StringField("packageId", "", Field.Store.YES),
            new StringField("packageVersion", "", Field.Store.YES),
            new StringField("targetFramework", "", Field.Store.YES),
            new StringField("isFromNuGetCache", "false", Field.Store.YES),
            new StringField("sourceFilePath", "", Field.Store.YES)
        ];

        TopDocs topDocs = CreateTopDocsWithDocument(doc);
        mockIndexManager.SearchByField("nameText", "Concat", 10).Returns(topDocs);
        mockIndexManager.GetDocument(0).Returns(doc);

        // Act
        List<MemberInfo> results = queryEngine.SearchByName("Concat", 10);

        // Assert
        results.Count.ShouldBe(1);
        MemberInfo member = results[0];
        member.PackageId.ShouldBeNull();
        member.PackageVersion.ShouldBeNull();
        member.TargetFramework.ShouldBeNull();
        member.IsFromNuGetCache.ShouldBeFalse();
        member.SourceFilePath.ShouldBeNull();
    }

    public void Dispose()
    {
        queryEngine?.Dispose();
    }
}