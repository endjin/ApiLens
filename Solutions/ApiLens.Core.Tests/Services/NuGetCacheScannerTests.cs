using ApiLens.Core.Models;
using ApiLens.Core.Services;
using Spectre.IO;
using Spectre.IO.Testing;

namespace ApiLens.Core.Tests.Services;

[TestClass]
public class NuGetCacheScannerTests
{
    private IFileSystemService mockFileSystem = null!;
    private FakeFileSystem? fakeFileSystem;
    private FakeEnvironment? fakeEnvironment;
    private IFileSystemService? fakeFileSystemService;
    private INuGetCacheScanner scanner = null!;

    [TestInitialize]
    public void Setup()
    {
        mockFileSystem = Substitute.For<IFileSystemService>();
        scanner = new NuGetCacheScanner(mockFileSystem);
    }

    private void SetupFakeFileSystem(string? nugetPackagesPath = null)
    {
        fakeEnvironment = FakeEnvironment.CreateLinuxEnvironment();
        if (nugetPackagesPath != null)
        {
            fakeEnvironment.SetEnvironmentVariable("NUGET_PACKAGES", nugetPackagesPath);
        }
        fakeFileSystem = new FakeFileSystem(fakeEnvironment);
        // Use a simplified FileSystemService implementation for Core tests
        fakeFileSystemService = new TestFileSystemService(fakeFileSystem, fakeEnvironment);
        scanner = new NuGetCacheScanner(fakeFileSystemService);
    }

    // Simplified FileSystemService implementation for Core tests
    private class TestFileSystemService : IFileSystemService
    {
        private readonly IFileSystem fileSystem;
        private readonly IEnvironment environment;

        public TestFileSystemService(IFileSystem fileSystem, IEnvironment environment)
        {
            this.fileSystem = fileSystem;
            this.environment = environment;
        }

        public bool FileExists(string path)
        {
            FilePath filePath = new(path);
            return fileSystem.Exist(filePath);
        }

        public bool DirectoryExists(string path)
        {
            DirectoryPath dirPath = new(path);
            return fileSystem.Exist(dirPath);
        }

        public IEnumerable<string> GetFiles(string path, string pattern, bool recursive)
        {
            DirectoryPath dirPath = new(path);
            if (!fileSystem.Exist(dirPath))
                return [];

            IDirectory directory = fileSystem.GetDirectory(dirPath);
            SearchScope scope = recursive ? SearchScope.Recursive : SearchScope.Current;
            return directory.GetFiles(pattern, scope).Select(f => f.Path.FullPath);
        }

        public string CombinePath(params string[] paths)
        {
            DirectoryPath result = new(paths[0]);
            for (int i = 1; i < paths.Length; i++)
            {
                result = result.Combine(paths[i]);
            }
            return result.FullPath;
        }

        public FileInfo GetFileInfo(string path) => new(path);
        public DirectoryInfo GetDirectoryInfo(string path) => new(path);

        public string GetUserNuGetCachePath()
        {
            string? nugetPackages = environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (!string.IsNullOrWhiteSpace(nugetPackages))
            {
                return nugetPackages;
            }

            string homeDirectory = environment.HomeDirectory.FullPath;
            return CombinePath(homeDirectory, ".nuget", "packages");
        }

        public string GetFileName(string path)
        {
            FilePath filePath = new(path);
            return filePath.GetFilename().ToString();
        }

        public string? GetDirectoryName(string path)
        {
            FilePath filePath = new(path);
            DirectoryPath? parent = filePath.GetDirectory();
            return parent?.FullPath;
        }

        public IEnumerable<FileInfo> EnumerateFiles(string path, string? pattern = null, bool recursive = false)
        {
            DirectoryPath dirPath = new(path);
            if (!fileSystem.Exist(dirPath))
                return [];

            IDirectory directory = fileSystem.GetDirectory(dirPath);
            string searchPattern = pattern ?? "*";
            SearchScope scope = recursive ? SearchScope.Recursive : SearchScope.Current;

            return directory.GetFiles(searchPattern, scope)
                .Select(f => new FileInfo(f.Path.FullPath));
        }
    }


