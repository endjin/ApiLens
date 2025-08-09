using ApiLens.Core.Services;

namespace ApiLens.Core.Tests.Services;

[TestClass]
public class ProjectParserServiceTests
{
    private IProjectParserService service = null!;
    private IFileSystemService fileSystem = null!;

    [TestInitialize]
    public void Setup()
    {
        fileSystem = Substitute.For<IFileSystemService>();
        service = new ProjectParserService(fileSystem);
    }

    [TestMethod]
    public async Task GetPackageReferencesAsync_SdkStyleProject_ReturnsPackages()
    {
        // Arrange
        string projectPath = "/test/MyProject.csproj";
        string projectContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net6.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                <PackageReference Include="Serilog" Version="2.10.0">
                  <PrivateAssets>all</PrivateAssets>
                </PackageReference>
              </ItemGroup>
            </Project>
            """;

        fileSystem.FileExists(projectPath).Returns(true);
        fileSystem.ReadAllTextAsync(projectPath).Returns(Task.FromResult(projectContent));

        // Act
        IEnumerable<Core.Models.PackageReference> result = await service.GetPackageReferencesAsync(projectPath);

        // Assert
        result.ShouldNotBeNull();
        List<Core.Models.PackageReference> packages = [.. result];
        packages.Count.ShouldBe(2);

        packages[0].Id.ShouldBe("Newtonsoft.Json");
        packages[0].Version.ShouldBe("13.0.1");

        packages[1].Id.ShouldBe("Serilog");
        packages[1].Version.ShouldBe("2.10.0");
        packages[1].PrivateAssets.ShouldBe("all");
    }

    [TestMethod]
    public async Task GetPackageReferencesAsync_ProjectWithPackagesConfig_ReturnsAllPackages()
    {
        // Arrange
        string projectPath = "/test/MyProject.csproj";
        string projectContent = """
            <Project>
              <PropertyGroup>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
              </PropertyGroup>
            </Project>
            """;

        string packagesConfigPath = "/test/packages.config";
        string packagesConfigContent = """
            <?xml version="1.0" encoding="utf-8"?>
            <packages>
              <package id="EntityFramework" version="6.4.4" targetFramework="net472" />
              <package id="System.Data.SqlClient" version="4.8.3" targetFramework="net472" />
            </packages>
            """;

        fileSystem.FileExists(projectPath).Returns(true);
        fileSystem.ReadAllTextAsync(projectPath).Returns(Task.FromResult(projectContent));
        fileSystem.GetDirectoryName(projectPath).Returns("/test");
        fileSystem.CombinePath("/test", "packages.config").Returns(packagesConfigPath);
        fileSystem.FileExists(packagesConfigPath).Returns(true);
        fileSystem.ReadAllTextAsync(packagesConfigPath).Returns(Task.FromResult(packagesConfigContent));

        // Act
        IEnumerable<Core.Models.PackageReference> result = await service.GetPackageReferencesAsync(projectPath);

        // Assert
        result.ShouldNotBeNull();
        List<Core.Models.PackageReference> packages = [.. result];
        packages.Count.ShouldBe(2);

        packages[0].Id.ShouldBe("EntityFramework");
        packages[0].Version.ShouldBe("6.4.4");
        packages[0].TargetFramework.ShouldBe("net472");
    }

    [TestMethod]
    public async Task GetPackageReferencesAsync_ProjectNotFound_ThrowsException()
    {
        // Arrange
        string projectPath = "/test/NotFound.csproj";
        fileSystem.FileExists(projectPath).Returns(false);

        // Act & Assert
        await Should.ThrowAsync<FileNotFoundException>(async () =>
            await service.GetPackageReferencesAsync(projectPath));
    }

    [TestMethod]
    public async Task GetTargetFrameworksAsync_SingleFramework_ReturnsOne()
    {
        // Arrange
        string projectPath = "/test/MyProject.csproj";
        string projectContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net6.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;

        fileSystem.FileExists(projectPath).Returns(true);
        fileSystem.ReadAllTextAsync(projectPath).Returns(Task.FromResult(projectContent));

        // Act
        IEnumerable<string> result = await service.GetTargetFrameworksAsync(projectPath);

        // Assert
        result.ShouldNotBeNull();
        result.Count().ShouldBe(1);
        result.First().ShouldBe("net6.0");
    }

    [TestMethod]
    public async Task GetTargetFrameworksAsync_MultipleFrameworks_ReturnsAll()
    {
        // Arrange
        string projectPath = "/test/MyProject.csproj";
        string projectContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net6.0;net7.0;net8.0</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """;

        fileSystem.FileExists(projectPath).Returns(true);
        fileSystem.ReadAllTextAsync(projectPath).Returns(Task.FromResult(projectContent));

        // Act
        IEnumerable<string> result = await service.GetTargetFrameworksAsync(projectPath);

        // Assert
        result.ShouldNotBeNull();
        List<string> frameworks = [.. result];
        frameworks.Count.ShouldBe(3);
        frameworks.ShouldContain("net6.0");
        frameworks.ShouldContain("net7.0");
        frameworks.ShouldContain("net8.0");
    }

    [TestMethod]
    public async Task GetTargetFrameworksAsync_LegacyProject_ConvertsVersion()
    {
        // Arrange
        string projectPath = "/test/MyProject.csproj";
        string projectContent = """
            <Project>
              <PropertyGroup>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
              </PropertyGroup>
            </Project>
            """;

        fileSystem.FileExists(projectPath).Returns(true);
        fileSystem.ReadAllTextAsync(projectPath).Returns(Task.FromResult(projectContent));

        // Act
        IEnumerable<string> result = await service.GetTargetFrameworksAsync(projectPath);

        // Assert
        result.ShouldNotBeNull();
        result.Count().ShouldBe(1);
        result.First().ShouldBe("net472");
    }
}