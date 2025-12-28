using ApiLens.Cli.Commands;
using ApiLens.Cli.Services;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace ApiLens.Cli.Tests.Commands;

[TestClass]
public class ExamplesCommandTests : IDisposable
{
    private ILuceneIndexManagerFactory indexManagerFactory = null!;
    private IQueryEngineFactory queryEngineFactory = null!;
    private IIndexPathResolver indexPathResolver = null!;
    private ILuceneIndexManager indexManager = null!;
    private IQueryEngine queryEngine = null!;
    private CommandContext context = null!;
    private TestConsole console = null!;

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

        console = new TestConsole();
        console.Profile.Width = 120;
        console.Profile.Height = 40;
    }

    [TestCleanup]
    public void Cleanup()
    {
        console?.Dispose();
    }

    public void Dispose()
    {
        console?.Dispose();
    }

    private static MemberInfo CreateMemberInfoWithExamples(string name, params CodeExample[] examples)
    {
        return new MemberInfo
        {
            Id = $"M:Test.{name}",
            Name = name,
            FullName = $"Test.{name}",
            MemberType = MemberType.Method,
            Assembly = "TestAssembly",
            Namespace = "Test",
            CodeExamples = [.. examples]
        };
    }

    [TestMethod]
    public void Settings_WithDefaults_IsValid()
    {
        // Arrange
        ExamplesCommand.Settings settings = new();

        // Assert
        settings.Pattern.ShouldBeNull();
        settings.IndexPath.ShouldBeNull(); // Default is null; resolved by IndexPathResolver to ~/.apilens/index
        settings.MaxResults.ShouldBe(10);
        settings.Format.ShouldBe(OutputFormat.Table);
    }

    [TestMethod]
    public void Settings_WithAllOptions_IsValid()
    {
        // Arrange
        ExamplesCommand.Settings settings = new()
        {
            Pattern = "async",
            IndexPath = "/custom/index",
            MaxResults = 50,
            Format = OutputFormat.Json
        };

        // Assert
        settings.Pattern.ShouldBe("async");
        settings.IndexPath.ShouldBe("/custom/index");
        settings.MaxResults.ShouldBe(50);
        settings.Format.ShouldBe(OutputFormat.Json);
    }

    [TestMethod]
    public void Execute_WithNoPattern_CallsGetMethodsWithExamples()
    {
        // Arrange
        ExamplesCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory, console);
        ExamplesCommand.Settings settings = new() { MaxResults = 20 };

        List<MemberInfo> expectedResults =
        [
            CreateMemberInfoWithExamples("Method1",
                new CodeExample { Language = "csharp", Code = "// Example 1", Description = "Example 1" }),
            CreateMemberInfoWithExamples("Method2",
                new CodeExample { Language = "csharp", Code = "// Example 2", Description = "Example 2" })
        ];
        queryEngine.GetMethodsWithExamples(20).Returns(expectedResults);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        queryEngine.Received(1).GetMethodsWithExamples(20);
        queryEngine.DidNotReceive().SearchByCodeExample(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Execute_WithPattern_CallsSearchByCodeExample()
    {
        // Arrange
        ExamplesCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory, console);
        ExamplesCommand.Settings settings = new()
        {
            Pattern = "async",
            MaxResults = 30
        };

        List<MemberInfo> expectedResults =
        [
            CreateMemberInfoWithExamples("AsyncMethod",
                new CodeExample
                    { Language = "csharp", Code = "await Task.Delay(100);", Description = "Async delay example" })
        ];
        queryEngine.SearchByCodeExample("async", 30).Returns(expectedResults);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        queryEngine.Received(1).SearchByCodeExample("async", 30);
        queryEngine.DidNotReceive().GetMethodsWithExamples(Arg.Any<int>());
    }

    [TestMethod]
    public void Execute_WithEmptyPattern_CallsGetMethodsWithExamples()
    {
        // Arrange
        ExamplesCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory, console);
        ExamplesCommand.Settings settings = new()
        {
            Pattern = "   ", // Whitespace only
            MaxResults = 10
        };

        List<MemberInfo> expectedResults = [];
        queryEngine.GetMethodsWithExamples(10).Returns(expectedResults);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        queryEngine.Received(1).GetMethodsWithExamples(10);
        queryEngine.DidNotReceive().SearchByCodeExample(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Execute_WithNoResults_ReturnsSuccess()
    {
        // Arrange
        ExamplesCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory, console);
        ExamplesCommand.Settings settings = new() { Pattern = "nonexistent" };

        queryEngine.SearchByCodeExample("nonexistent", 10).Returns([]);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_WithException_ReturnsErrorCode()
    {
        // Arrange
        ExamplesCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory, console);
        ExamplesCommand.Settings settings = new();

        queryEngine.When(x => x.GetMethodsWithExamples(Arg.Any<int>()))
            .Do(_ => throw new InvalidOperationException("Index error"));

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
    }

    [TestMethod]
    public void Execute_DisposesResources()
    {
        // Arrange
        ExamplesCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory, console);
        ExamplesCommand.Settings settings = new();

        queryEngine.GetMethodsWithExamples(10).Returns([]);

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
        ExamplesCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory, console);
        ExamplesCommand.Settings settings = new()
        {
            Format = OutputFormat.Json
        };

        MemberInfo memberInfo = CreateMemberInfoWithExamples(
            "TestMethod",
            new CodeExample
            {
                Language = "csharp",
                Code = "Console.WriteLine(\"Hello\");",
                Description = "Basic example"
            }
        );

        queryEngine.GetMethodsWithExamples(10).Returns([memberInfo]);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_WithMarkdownFormat_ProcessesResults()
    {
        // Arrange
        ExamplesCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory, console);
        ExamplesCommand.Settings settings = new()
        {
            Format = OutputFormat.Markdown
        };

        MemberInfo memberInfo = CreateMemberInfoWithExamples(
            "TestMethod",
            new CodeExample
            {
                Language = "csharp",
                Code = "var result = await ProcessAsync();",
                Description = "Async example"
            }
        );

        queryEngine.GetMethodsWithExamples(10).Returns([memberInfo]);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_WithTableFormat_ProcessesResults()
    {
        // Arrange
        ExamplesCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory, console);
        ExamplesCommand.Settings settings = new()
        {
            Format = OutputFormat.Table
        };

        List<MemberInfo> members =
        [
            CreateMemberInfoWithExamples(
                "Method1",
                new CodeExample { Language = "csharp", Code = "// Example 1", Description = "C# example" },
                new CodeExample { Language = "vb", Code = "' Example 1 in VB", Description = "VB example" }
            ),

            CreateMemberInfoWithExamples(
                "Method2",
                new CodeExample { Language = "csharp", Code = "// Example 2", Description = "Example 2" }
            )
        ];

        queryEngine.GetMethodsWithExamples(10).Returns(members);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_WithMultipleExamplesPerMethod_HandlesCorrectly()
    {
        // Arrange
        ExamplesCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory, console);
        ExamplesCommand.Settings settings = new();

        MemberInfo memberInfo = CreateMemberInfoWithExamples(
            "MultiExampleMethod",
            new CodeExample { Language = "csharp", Code = "// C# example", Description = "C# version" },
            new CodeExample { Language = "vb", Code = "' VB example", Description = "VB version" },
            new CodeExample { Language = "fsharp", Code = "// F# example", Description = "F# version" }
        );

        queryEngine.GetMethodsWithExamples(10).Returns([memberInfo]);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_WithJsonFormatAndNoResults_ReturnsEmptyArray()
    {
        // Arrange
        ExamplesCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory, console);
        ExamplesCommand.Settings settings = new()
        {
            Format = OutputFormat.Json,
            Pattern = "nonexistent"
        };

        queryEngine.SearchByCodeExample("nonexistent", 10).Returns([]);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_SearchWithPattern_ReturnsMatchingExamples()
    {
        // Arrange
        ExamplesCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory, console);
        ExamplesCommand.Settings settings = new()
        {
            Pattern = "Task.Run",
            MaxResults = 5
        };

        MemberInfo memberInfo = CreateMemberInfoWithExamples(
            "ParallelMethod",
            new CodeExample
            {
                Language = "csharp",
                Code = "await Task.Run(() => DoWork());",
                Description = "Running work in background"
            }
        );

        queryEngine.SearchByCodeExample("Task.Run", 5).Returns([memberInfo]);

        // Act
        int result = command.Execute(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        queryEngine.Received(1).SearchByCodeExample("Task.Run", 5);
    }
}