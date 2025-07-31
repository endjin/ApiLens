using ApiLens.Cli.Commands;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Querying;
using Lucene.Net.Documents;
using Spectre.Console;
using Spectre.Console.Testing;
using static ApiLens.Cli.Commands.QueryCommand;

namespace ApiLens.Cli.Tests.Commands;

[TestClass]
public class QueryCommandVersionTests
{
    private string indexPath = null!;
    private QueryCommand command = null!;
    private ILuceneIndexManagerFactory indexManagerFactory = null!;
    private IQueryEngineFactory queryEngineFactory = null!;

    [TestInitialize]
    public void Setup()
    {
        indexPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(indexPath);

        indexManagerFactory = Substitute.For<ILuceneIndexManagerFactory>();
        queryEngineFactory = Substitute.For<IQueryEngineFactory>();

        // For these integration tests, we'll use real implementations
        indexManagerFactory.Create(Arg.Any<string>()).Returns(callInfo =>
        {
            XmlDocumentParser parser = new();
            DocumentBuilder documentBuilder = new();
            return new LuceneIndexManager(callInfo.Arg<string>(), parser, documentBuilder);
        });
        queryEngineFactory.Create(Arg.Any<ILuceneIndexManager>()).Returns(callInfo => new QueryEngine(callInfo.Arg<ILuceneIndexManager>()));

        command = new QueryCommand(indexManagerFactory, queryEngineFactory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(indexPath))
        {
            Directory.Delete(indexPath, true);
        }
    }

    [TestMethod]
    public void Execute_WithVersionInfo_DisplaysVersionInTable()
    {
        // Arrange
        CreateIndexWithVersionInfo();

        Settings settings = new()
        {
            Query = "TestClass",
            IndexPath = indexPath,
            QueryType = QueryType.Name,
            Format = OutputFormat.Table
        };

        TestConsole console = new();
        AnsiConsole.Console = console;

        // Act
        int result = command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        string output = console.Output;
        output.ShouldContain("TestClass");
        output.ShouldContain("Version");
        output.ShouldContain("1.0.0");
        output.ShouldContain("net6.0");
    }

    [TestMethod]
    public void Execute_WithVersionInfo_IncludesVersionInJson()
    {
        // Arrange
        CreateIndexWithVersionInfo();

        Settings settings = new()
        {
            Query = "TestClass",
            IndexPath = indexPath,
            QueryType = QueryType.Name,
            Format = OutputFormat.Json
        };

        TestConsole console = new();
        AnsiConsole.Console = console;

        // Act
        int result = command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        string output = console.Output;
        output.ShouldContain("TestClass");
        output.ShouldContain("\"packageId\": \"TestPackage\"");
        output.ShouldContain("\"packageVersion\": \"1.0.0\"");
        output.ShouldContain("\"targetFramework\": \"net6.0\"");
        output.ShouldContain("\"isFromNuGetCache\": true");
    }

    [TestMethod]
    public void Execute_WithoutVersionInfo_HandlesGracefully()
    {
        // Arrange
        CreateIndexWithoutVersionInfo();

        Settings settings = new()
        {
            Query = "OldClass",
            IndexPath = indexPath,
            QueryType = QueryType.Name,
            Format = OutputFormat.Table
        };

        TestConsole console = new();
        AnsiConsole.Console = console;

        // Act
        int result = command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        string output = console.Output;
        output.ShouldContain("OldClass");
        output.ShouldContain("N/A"); // Should show N/A for missing version info
    }

    [TestMethod]
    public void Execute_WithVersionInfo_IncludesVersionInMarkdown()
    {
        // Arrange
        CreateIndexWithVersionInfo();

        Settings settings = new()
        {
            Query = "TestClass",
            IndexPath = indexPath,
            QueryType = QueryType.Name,
            Format = OutputFormat.Markdown
        };

        TestConsole console = new();
        AnsiConsole.Console = console;

        // Act
        int result = command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        string output = console.Output;
        output.ShouldContain("### Version Information");
        output.ShouldContain("- **Package**: TestPackage v1.0.0");
        output.ShouldContain("- **Framework**: net6.0");
        output.ShouldContain("- **Source**: NuGet Cache");
    }

    private void CreateIndexWithVersionInfo()
    {
        XmlDocumentParser parser = new();
        DocumentBuilder documentBuilder = new();
        using LuceneIndexManager indexManager = new(indexPath, parser, documentBuilder);

        MemberInfo member = new()
        {
            Id = "T:TestPackage.TestClass",
            Name = "TestClass",
            FullName = "TestPackage.TestClass",
            Namespace = "TestPackage",
            Assembly = "TestPackage",
            MemberType = MemberType.Type,
            Summary = "A test class with version info",
            // Version tracking fields
            PackageId = "TestPackage",
            PackageVersion = "1.0.0",
            TargetFramework = "net6.0",
            IsFromNuGetCache = true,
            SourceFilePath = "/home/user/.nuget/packages/testpackage/1.0.0/lib/net6.0/TestPackage.xml"
        };

        Document doc = documentBuilder.BuildDocument(member);
        MemberInfo[] members = [member];
        indexManager.IndexBatchAsync(members).GetAwaiter().GetResult();
        indexManager.CommitAsync().GetAwaiter().GetResult();
    }

    private void CreateIndexWithoutVersionInfo()
    {
        XmlDocumentParser parser = new();
        DocumentBuilder documentBuilder = new();
        using LuceneIndexManager indexManager = new(indexPath, parser, documentBuilder);

        MemberInfo member = new()
        {
            Id = "T:OldAssembly.OldClass",
            Name = "OldClass",
            FullName = "OldAssembly.OldClass",
            Namespace = "OldAssembly",
            Assembly = "OldAssembly",
            MemberType = MemberType.Type,
            Summary = "An old class without version info"
            // No version fields set
        };

        Document doc = documentBuilder.BuildDocument(member);
        MemberInfo[] members = [member];
        indexManager.IndexBatchAsync(members).GetAwaiter().GetResult();
        indexManager.CommitAsync().GetAwaiter().GetResult();
    }
}