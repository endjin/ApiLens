using ApiLens.Core.Services;

namespace ApiLens.Core.Tests.Services;

[TestClass]
public class SolutionParserServiceTests
{
    private ISolutionParserService service = null!;
    private IFileSystemService fileSystem = null!;

    [TestInitialize]
    public void Setup()
    {
        fileSystem = Substitute.For<IFileSystemService>();
        service = new SolutionParserService(fileSystem);
    }

    [TestMethod]
    public async Task ParseSolutionAsync_ValidSolution_ReturnsProjects()
    {
        // Arrange
        string solutionPath = "/test/MySolution.sln";
        string project1Path = "/test/src/Project1/Project1.csproj";
        string project2Path = "/test/src/Project2/Project2.fsproj";
        string solutionContent = """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project1", "src\Project1\Project1.csproj", "{5819AC13-E147-4DBD-B4E6-CB83826DAB8E}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project2", "src\Project2\Project2.fsproj", "{F6CC102C-947D-4F97-8092-242935FCBD9D}"
            EndProject
            """;

        fileSystem.FileExists(solutionPath).Returns(true);
        fileSystem.GetDirectoryName(solutionPath).Returns("/test");
        fileSystem.CombinePath("/test", @"src\Project1\Project1.csproj").Returns(project1Path);
        fileSystem.CombinePath("/test", @"src\Project2\Project2.fsproj").Returns(project2Path);
        fileSystem.FileExists(project1Path).Returns(true);
        fileSystem.FileExists(project2Path).Returns(true);
        fileSystem.ReadAllTextAsync(solutionPath).Returns(Task.FromResult(solutionContent));

        // Act
        Core.Models.SolutionInfo result = await service.ParseSolutionAsync(solutionPath);

        // Assert
        result.ShouldNotBeNull();
        result.Path.ShouldBe(solutionPath);
        result.Projects.Count.ShouldBe(2);
        result.Projects[0].Name.ShouldBe("Project1");
        result.Projects[0].Path.ShouldContain("Project1.csproj");
        result.Projects[1].Name.ShouldBe("Project2");
        result.Projects[1].Path.ShouldContain("Project2.fsproj");
    }

    [TestMethod]
    public async Task ParseSolutionAsync_FileNotFound_ThrowsException()
    {
        // Arrange
        string solutionPath = "/test/NotFound.sln";
        fileSystem.FileExists(solutionPath).Returns(false);

        // Act & Assert
        await Should.ThrowAsync<FileNotFoundException>(async () =>
            await service.ParseSolutionAsync(solutionPath));
    }

    [TestMethod]
    public async Task ParseSolutionAsync_EmptySolution_ReturnsEmptyProjects()
    {
        // Arrange
        string solutionPath = "/test/Empty.sln";
        string solutionContent = """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            """;

        fileSystem.FileExists(solutionPath).Returns(true);
        fileSystem.GetDirectoryName(solutionPath).Returns("/test");
        fileSystem.ReadAllTextAsync(solutionPath).Returns(Task.FromResult(solutionContent));

        // Act
        Core.Models.SolutionInfo result = await service.ParseSolutionAsync(solutionPath);

        // Assert
        result.ShouldNotBeNull();
        result.Projects.Count.ShouldBe(0);
    }

    [TestMethod]
    public async Task GetProjectPathsAsync_ValidSolution_ReturnsProjectPaths()
    {
        // Arrange
        string solutionPath = "/test/MySolution.sln";
        string projectPath = "/test/Project1.csproj";
        string solutionContent = """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project1", "Project1.csproj", "{GUID}"
            EndProject
            """;

        fileSystem.FileExists(solutionPath).Returns(true);
        fileSystem.GetDirectoryName(solutionPath).Returns("/test");
        fileSystem.CombinePath("/test", "Project1.csproj").Returns(projectPath);
        fileSystem.FileExists(projectPath).Returns(true);
        fileSystem.ReadAllTextAsync(solutionPath).Returns(Task.FromResult(solutionContent));

        // Act
        IEnumerable<string> result = await service.GetProjectPathsAsync(solutionPath);

        // Assert
        result.ShouldNotBeNull();
        result.Count().ShouldBe(1);
        result.First().ShouldContain("Project1.csproj");
    }
}