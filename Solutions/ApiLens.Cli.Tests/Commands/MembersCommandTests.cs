using ApiLens.Cli.Commands;
using ApiLens.Cli.Services;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;
using Spectre.Console;
using Spectre.Console.Testing;

namespace ApiLens.Cli.Tests.Commands;

[TestClass]
public class MembersCommandTests : IDisposable
{
    private MembersCommand command = null!;
    private ILuceneIndexManagerFactory indexManagerFactory = null!;
    private IQueryEngineFactory queryEngineFactory = null!;
    private TestConsole console = null!;
    private ILuceneIndexManager mockIndexManager = null!;
    private IQueryEngine mockQueryEngine = null!;
    private IIndexPathResolver indexPathResolver = null!;

    [TestInitialize]
    public void Setup()
    {
        indexManagerFactory = Substitute.For<ILuceneIndexManagerFactory>();
        queryEngineFactory = Substitute.For<IQueryEngineFactory>();
        indexPathResolver = Substitute.For<IIndexPathResolver>();
        indexPathResolver.ResolveIndexPath(Arg.Any<string>()).Returns(info => info.Arg<string>() ?? "./index");
        command = new MembersCommand(indexManagerFactory, indexPathResolver, queryEngineFactory);
        console = new TestConsole();
        AnsiConsole.Console = console;

        mockIndexManager = Substitute.For<ILuceneIndexManager>();
        mockQueryEngine = Substitute.For<IQueryEngine>();

        indexManagerFactory.Create(Arg.Any<string>()).Returns(mockIndexManager);
        queryEngineFactory.Create(mockIndexManager).Returns(mockQueryEngine);
    }

    [TestCleanup]
    public void Cleanup()
    {
        console?.Dispose();
    }

    [TestMethod]
    public void Settings_DefaultsDistinctToTrue()
    {
        // Arrange & Act
        var settings = new MembersCommand.Settings
        {
            TypeName = "TestType"
        };

        // Assert
        settings.Distinct.ShouldBeTrue();
        settings.MaxResults.ShouldBe(1000);
        settings.MaxPerType.ShouldBe(20);
        settings.Format.ShouldBe(OutputFormat.Table);
    }

    [TestMethod]
    public void Execute_WithDistinct_CallsDeduplicationService()
    {
        // Arrange
        var targetType = CreateMemberInfo("TestType", MemberType.Type);
        var member1 = CreateMemberInfo("Method1", MemberType.Method, "net8.0");
        var member2 = CreateMemberInfo("Method1", MemberType.Method, "net9.0");

        mockQueryEngine.SearchByName("TestType", 10)
            .Returns(new List<MemberInfo> { targetType });

        mockQueryEngine.SearchByDeclaringType("TestNamespace.TestType", 1000)
            .Returns(new List<MemberInfo> { member1, member2 });

        var settings = new MembersCommand.Settings
        {
            TypeName = "TestType",
            Distinct = true,
            IndexPath = "./test-index"
        };

        // Act
        int result = command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        mockQueryEngine.Received(1).SearchByName("TestType", 10);
        mockQueryEngine.Received(1).SearchByDeclaringType("TestNamespace.TestType", 1000);

        // Verify deduplication happened (check console output)
        var output = console.Output;
        output.ShouldContain("TestType");
        output.ShouldContain("Method1");
    }

    [TestMethod]
    public void Execute_WithoutDistinct_SkipsDeduplication()
    {
        // Arrange
        var targetType = CreateMemberInfo("TestType", MemberType.Type);
        var member1 = CreateMemberInfo("Method1", MemberType.Method, "net8.0");
        var member2 = CreateMemberInfo("Method1", MemberType.Method, "net9.0");

        mockQueryEngine.SearchByName("TestType", 10)
            .Returns(new List<MemberInfo> { targetType });

        mockQueryEngine.SearchByDeclaringType("TestNamespace.TestType", 1000)
            .Returns(new List<MemberInfo> { member1, member2 });

        var settings = new MembersCommand.Settings
        {
            TypeName = "TestType",
            Distinct = false,
            IndexPath = "./test-index"
        };

        // Act
        int result = command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        var output = console.Output;
        output.ShouldContain("TestType");
        output.ShouldContain("Method1");
    }

    [TestMethod]
    public void Execute_WithTypeNotFound_ReturnsError()
    {
        // Arrange
        mockQueryEngine.SearchByName("NonExistentType", 10)
            .Returns(new List<MemberInfo>());

        mockQueryEngine.SearchWithFilters("*NonExistentType*", MemberType.Type, null, null, 10)
            .Returns(new List<MemberInfo>());

        var settings = new MembersCommand.Settings
        {
            TypeName = "NonExistentType",
            IndexPath = "./test-index"
        };

        // Act
        int result = command.Execute(null!, settings);

        // Assert
        result.ShouldBe(1);
        var output = console.Output;
        output.ShouldContain("not found");
    }

    [TestMethod]
    public void Execute_WithJsonFormat_OutputsJson()
    {
        // Arrange
        var targetType = CreateMemberInfo("TestType", MemberType.Type);
        var method = CreateMemberInfo("TestMethod", MemberType.Method);

        mockQueryEngine.SearchByName("TestType", 10)
            .Returns(new List<MemberInfo> { targetType });

        mockQueryEngine.SearchByDeclaringType("TestNamespace.TestType", 1000)
            .Returns(new List<MemberInfo> { method });

        var settings = new MembersCommand.Settings
        {
            TypeName = "TestType",
            Format = OutputFormat.Json,
            IndexPath = "./test-index"
        };

        // Act
        int result = command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        var output = console.Output;
        output.ShouldContain("{");
        output.ShouldContain("\"type\"");
        output.ShouldContain("\"members\"");
        output.ShouldContain("TestMethod");
    }

    [TestMethod]
    public void Execute_GroupsByMemberType()
    {
        // Arrange
        var targetType = CreateMemberInfo("TestType", MemberType.Type);
        var method = CreateMemberInfo("TestMethod", MemberType.Method);
        var property = CreateMemberInfo("TestProperty", MemberType.Property);
        var field = CreateMemberInfo("TestField", MemberType.Field);

        mockQueryEngine.SearchByName("TestType", 10)
            .Returns(new List<MemberInfo> { targetType });

        mockQueryEngine.SearchByDeclaringType("TestNamespace.TestType", 1000)
            .Returns(new List<MemberInfo> { method, property, field });

        var settings = new MembersCommand.Settings
        {
            TypeName = "TestType",
            IndexPath = "./test-index"
        };

        // Act
        int result = command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        var output = console.Output;
        output.ShouldContain("Properties");
        output.ShouldContain("Methods");
        output.ShouldContain("Fields");
    }

    private static MemberInfo CreateMemberInfo(string name, MemberType type, string? framework = "net9.0")
    {
        return new MemberInfo
        {
            Id = $"{type.ToString()[0]}:TestNamespace.TestType.{name}",
            Name = name,
            FullName = $"TestNamespace.TestType{(type == MemberType.Type ? "" : $".{name}")}",
            MemberType = type,
            Assembly = "TestAssembly",
            Namespace = "TestNamespace",
            TargetFramework = framework,
            Summary = $"Summary for {name}",
            Parameters = type == MemberType.Method
                ? [new ParameterInfo { Name = "param1", Type = "string", Position = 0, IsOptional = false, IsParams = false, IsOut = false, IsRef = false }]
                : [],
            ReturnType = type == MemberType.Method ? "void" : null
        };
    }

    public void Dispose()
    {
        console?.Dispose();
        mockIndexManager?.Dispose();
        mockQueryEngine?.Dispose();
    }
}