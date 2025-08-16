using ApiLens.Cli.Commands;
using ApiLens.Cli.Services;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;
using Spectre.Console.Cli;

namespace ApiLens.Cli.Tests.Commands;

[TestClass]
public class ExceptionsCommandTests
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

    private static MemberInfo CreateMemberInfoWithExceptions(string name, params ExceptionInfo[] exceptions)
    {
        return new MemberInfo
        {
            Id = $"M:Test.{name}",
            Name = name,
            FullName = $"Test.{name}",
            MemberType = MemberType.Method,
            Assembly = "TestAssembly",
            Namespace = "Test",
            Exceptions = [.. exceptions]
        };
    }

    [TestMethod]
    public void Settings_WithDefaults_IsValid()
    {
        // Arrange
        ExceptionsCommand.Settings settings = new()
        {
            ExceptionType = "ArgumentNullException"
        };

        // Assert
        settings.ExceptionType.ShouldBe("ArgumentNullException");
        settings.IndexPath.ShouldBe("./index");
        settings.MaxResults.ShouldBe(10);
        settings.ShowDetails.ShouldBe(false);
        settings.Format.ShouldBe(OutputFormat.Table);
    }

    [TestMethod]
    public void Settings_WithAllOptions_IsValid()
    {
        // Arrange
        ExceptionsCommand.Settings settings = new()
        {
            ExceptionType = "IOException",
            IndexPath = "/custom/index",
            MaxResults = 50,
            ShowDetails = true,
            Format = OutputFormat.Json
        };

        // Assert
        settings.ExceptionType.ShouldBe("IOException");
        settings.IndexPath.ShouldBe("/custom/index");
        settings.MaxResults.ShouldBe(50);
        settings.ShowDetails.ShouldBe(true);
        settings.Format.ShouldBe(OutputFormat.Json);
    }

    [TestMethod]
    public void Execute_WithExceptionType_CallsGetByExceptionType()
    {
        // Arrange
        ExceptionsCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ExceptionsCommand.Settings settings = new()
        {
            ExceptionType = "System.ArgumentNullException",
            MaxResults = 30
        };

        List<MemberInfo> expectedResults =
        [
            CreateMemberInfoWithExceptions("Method1",
                new ExceptionInfo { Type = "System.ArgumentNullException", Condition = "When parameter is null" }),

            CreateMemberInfoWithExceptions("Method2",
                new ExceptionInfo { Type = "System.ArgumentNullException", Condition = "When argument is null" })
        ];
        queryEngine.GetByExceptionType("System.ArgumentNullException", 30).Returns(expectedResults);

        // Act
        int result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);
        queryEngine.Received(1).GetByExceptionType("System.ArgumentNullException", 30);
    }

    [TestMethod]
    public void Execute_WithNoResults_ReturnsSuccess()
    {
        // Arrange
        ExceptionsCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ExceptionsCommand.Settings settings = new()
        {
            ExceptionType = "System.CustomException"
        };

        queryEngine.GetByExceptionType("System.CustomException", 10).Returns([]);

        // Act
        int result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_WithException_ReturnsErrorCode()
    {
        // Arrange
        ExceptionsCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ExceptionsCommand.Settings settings = new() { ExceptionType = "System.Exception" };

        queryEngine.When(x => x.GetByExceptionType(Arg.Any<string>(), Arg.Any<int>()))
            .Do(_ => throw new InvalidOperationException("Index error"));

        // Act
        int result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(1);
    }

    [TestMethod]
    public void Execute_DisposesResources()
    {
        // Arrange
        ExceptionsCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ExceptionsCommand.Settings settings = new() { ExceptionType = "System.Exception" };

        queryEngine.GetByExceptionType("System.Exception", 10).Returns([]);

        // Act
        command.Execute(context, settings);

        // Assert
        indexManager.Received(1).Dispose();
        queryEngine.Received(1).Dispose();
    }

    [TestMethod]
    public void Execute_WithJsonFormat_ProcessesResults()
    {
        // Arrange
        ExceptionsCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ExceptionsCommand.Settings settings = new()
        {
            ExceptionType = "System.InvalidOperationException",
            Format = OutputFormat.Json
        };

        MemberInfo memberInfo = CreateMemberInfoWithExceptions(
            "TestMethod",
            new ExceptionInfo
            {
                Type = "System.InvalidOperationException",
                Condition = "When operation is invalid"
            }
        );

        queryEngine.GetByExceptionType("System.InvalidOperationException", 10)
            .Returns([memberInfo]);

        // Act
        int result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_WithMarkdownFormat_ProcessesResults()
    {
        // Arrange
        ExceptionsCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ExceptionsCommand.Settings settings = new()
        {
            ExceptionType = "System.ArgumentException",
            Format = OutputFormat.Markdown
        };

        MemberInfo memberInfo = CreateMemberInfoWithExceptions(
            "ValidateArg",
            new ExceptionInfo
            {
                Type = "System.ArgumentException",
                Condition = "When argument is invalid"
            }
        );

        queryEngine.GetByExceptionType("System.ArgumentException", 10)
            .Returns([memberInfo]);

        // Act
        int result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_WithTableFormat_ProcessesResults()
    {
        // Arrange
        ExceptionsCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ExceptionsCommand.Settings settings = new()
        {
            ExceptionType = "System.Exception",
            Format = OutputFormat.Table
        };

        List<MemberInfo> members =
        [
            CreateMemberInfoWithExceptions(
                "Method1",
                new ExceptionInfo { Type = "System.Exception", Condition = "General error" },
                new ExceptionInfo { Type = "System.ArgumentException", Condition = "Invalid argument" }
            ),

            CreateMemberInfoWithExceptions(
                "Method2",
                new ExceptionInfo { Type = "System.Exception", Condition = "Another error" }
            )
        ];

        queryEngine.GetByExceptionType("System.Exception", 10).Returns(members);

        // Act
        int result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_WithShowDetails_HandlesCorrectly()
    {
        // Arrange
        ExceptionsCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ExceptionsCommand.Settings settings = new()
        {
            ExceptionType = "System.ArgumentNullException",
            ShowDetails = true
        };

        MemberInfo memberInfo = CreateMemberInfoWithExceptions(
            "ComplexMethod",
            new ExceptionInfo { Type = "System.ArgumentNullException", Condition = "When first parameter is null" },
            new ExceptionInfo { Type = "System.ArgumentNullException", Condition = "When second parameter is null" },
            new ExceptionInfo { Type = "System.InvalidOperationException", Condition = "When state is invalid" }
        );

        queryEngine.GetByExceptionType("System.ArgumentNullException", 10)
            .Returns([memberInfo]);

        // Act
        int result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_WithJsonFormatAndNoResults_ReturnsEmptyArray()
    {
        // Arrange
        ExceptionsCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ExceptionsCommand.Settings settings = new()
        {
            ExceptionType = "System.NonExistentException",
            Format = OutputFormat.Json
        };

        queryEngine.GetByExceptionType("System.NonExistentException", 10)
            .Returns([]);

        // Act
        int result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public void Execute_WithFullyQualifiedExceptionType_Works()
    {
        // Arrange
        ExceptionsCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ExceptionsCommand.Settings settings = new()
        {
            ExceptionType = "System.Collections.Generic.KeyNotFoundException",
            MaxResults = 5
        };

        MemberInfo memberInfo = CreateMemberInfoWithExceptions(
            "GetValue",
            new ExceptionInfo
            {
                Type = "System.Collections.Generic.KeyNotFoundException",
                Condition = "When key does not exist in dictionary"
            }
        );

        queryEngine.GetByExceptionType("System.Collections.Generic.KeyNotFoundException", 5)
            .Returns([memberInfo]);

        // Act
        int result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);
        queryEngine.Received(1).GetByExceptionType("System.Collections.Generic.KeyNotFoundException", 5);
    }

    [TestMethod]
    public void Execute_WithEmptyExceptionType_ReturnsSuccess()
    {
        // Arrange
        ExceptionsCommand command = new(indexManagerFactory, indexPathResolver, queryEngineFactory);
        ExceptionsCommand.Settings settings = new()
        {
            ExceptionType = string.Empty
        };

        queryEngine.GetByExceptionType(string.Empty, 10).Returns([]);

        // Act
        int result = command.Execute(context, settings);

        // Assert
        result.ShouldBe(0);
    }
}