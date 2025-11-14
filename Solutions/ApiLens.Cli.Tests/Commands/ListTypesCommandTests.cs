using ApiLens.Cli.Commands;
using ApiLens.Cli.Services;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;
using Spectre.Console;
using Spectre.Console.Testing;
namespace ApiLens.Cli.Tests.Commands;

[TestClass]
public class ListTypesCommandTests : IDisposable
{
    private ListTypesCommand command = null!;
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
        command = new ListTypesCommand(indexManagerFactory, indexPathResolver, queryEngineFactory);
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
        var settings = new ListTypesCommand.Settings
        {
            Package = "TestPackage"
        };

        // Assert
        settings.Distinct.ShouldBeTrue();
        settings.MaxResults.ShouldBe(100);
        settings.Format.ShouldBe(OutputFormat.Table);
        settings.GroupBy.ShouldBe(ListTypesCommand.GroupByOption.Assembly);
    }

    [TestMethod]
    public void Execute_RequiresAtLeastOneFilter()
    {
        // Arrange
        var settings = new ListTypesCommand.Settings
        {
            // No filters provided
            IndexPath = "./test-index"
        };

        // Act
        int result = command.Execute(null!, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        var output = console.Output;
        output.ShouldContain("At least one filter");
    }

    [TestMethod]
    public void Execute_WithPackageFilter_AppliesDeduplication()
    {
        // Arrange
        var type1Net8 = CreateMemberInfo("Type1", "net8.0", "TestPackage");
        var type1Net9 = CreateMemberInfo("Type1", "net9.0", "TestPackage");
        var type2 = CreateMemberInfo("Type2", "net9.0", "TestPackage");

        mockQueryEngine.ListTypesFromPackage("TestPackage", 100)
            .Returns(new List<MemberInfo> { type1Net8, type1Net9, type2 });

        var settings = new ListTypesCommand.Settings
        {
            Package = "TestPackage",
            Distinct = true,
            IndexPath = "./test-index"
        };

        // Act
        int result = command.Execute(null!, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        var output = console.Output;
        output.ShouldContain("Type1");
        output.ShouldContain("Type2");
        output.ShouldContain("Found 2 types"); // Deduplicated from 3 to 2
    }

    [TestMethod]
    public void Execute_WithoutDistinct_ShowsAllVersions()
    {
        // Arrange
        var type1Net8 = CreateMemberInfo("Type1", "net8.0", "TestPackage");
        var type1Net9 = CreateMemberInfo("Type1", "net9.0", "TestPackage");

        mockQueryEngine.ListTypesFromPackage("TestPackage", 100)
            .Returns(new List<MemberInfo> { type1Net8, type1Net9 });

        var settings = new ListTypesCommand.Settings
        {
            Package = "TestPackage",
            Distinct = false,
            IndexPath = "./test-index"
        };

        // Act
        int result = command.Execute(null!, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        var output = console.Output;
        output.ShouldContain("Type1");
        output.ShouldContain("Found 2 types"); // Not deduplicated
    }

    [TestMethod]
    public void Execute_WithNamespaceFilter_FiltersCorrectly()
    {
        // Arrange
        var types = new List<MemberInfo>
        {
            CreateMemberInfo("Type1", "net9.0", null, "TestNamespace"),
            CreateMemberInfo("Type2", "net9.0", null, "TestNamespace")
        };

        mockQueryEngine.SearchByNamespace("TestNamespace", 1000)
            .Returns(types);

        var settings = new ListTypesCommand.Settings
        {
            Namespace = "TestNamespace",
            IndexPath = "./test-index"
        };

        // Act
        int result = command.Execute(null!, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        var output = console.Output;
        output.ShouldContain("Type1");
        output.ShouldContain("Type2");
    }

    [TestMethod]
    public void Execute_WithAssemblyFilter_FiltersCorrectly()
    {
        // Arrange
        var types = new List<MemberInfo>
        {
            CreateMemberInfo("Type1", "net9.0"),
            CreateMemberInfo("Type2", "net9.0")
        };

        mockQueryEngine.ListTypesFromAssembly("TestAssembly", 100)
            .Returns(types);

        var settings = new ListTypesCommand.Settings
        {
            Assembly = "TestAssembly",
            IndexPath = "./test-index"
        };

        // Act
        int result = command.Execute(null!, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        var output = console.Output;
        output.ShouldContain("Type1");
        output.ShouldContain("Type2");
    }

    [TestMethod]
    public void Execute_WithJsonFormat_OutputsJson()
    {
        // Arrange
        var type = CreateMemberInfo("TestType", "net9.0", "TestPackage");

        mockQueryEngine.ListTypesFromPackage("TestPackage", 100)
            .Returns(new List<MemberInfo> { type });

        var settings = new ListTypesCommand.Settings
        {
            Package = "TestPackage",
            Format = OutputFormat.Json,
            IndexPath = "./test-index"
        };

        // Act
        int result = command.Execute(null!, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        var output = console.Output;
        output.ShouldContain("{");
        output.ShouldContain("\"results\"");
        output.ShouldContain("TestType");
    }

    [TestMethod]
    public void Execute_GroupsBySelectedOption()
    {
        // Arrange
        var type1 = CreateMemberInfo("Type1", "net9.0", "Package1", "Namespace1");
        var type2 = CreateMemberInfo("Type2", "net9.0", "Package2", "Namespace2");

        mockQueryEngine.SearchByNamespacePattern("Test.*", 1000)
            .Returns(new List<MemberInfo> { type1, type2 });

        var settings = new ListTypesCommand.Settings
        {
            Namespace = "Test.*",
            GroupBy = ListTypesCommand.GroupByOption.Package,
            IndexPath = "./test-index"
        };

        // Act
        int result = command.Execute(null!, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        var output = console.Output;
        output.ShouldContain("Package1");
        output.ShouldContain("Package2");
    }

    [TestMethod]
    public void Execute_WithIncludeMembers_ShowsAllMemberTypes()
    {
        // Arrange
        var type = CreateMemberInfo("TestType", "net9.0", "TestPackage");
        var method = new MemberInfo
        {
            Id = "M:TestMethod",
            Name = "TestMethod",
            FullName = "TestNamespace.TestType.TestMethod",
            MemberType = MemberType.Method,
            Assembly = "TestAssembly",
            Namespace = "TestNamespace",
            PackageId = "TestPackage"
        };

        mockQueryEngine.SearchByPackage("TestPackage", 100)
            .Returns(new List<MemberInfo> { type, method });

        var settings = new ListTypesCommand.Settings
        {
            Package = "TestPackage",
            IncludeMembers = true,
            IndexPath = "./test-index"
        };

        // Act
        int result = command.Execute(null!, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        var output = console.Output;
        output.ShouldContain("TestType");
        output.ShouldContain("TestMethod");
        output.ShouldContain("Found 2 members");
    }

    private static MemberInfo CreateMemberInfo(string name, string? framework = "net9.0",
        string? packageId = null, string? namespaceName = null)
    {
        return new MemberInfo
        {
            Id = $"T:TestNamespace.{name}",
            Name = name,
            FullName = $"{namespaceName ?? "TestNamespace"}.{name}",
            MemberType = MemberType.Type,
            Assembly = "TestAssembly",
            Namespace = namespaceName ?? "TestNamespace",
            TargetFramework = framework,
            PackageId = packageId ?? "TestPackage"
        };
    }

    public void Dispose()
    {
        console?.Dispose();
        mockIndexManager?.Dispose();
        mockQueryEngine?.Dispose();
    }
}