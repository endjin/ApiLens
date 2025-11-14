using ApiLens.Cli.Commands;
using ApiLens.Cli.Services;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;
using Spectre.Console.Cli;

namespace ApiLens.Cli.Tests.Commands;

[TestClass]
public class ComplexityCommandTests
{
    private ILuceneIndexManagerFactory indexManagerFactory = null!;
    private IQueryEngineFactory queryEngineFactory = null!;
    private IIndexPathResolver indexPathResolver = null!;
    private ILuceneIndexManager indexManager = null!;
    private IQueryEngine queryEngine = null!;
    private CommandContext context = null!;

    [TestInitialize]
    public void Initialize()
    {
        indexManagerFactory = Substitute.For<ILuceneIndexManagerFactory>();
        queryEngineFactory = Substitute.For<IQueryEngineFactory>();
        indexPathResolver = Substitute.For<IIndexPathResolver>();
        indexManager = Substitute.For<ILuceneIndexManager>();
        queryEngine = Substitute.For<IQueryEngine>();
        // CommandContext is sealed, so we'll pass null in tests since it's not used
        context = null!;

        indexPathResolver.ResolveIndexPath(Arg.Any<string>()).Returns(info => info.Arg<string>() ?? "./index");
        indexManagerFactory.Create(Arg.Any<string>()).Returns(indexManager);
        queryEngineFactory.Create(indexManager).Returns(queryEngine);
    }

    private static MemberInfo CreateMemberInfo(string name, ComplexityMetrics? complexity = null)
    {
        return new MemberInfo
        {
            Id = $"M:Test.{name}",
            Name = name,
            FullName = $"Test.{name}",
            MemberType = MemberType.Method,
            Assembly = "TestAssembly",
            Namespace = "Test",
            Complexity = complexity
        };
    }

    [TestMethod]
    public void Settings_WithDefaults_IsValid()
    {
        // Arrange
        ComplexityCommand.Settings settings = new();

        // Assert
        settings.MinComplexity.ShouldBeNull();
        settings.MinParams.ShouldBeNull();
        settings.MaxParams.ShouldBeNull();
        settings.IndexPath.ShouldBe("./index");
        settings.MaxResults.ShouldBe(20);
        settings.SortBy.ShouldBe("complexity");
        settings.ShowStats.ShouldBe(false);
        settings.Format.ShouldBe(OutputFormat.Table);
    }

    [TestMethod]
    public void Settings_WithAllOptions_IsValid()
    {
        // Arrange
        ComplexityCommand.Settings settings = new()
        {
            MinComplexity = 10,
            MinParams = 3,
            MaxParams = 7,
            IndexPath = "/custom/index",
            MaxResults = 50,
            SortBy = "params",
            ShowStats = true,
            Format = OutputFormat.Json
        };

        // Assert
        settings.MinComplexity.ShouldBe(10);
        settings.MinParams.ShouldBe(3);
        settings.MaxParams.ShouldBe(7);
        settings.IndexPath.ShouldBe("/custom/index");
        settings.MaxResults.ShouldBe(50);
        settings.SortBy.ShouldBe("params");
        settings.ShowStats.ShouldBe(true);
        settings.Format.ShouldBe(OutputFormat.Json);
    }

    [TestMethod]
    public void Execute_WithNoParameters_ReturnsError()
    {
        // Arrange
        ComplexityCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ComplexityCommand.Settings settings = new();

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
    }

    [TestMethod]
    public void Execute_WithMinComplexity_CallsGetComplexMethods()
    {
        // Arrange
        ComplexityCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ComplexityCommand.Settings settings = new()
        {
            MinComplexity = 10,
            MaxResults = 50
        };

        List<MemberInfo> expectedResults =
        [
            CreateMemberInfo("Method1"),
            CreateMemberInfo("Method2")
        ];
        queryEngine.GetComplexMethods(10, 50).Returns(expectedResults);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        queryEngine.Received(1).GetComplexMethods(10, 50);
    }

    [TestMethod]
    public void Execute_WithParameterRange_CallsGetByParameterCount()
    {
        // Arrange
        ComplexityCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ComplexityCommand.Settings settings = new()
        {
            MinParams = 2,
            MaxParams = 5,
            MaxResults = 30
        };

        List<MemberInfo> expectedResults = [CreateMemberInfo("Method1")];
        queryEngine.GetByParameterCount(2, 5, 30).Returns(expectedResults);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        queryEngine.Received(1).GetByParameterCount(2, 5, 30);
    }

    [TestMethod]
    public void Execute_WithMinParamsOnly_CallsGetByParameterCount()
    {
        // Arrange
        ComplexityCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ComplexityCommand.Settings settings = new()
        {
            MinParams = 3,
            MaxResults = 20
        };

        List<MemberInfo> expectedResults = [];
        queryEngine.GetByParameterCount(3, int.MaxValue, 20).Returns(expectedResults);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        queryEngine.Received(1).GetByParameterCount(3, int.MaxValue, 20);
    }

