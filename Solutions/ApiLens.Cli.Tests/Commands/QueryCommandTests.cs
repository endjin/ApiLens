using ApiLens.Cli.Commands;
using ApiLens.Core.Lucene;
using ApiLens.Core.Querying;

namespace ApiLens.Cli.Tests.Commands;

[TestClass]
public class QueryCommandTests
{
    private QueryCommand command = null!;
    private ILuceneIndexManagerFactory indexManagerFactory = null!;
    private IQueryEngineFactory queryEngineFactory = null!;

    [TestInitialize]
    public void Setup()
    {
        indexManagerFactory = Substitute.For<ILuceneIndexManagerFactory>();
        queryEngineFactory = Substitute.For<IQueryEngineFactory>();
        command = new QueryCommand(indexManagerFactory, queryEngineFactory);
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
        settings.IndexPath.ShouldBe("./index");
        settings.MaxResults.ShouldBe(10);
        settings.QueryType.ShouldBe(QueryCommand.QueryType.Name);
        settings.Format.ShouldBe(OutputFormat.Table);
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

        QueryCommand.Settings settings = new()
        {
            Query = "TestQuery",
            IndexPath = "./test-index",
            MaxResults = 10,
            QueryType = QueryCommand.QueryType.Name
        };

        // Act
        int result = command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        indexManagerFactory.Received(1).Create("./test-index");
        queryEngineFactory.Received(1).Create(mockIndexManager);
        mockQueryEngine.Received(1).SearchByName("TestQuery", 10);
        mockIndexManager.Received(1).Dispose();
        mockQueryEngine.Received(1).Dispose();
    }
}