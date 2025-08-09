using ApiLens.Core.Models;
using ApiLens.Core.Services;

namespace ApiLens.Core.Tests.Services;

[TestClass]
public class ProjectAnalysisServiceTests
{
    private IProjectAnalysisService service = null!;
    private ISolutionParserService solutionParser = null!;
    private IProjectParserService projectParser = null!;
    private IAssetFileParserService assetParser = null!;
    private IFileSystemService fileSystem = null!;

    [TestInitialize]
    public void Setup()
    {
        solutionParser = Substitute.For<ISolutionParserService>();
        projectParser = Substitute.For<IProjectParserService>();
        assetParser = Substitute.For<IAssetFileParserService>();
        fileSystem = Substitute.For<IFileSystemService>();

        service = new ProjectAnalysisService(solutionParser, projectParser, assetParser, fileSystem);
    }

    [TestMethod]
    public async Task AnalyzeAsync_SingleProject_ReturnsCorrectAnalysis()
    {
        // Arrange
        string projectPath = "/test/MyProject.csproj";
        fileSystem.FileExists(projectPath).Returns(true);

        List<PackageReference> packages =
        [
            new() { Id = "Newtonsoft.Json", Version = "13.0.1" },
            new() { Id = "Serilog", Version = "2.10.0" }
        ];

        projectParser.GetPackageReferencesAsync(projectPath).Returns(packages);
        projectParser.GetTargetFrameworksAsync(projectPath).Returns(new[] { "net6.0" });

        // Act
        ProjectAnalysisResult result = await service.AnalyzeAsync(projectPath);

        // Assert
        result.ShouldNotBeNull();
        result.Path.ShouldBe(projectPath);
        result.Type.ShouldBe(ProjectType.CsProj);
        result.Packages.Count.ShouldBe(2);
        result.Frameworks.Count.ShouldBe(1);
        result.Statistics["TotalProjects"].ShouldBe(1);
        result.Statistics["TotalPackages"].ShouldBe(2);
        result.Statistics["DirectPackages"].ShouldBe(2);
        result.Statistics["TransitivePackages"].ShouldBe(0);
    }

    [TestMethod]
    public async Task AnalyzeAsync_Solution_AnalyzesAllProjects()
    {
        // Arrange
        string solutionPath = "/test/MySolution.sln";
        fileSystem.FileExists(solutionPath).Returns(true);

        SolutionInfo solutionInfo = new()
        {
            Path = solutionPath,
            Projects =
            [
                new() { Name = "Project1", Path = "/test/Project1.csproj" },
                new() { Name = "Project2", Path = "/test/Project2.csproj" }
            ]
        };

        solutionParser.ParseSolutionAsync(solutionPath).Returns(solutionInfo);

        projectParser.GetPackageReferencesAsync("/test/Project1.csproj")
            .Returns(new List<PackageReference> { new() { Id = "Package1", Version = "1.0.0" } });

        projectParser.GetPackageReferencesAsync("/test/Project2.csproj")
            .Returns(new List<PackageReference> { new() { Id = "Package2", Version = "2.0.0" } });

        projectParser.GetTargetFrameworksAsync(Arg.Any<string>())
            .Returns(new[] { "net6.0" });

        // Act
        ProjectAnalysisResult result = await service.AnalyzeAsync(solutionPath);

        // Assert
        result.ShouldNotBeNull();
        result.Type.ShouldBe(ProjectType.Solution);
        result.ProjectPaths.Count.ShouldBe(2);
        result.Packages.Count.ShouldBe(2);
        result.Statistics["TotalProjects"].ShouldBe(2);
        result.Statistics["TotalPackages"].ShouldBe(2);
    }

    [TestMethod]
    public async Task AnalyzeAsync_WithAssets_MergesVersions()
    {
        // Arrange
        string projectPath = "/test/MyProject.csproj";
        fileSystem.FileExists(projectPath).Returns(true);

        List<PackageReference> packages =
        [
            new() { Id = "Package1" } // No version specified
        ];

        ProjectAssets assets = new()
        {
            HasAssets = true,
            Packages =
            [
                new() { Id = "Package1", Version = "1.2.3" }
            ],
            TargetFrameworks = ["net6.0"]
        };

        projectParser.GetPackageReferencesAsync(projectPath).Returns(packages);
        projectParser.GetTargetFrameworksAsync(projectPath).Returns(Array.Empty<string>());
        assetParser.ParseProjectAssetsAsync(projectPath).Returns(assets);

        // Act
        ProjectAnalysisResult result = await service.AnalyzeAsync(projectPath, useAssetsFile: true);

        // Assert
        result.ShouldNotBeNull();
        result.Packages.Count.ShouldBe(1);
        result.Packages[0].Version.ShouldBe("1.2.3");
        result.Frameworks.ShouldContain("net6.0");
    }

    [TestMethod]
    public async Task AnalyzeAsync_WithTransitiveDependencies_IncludesThem()
    {
        // Arrange
        string projectPath = "/test/MyProject.csproj";
        fileSystem.FileExists(projectPath).Returns(true);

        List<PackageReference> packages =
        [
            new() { Id = "DirectPackage", Version = "1.0.0" }
        ];

        ProjectAssets assets = new()
        {
            HasAssets = true,
            Packages =
            [
                new() { Id = "DirectPackage", Version = "1.0.0" },
                new() { Id = "TransitivePackage", Version = "2.0.0" }
            ],
            TargetFrameworks = ["net6.0"]
        };

        projectParser.GetPackageReferencesAsync(projectPath).Returns(packages);
        projectParser.GetTargetFrameworksAsync(projectPath).Returns(Array.Empty<string>());
        assetParser.ParseProjectAssetsAsync(projectPath).Returns(assets);

        // Act
        ProjectAnalysisResult result = await service.AnalyzeAsync(projectPath, includeTransitive: true, useAssetsFile: true);

        // Assert
        result.ShouldNotBeNull();
        result.Packages.Count.ShouldBe(2);
        result.Statistics["DirectPackages"].ShouldBe(1);
        result.Statistics["TransitivePackages"].ShouldBe(1);
    }

    [TestMethod]
    public void IsProjectOrSolution_ValidExtensions_ReturnsTrue()
    {
        // Act & Assert
        service.IsProjectOrSolution("test.sln").ShouldBeTrue();
        service.IsProjectOrSolution("test.csproj").ShouldBeTrue();
        service.IsProjectOrSolution("test.fsproj").ShouldBeTrue();
        service.IsProjectOrSolution("test.vbproj").ShouldBeTrue();
    }

    [TestMethod]
    public void IsProjectOrSolution_InvalidExtensions_ReturnsFalse()
    {
        // Act & Assert
        service.IsProjectOrSolution("test.txt").ShouldBeFalse();
        service.IsProjectOrSolution("test.xml").ShouldBeFalse();
        service.IsProjectOrSolution("").ShouldBeFalse();
        service.IsProjectOrSolution(null!).ShouldBeFalse();
    }

    [TestMethod]
    public void GetProjectType_ValidExtensions_ReturnsCorrectType()
    {
        // Act & Assert
        service.GetProjectType("test.sln").ShouldBe(ProjectType.Solution);
        service.GetProjectType("test.csproj").ShouldBe(ProjectType.CsProj);
        service.GetProjectType("test.fsproj").ShouldBe(ProjectType.FsProj);
        service.GetProjectType("test.vbproj").ShouldBe(ProjectType.VbProj);
    }

    [TestMethod]
    public void GetProjectType_InvalidExtension_ThrowsException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetProjectType("test.txt"));
    }
}