    [TestMethod]
    public void Execute_WithMaxParamsOnly_CallsGetByParameterCount()
    {
        // Arrange
        ComplexityCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ComplexityCommand.Settings settings = new()
        {
            MaxParams = 5,
            MaxResults = 20
        };

        List<MemberInfo> expectedResults = [];
        queryEngine.GetByParameterCount(0, 5, 20).Returns(expectedResults);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        queryEngine.Received(1).GetByParameterCount(0, 5, 20);
    }

    [TestMethod]
    public void Execute_WithException_ReturnsErrorCode()
    {
        // Arrange
        ComplexityCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ComplexityCommand.Settings settings = new() { MinComplexity = 10 };

        queryEngine.When(x => x.GetComplexMethods(Arg.Any<int>(), Arg.Any<int>()))
            .Do(x => throw new InvalidOperationException("Index error"));

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
    }

    [TestMethod]
    public void Execute_DisposesResources()
    {
        // Arrange
        ComplexityCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ComplexityCommand.Settings settings = new() { MinComplexity = 5 };

        queryEngine.GetComplexMethods(5, 20).Returns([]);

        // Act
        command.Execute(context, settings, CancellationToken.None);

        // Assert
        indexManager.Received(1).Dispose();
        queryEngine.Received(1).Dispose();
    }

    [TestMethod]
    public void Execute_WithJsonFormat_ProcessesResults()
    {
        // Arrange
        ComplexityCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ComplexityCommand.Settings settings = new()
        {
            MinComplexity = 5,
            Format = OutputFormat.Json
        };

        MemberInfo memberInfo = CreateMemberInfo("Method", new ComplexityMetrics
        {
            CyclomaticComplexity = 10,
            ParameterCount = 3,
            DocumentationLineCount = 5
        });

        queryEngine.GetComplexMethods(5, 20).Returns([memberInfo]);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_WithMarkdownFormat_ProcessesResults()
    {
        // Arrange
        ComplexityCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ComplexityCommand.Settings settings = new()
        {
            MinComplexity = 5,
            Format = OutputFormat.Markdown
        };

        MemberInfo memberInfo = CreateMemberInfo("Method", new ComplexityMetrics
        {
            CyclomaticComplexity = 10,
            ParameterCount = 3,
            DocumentationLineCount = 5
        });

        queryEngine.GetComplexMethods(5, 20).Returns([memberInfo]);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_WithTableFormat_ProcessesResults()
    {
        // Arrange
        ComplexityCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ComplexityCommand.Settings settings = new()
        {
            MinComplexity = 5,
            Format = OutputFormat.Table,
            ShowStats = true
        };

        List<MemberInfo> members =
        [
            CreateMemberInfo("Method1", new ComplexityMetrics
            {
                CyclomaticComplexity = 10,
                ParameterCount = 2,
                DocumentationLineCount = 5
            }),

            CreateMemberInfo("Method2", new ComplexityMetrics
            {
                CyclomaticComplexity = 20,
                ParameterCount = 4,
                DocumentationLineCount = 10
            })
        ];

        queryEngine.GetComplexMethods(5, 20).Returns(members);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_SortsByComplexity_WhenSpecified()
    {
        // Arrange
        ComplexityCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ComplexityCommand.Settings settings = new()
        {
            MinComplexity = 1,
            SortBy = "complexity"
        };

        List<MemberInfo> members =
        [
            CreateMemberInfo("Method1", new ComplexityMetrics
            {
                CyclomaticComplexity = 20,
                ParameterCount = 0,
                DocumentationLineCount = 0
            }),

            CreateMemberInfo("Method2", new ComplexityMetrics
            {
                CyclomaticComplexity = 10,
                ParameterCount = 0,
                DocumentationLineCount = 0
            })
        ];

        queryEngine.GetComplexMethods(1, 20).Returns(members);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        // The sorting happens within the Execute method
    }

    [TestMethod]
    public void Execute_SortsByParameters_WhenSpecified()
    {
        // Arrange
        ComplexityCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ComplexityCommand.Settings settings = new()
        {
            MinParams = 1,
            SortBy = "params"
        };

        List<MemberInfo> members =
        [
            CreateMemberInfo("Method1", new ComplexityMetrics
            {
                ParameterCount = 5,
                CyclomaticComplexity = 1,
                DocumentationLineCount = 0
            }),

            CreateMemberInfo("Method2", new ComplexityMetrics
            {
                ParameterCount = 2,
                CyclomaticComplexity = 1,
                DocumentationLineCount = 0
            })
        ];

        queryEngine.GetByParameterCount(1, int.MaxValue, 20).Returns(members);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        // The sorting happens within the Execute method
    }
}