    [TestMethod]
    public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new NuGetCacheScanner(null!))
            .ParamName.ShouldBe("fileSystem");
    }

    [TestMethod]
    public void ScanNuGetCache_WithEmptyCache_ReturnsEmptyCollection()
    {
        // Arrange
        SetupFakeFileSystem();
        string cachePath = fakeEnvironment!.HomeDirectory.FullPath + "/.nuget/packages";

        // Create the cache directory but leave it empty
        fakeFileSystem!.CreateDirectory(cachePath);

        // Act
        ImmutableArray<NuGetPackageInfo> result = scanner.ScanNuGetCache();

        // Assert
        result.ShouldBeEmpty();
    }

    [TestMethod]
    public void ScanNuGetCache_WithNonExistentCache_ReturnsEmptyCollection()
    {
        // Arrange
        SetupFakeFileSystem();
        // Don't create the cache directory - it should not exist

        // Act
        ImmutableArray<NuGetPackageInfo> result = scanner.ScanNuGetCache();

        // Assert
        result.ShouldBeEmpty();
    }

    [TestMethod]
    public void ScanNuGetCache_WithSinglePackage_ReturnsPackageInfo()
    {
        // Arrange
        SetupFakeFileSystem();
        string cachePath = fakeEnvironment!.HomeDirectory.FullPath + "/.nuget/packages";

        // Create a realistic NuGet cache structure
        string xmlPath = $"{cachePath}/newtonsoft.json/13.0.1/lib/net6.0/Newtonsoft.Json.xml";
        fakeFileSystem!.CreateFile(xmlPath)
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");

        // Act
        ImmutableArray<NuGetPackageInfo> result = scanner.ScanNuGetCache();

        // Assert
        result.Length.ShouldBe(1);
        NuGetPackageInfo package = result[0];
        package.PackageId.ShouldBe("newtonsoft.json");
        package.Version.ShouldBe("13.0.1");
        package.TargetFramework.ShouldBe("net6.0");
        package.XmlDocumentationPath.ShouldContain("Newtonsoft.Json.xml");
    }

    [TestMethod]
    public void ScanNuGetCache_WithMultipleVersions_ReturnsAllVersions()
    {
        // Arrange
        SetupFakeFileSystem();
        string cachePath = fakeEnvironment!.HomeDirectory.FullPath + "/.nuget/packages";

        // Create multiple versions and frameworks
        fakeFileSystem!.CreateFile($"{cachePath}/newtonsoft.json/13.0.1/lib/net6.0/Newtonsoft.Json.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");
        fakeFileSystem.CreateFile($"{cachePath}/newtonsoft.json/12.0.3/lib/netstandard2.0/Newtonsoft.Json.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");
        fakeFileSystem.CreateFile($"{cachePath}/newtonsoft.json/13.0.1/lib/net5.0/Newtonsoft.Json.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");

        // Act
        ImmutableArray<NuGetPackageInfo> result = scanner.ScanNuGetCache();

        // Assert
        result.Length.ShouldBe(3);
        result.ShouldContain(p => p.Version == "13.0.1" && p.TargetFramework == "net6.0");
        result.ShouldContain(p => p.Version == "12.0.3" && p.TargetFramework == "netstandard2.0");
        result.ShouldContain(p => p.Version == "13.0.1" && p.TargetFramework == "net5.0");
    }

    [TestMethod]
    public void ScanNuGetCache_WithInvalidPaths_SkipsInvalidEntries()
    {
        // Arrange
        SetupFakeFileSystem();
        string cachePath = fakeEnvironment!.HomeDirectory.FullPath + "/.nuget/packages";

        // Create valid and invalid file structures
        fakeFileSystem!.CreateFile($"{cachePath}/newtonsoft.json/13.0.1/lib/net6.0/Newtonsoft.Json.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");
        fakeFileSystem.CreateFile($"{cachePath}/invalid.xml") // Invalid structure
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");
        fakeFileSystem.CreateFile($"{cachePath}/package/version/invalid.xml") // Missing lib folder
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");

        // Act
        ImmutableArray<NuGetPackageInfo> result = scanner.ScanNuGetCache();

        // Assert
        result.Length.ShouldBe(1); // Only valid entry
        result[0].PackageId.ShouldBe("newtonsoft.json");
    }

    [TestMethod]
    public void ScanNuGetCache_WithRefAssembliesPath_ExtractsCorrectInfo()
    {
        // Arrange
        SetupFakeFileSystem();
        string cachePath = fakeEnvironment!.HomeDirectory.FullPath + "/.nuget/packages";

        // Create ref assembly (reference assembly) structure
        fakeFileSystem!.CreateFile($"{cachePath}/system.text.json/6.0.0/ref/net6.0/System.Text.Json.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");

        // Act
        ImmutableArray<NuGetPackageInfo> result = scanner.ScanNuGetCache();

        // Assert
        result.Length.ShouldBe(1);
        NuGetPackageInfo package = result[0];
        package.PackageId.ShouldBe("system.text.json");
        package.Version.ShouldBe("6.0.0");
        package.TargetFramework.ShouldBe("net6.0");
    }

    [TestMethod]
    public void GetLatestVersions_WithMultipleVersions_ReturnsLatestPerFramework()
    {
        // Arrange
        SetupFakeFileSystem();
        string cachePath = fakeEnvironment!.HomeDirectory.FullPath + "/.nuget/packages";

        // Create multiple versions for version comparison testing
        fakeFileSystem!.CreateFile($"{cachePath}/newtonsoft.json/13.0.3/lib/net6.0/Newtonsoft.Json.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");
        fakeFileSystem.CreateFile($"{cachePath}/newtonsoft.json/13.0.1/lib/net6.0/Newtonsoft.Json.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");
        fakeFileSystem.CreateFile($"{cachePath}/newtonsoft.json/12.0.3/lib/net6.0/Newtonsoft.Json.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");
        fakeFileSystem.CreateFile($"{cachePath}/newtonsoft.json/13.0.2/lib/net5.0/Newtonsoft.Json.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");
        fakeFileSystem.CreateFile($"{cachePath}/newtonsoft.json/13.0.1/lib/net5.0/Newtonsoft.Json.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");

        // Act
        ImmutableArray<NuGetPackageInfo> allPackages = scanner.ScanNuGetCache();
        ImmutableArray<NuGetPackageInfo> latestVersions = scanner.GetLatestVersions(allPackages);

        // Assert
        latestVersions.Length.ShouldBe(2); // One per framework
        latestVersions.ShouldContain(p => p.Version == "13.0.3" && p.TargetFramework == "net6.0");
        latestVersions.ShouldContain(p => p.Version == "13.0.2" && p.TargetFramework == "net5.0");
    }

    [TestMethod]
    public void ScanNuGetCache_WithFakeFileSystem_FindsPackages()
    {
        // Arrange
        SetupFakeFileSystem();
        string cachePath = fakeEnvironment!.HomeDirectory.FullPath + "/.nuget/packages";

        // Create a realistic NuGet cache structure
        fakeFileSystem!.CreateFile($"{cachePath}/newtonsoft.json/13.0.3/lib/net6.0/Newtonsoft.Json.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");
        fakeFileSystem.CreateFile($"{cachePath}/newtonsoft.json/13.0.3/lib/net5.0/Newtonsoft.Json.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");
        fakeFileSystem.CreateFile($"{cachePath}/system.text.json/6.0.0/lib/net6.0/System.Text.Json.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");

        // Create some non-XML files that should be ignored
        fakeFileSystem.CreateFile($"{cachePath}/newtonsoft.json/13.0.3/lib/net6.0/Newtonsoft.Json.dll");
        fakeFileSystem.CreateFile($"{cachePath}/some-package/1.0.0/readme.txt");

        // Act
        ImmutableArray<NuGetPackageInfo> result = scanner.ScanNuGetCache();

        // Assert
        result.Length.ShouldBe(3);
        result.ShouldContain(p => p.PackageId == "newtonsoft.json" && p.Version == "13.0.3" && p.TargetFramework == "net6.0");
        result.ShouldContain(p => p.PackageId == "newtonsoft.json" && p.Version == "13.0.3" && p.TargetFramework == "net5.0");
        result.ShouldContain(p => p.PackageId == "system.text.json" && p.Version == "6.0.0" && p.TargetFramework == "net6.0");
    }

    [TestMethod]
    public void ScanNuGetCache_WithFakeFileSystem_EmptyCache_ReturnsEmpty()
    {
        // Arrange
        SetupFakeFileSystem();
        string cachePath = fakeEnvironment!.HomeDirectory.FullPath + "/.nuget/packages";

        // Create the cache directory but leave it empty
        fakeFileSystem!.CreateDirectory(cachePath);

        // Act
        ImmutableArray<NuGetPackageInfo> result = scanner.ScanNuGetCache();

        // Assert
        result.ShouldBeEmpty();
    }

    [TestMethod]
    public void ScanNuGetCache_WithFakeFileSystem_CustomCachePath_FindsPackages()
    {
        // Arrange
        string customCachePath = "/custom/nuget/cache";
        SetupFakeFileSystem(customCachePath);

        // Create packages in custom location
        fakeFileSystem!.CreateFile($"{customCachePath}/serilog/2.10.0/lib/netstandard2.0/Serilog.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");
        fakeFileSystem.CreateFile($"{customCachePath}/serilog/2.10.0/lib/netstandard2.1/Serilog.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");

        // Act
        ImmutableArray<NuGetPackageInfo> result = scanner.ScanNuGetCache();

        // Assert
        result.Length.ShouldBe(2);
        result.All(p => p.PackageId == "serilog").ShouldBeTrue();
        result.All(p => p.Version == "2.10.0").ShouldBeTrue();
    }

    [TestMethod]
    public void ScanNuGetCache_WithFakeFileSystem_RefAssemblies_ParsesCorrectly()
    {
        // Arrange
        SetupFakeFileSystem();
        string cachePath = fakeEnvironment!.HomeDirectory.FullPath + "/.nuget/packages";

        // Create ref assemblies (reference assemblies)
        fakeFileSystem!.CreateFile($"{cachePath}/microsoft.extensions.logging/6.0.0/ref/net6.0/Microsoft.Extensions.Logging.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");

        // Also create lib assemblies for comparison
        fakeFileSystem.CreateFile($"{cachePath}/microsoft.extensions.logging/6.0.0/lib/net6.0/Microsoft.Extensions.Logging.xml")
            .SetTextContent("<?xml version=\"1.0\"?><doc></doc>");

        // Act
        ImmutableArray<NuGetPackageInfo> result = scanner.ScanNuGetCache();

        // Assert
        result.Length.ShouldBe(2);
        result.All(p => p.PackageId == "microsoft.extensions.logging").ShouldBeTrue();
        result.All(p => p.Version == "6.0.0").ShouldBeTrue();
        result.All(p => p.TargetFramework == "net6.0").ShouldBeTrue();
    }
}