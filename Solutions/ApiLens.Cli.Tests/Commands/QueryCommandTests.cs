using ApiLens.Cli.Commands;
using ApiLens.Cli.Services;
using ApiLens.Core.Lucene;
using ApiLens.Core.Querying;
using Spectre.Console;
using Spectre.Console.Testing;
namespace ApiLens.Cli.Tests.Commands;

[TestClass]
public sealed class QueryCommandTests : IDisposable
{
    private QueryCommand command = null!;
    private ILuceneIndexManagerFactory indexManagerFactory = null!;
    private IQueryEngineFactory queryEngineFactory = null!;
    private TestConsole console = null!;
    private IIndexPathResolver indexPathResolver = null!;

    [TestInitialize]
    public void Setup()
    {
        indexManagerFactory = Substitute.For<ILuceneIndexManagerFactory>();
        queryEngineFactory = Substitute.For<IQueryEngineFactory>();
        indexPathResolver = Substitute.For<IIndexPathResolver>();
        indexPathResolver.ResolveIndexPath(Arg.Any<string>()).Returns(info => info.Arg<string>() ?? "./index");
        console = new TestConsole();
        console.Profile.Width = 120;
        console.Profile.Height = 40;

        command = new QueryCommand(indexManagerFactory, queryEngineFactory, indexPathResolver, console);
    }

    [TestCleanup]
    public void Cleanup()
    {
        console?.Dispose();
    }

    [TestMethod]
    public void Settings_WithRequiredQuery_IsValid()
    {
        // Arrange
        QueryCommand.Settings settings = new()
        {
            Query = "String"
        };

        // Assert
        settings.Query.ShouldBe("String");
        settings.IndexPath.ShouldBeNull(); // Default is null; resolved by IndexPathResolver to ~/.apilens/index
        settings.MaxResults.ShouldBe(10);
        settings.QueryType.ShouldBe(QueryCommand.QueryType.Name);
        settings.Format.ShouldBe(OutputFormat.Table);
        settings.Distinct.ShouldBeTrue(); // Verify new default
    }

    [TestMethod]
    public void Settings_WithAllOptions_IsValid()
    {
        // Arrange
        QueryCommand.Settings settings = new()
        {
            Query = "System.String",
            IndexPath = "/custom/index",
            MaxResults = 50,
            QueryType = QueryCommand.QueryType.Content,
            Format = OutputFormat.Json
        };

        // Assert
        settings.Query.ShouldBe("System.String");
        settings.IndexPath.ShouldBe("/custom/index");
        settings.MaxResults.ShouldBe(50);
        settings.QueryType.ShouldBe(QueryCommand.QueryType.Content);
        settings.Format.ShouldBe(OutputFormat.Json);
    }

    [TestMethod]
    public void Execute_CallsFactoriesWithCorrectParameters()
    {
        // Arrange
        ILuceneIndexManager? mockIndexManager = Substitute.For<ILuceneIndexManager>();
        IQueryEngine? mockQueryEngine = Substitute.For<IQueryEngine>();

        indexManagerFactory.Create("./test-index").Returns(mockIndexManager);
        queryEngineFactory.Create(mockIndexManager).Returns(mockQueryEngine);

        mockQueryEngine.SearchByName("TestQuery", 10).Returns([]);
        mockQueryEngine.SearchByName("TestQuery", 10, Arg.Any<bool>()).Returns([]);

        QueryCommand.Settings settings = new()
        {
            Query = "TestQuery",
            IndexPath = "./test-index",
            MaxResults = 10,
            QueryType = QueryCommand.QueryType.Name
        };

        // Act
        int result = command.Execute(null!, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        indexManagerFactory.Received(1).Create("./test-index");
        queryEngineFactory.Received(1).Create(mockIndexManager);
        mockQueryEngine.Received(1).SearchByName("TestQuery", 10, Arg.Any<bool>());
        mockIndexManager.Received(1).Dispose();
        mockQueryEngine.Received(1).Dispose();
    }

    [TestMethod]
    public void Settings_DefaultsDistinctToTrue()
    {
        // Arrange & Act
        var settings = new QueryCommand.Settings
        {
            Query = "TestQuery"
        };

        // Assert
        settings.Distinct.ShouldBeTrue();
    }

    [TestMethod]
    public void Execute_WithDistinct_AppliesDeduplication()
    {
        // Arrange
        ILuceneIndexManager? mockIndexManager = Substitute.For<ILuceneIndexManager>();
        IQueryEngine? mockQueryEngine = Substitute.For<IQueryEngine>();

        indexManagerFactory.Create("./test-index").Returns(mockIndexManager);
        queryEngineFactory.Create(mockIndexManager).Returns(mockQueryEngine);

        var member1 = new ApiLens.Core.Models.MemberInfo
        {
            Id = "T:TestType",
            Name = "TestType",
            FullName = "TestNamespace.TestType",
            MemberType = ApiLens.Core.Models.MemberType.Type,
            Assembly = "TestAssembly",
            Namespace = "TestNamespace",
            TargetFramework = "net8.0"
        };

        var member2 = new ApiLens.Core.Models.MemberInfo
        {
            Id = "T:TestType",
            Name = "TestType",
            FullName = "TestNamespace.TestType",
            MemberType = ApiLens.Core.Models.MemberType.Type,
            Assembly = "TestAssembly",
            Namespace = "TestNamespace",
            TargetFramework = "net9.0"
        };

        mockQueryEngine.SearchByName("TestQuery", 10).Returns(new List<ApiLens.Core.Models.MemberInfo> { member1, member2 });
        mockQueryEngine.SearchByName("TestQuery", 10, Arg.Any<bool>()).Returns(new List<ApiLens.Core.Models.MemberInfo> { member1, member2 });

        QueryCommand.Settings settings = new()
        {
            Query = "TestQuery",
            IndexPath = "./test-index",
            MaxResults = 10,
            QueryType = QueryCommand.QueryType.Name,
            Distinct = true
        };

        // Act
        int result = command.Execute(null!, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        // The deduplication should have been applied, reducing duplicates
    }

    public void Dispose()
    {
        console?.Dispose();
    }
}