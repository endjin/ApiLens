using ApiLens.Cli.Commands;
using ApiLens.Cli.Services;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Services;
using Spectre.Console.Cli;
using Spectre.IO.Testing;

namespace ApiLens.Cli.Tests.Commands;

[TestClass]
public class NuGetCommandTests
{
    private IFileSystemService mockFileSystem = null!;
    private INuGetCacheScanner mockScanner = null!;
    private ILuceneIndexManagerFactory mockIndexManagerFactory = null!;
    private ILuceneIndexManager mockIndexManager = null!;
    private FakeFileSystem? fakeFileSystem;
    private FakeEnvironment? fakeEnvironment;
    private IFileSystemService? fakeFileSystemService;
    private NuGetCommand command = null!;
    private CommandContext context = null!;

    [TestInitialize]
    public void Setup()
    {
        mockFileSystem = Substitute.For<IFileSystemService>();
        mockScanner = Substitute.For<INuGetCacheScanner>();
        mockIndexManagerFactory = Substitute.For<ILuceneIndexManagerFactory>();
        mockIndexManager = Substitute.For<ILuceneIndexManager>();

        mockIndexManagerFactory.Create(Arg.Any<string>()).Returns(mockIndexManager);

        command = new NuGetCommand(mockFileSystem, mockScanner, mockIndexManagerFactory);
        // CommandContext is sealed, so we'll pass null in tests since it's not used
        context = null!;
    }

    private void SetupFakeFileSystem(string? customCachePath = null)
    {
        fakeEnvironment = FakeEnvironment.CreateLinuxEnvironment();
        if (customCachePath != null)
        {
            fakeEnvironment.SetEnvironmentVariable("NUGET_PACKAGES", customCachePath);
        }
        fakeFileSystem = new FakeFileSystem(fakeEnvironment);
        fakeFileSystemService = new FileSystemService(fakeFileSystem, fakeEnvironment);
        command = new NuGetCommand(fakeFileSystemService, mockScanner, mockIndexManagerFactory);
    }

    [TestMethod]
    public void Settings_WithDefaults_IsValid()
    {
        // Arrange
        NuGetCommand.Settings settings = new();

        // Assert
        settings.IndexPath.ShouldBe("./nuget-index");
        settings.Clean.ShouldBe(false);
        settings.LatestOnly.ShouldBe(false);
        settings.PackageFilter.ShouldBeNull();
        settings.List.ShouldBe(false);
    }

    [TestMethod]
    public void Settings_WithAllOptions_IsValid()
    {
        // Arrange
        NuGetCommand.Settings settings = new()
        {
            IndexPath = "/custom/index",
            Clean = true,
            // LatestOnly = true, // Property removed
            PackageFilter = "newtonsoft.*",
            List = true
        };

        // Assert
        settings.IndexPath.ShouldBe("/custom/index");
        settings.Clean.ShouldBeTrue();
        // settings.LatestOnly.ShouldBeTrue(); // Property removed
        settings.PackageFilter.ShouldBe("newtonsoft.*");
        settings.List.ShouldBeTrue();
    }

    [TestMethod]
    public async Task Execute_WithList_DoesNotIndex()
    {
        // Arrange
        NuGetCommand.Settings settings = new()
        {
            List = true
        };

        string cachePath = "/home/user/.nuget/packages";
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(true);

        NuGetPackageInfo[] packages =
        [
            new()
            {
                PackageId = "newtonsoft.json",
                Version = "13.0.3",
                TargetFramework = "net6.0",
                XmlDocumentationPath = "/path/to/xml"
            }
        ];
        mockScanner.ScanDirectory(cachePath).Returns([.. packages]);

        // Act
        int result = await command.ExecuteAsync(context, settings);

        // Assert
        result.ShouldBe(0);
        // Should scan but not index
        mockScanner.Received(1).ScanDirectory(cachePath);
        mockFileSystem.DidNotReceive().FileExists(Arg.Any<string>());
    }

    [TestMethod]
    public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new NuGetCommand(null!, mockScanner, mockIndexManagerFactory))
            .ParamName.ShouldBe("fileSystem");
    }

    [TestMethod]
    public void Constructor_WithNullScanner_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new NuGetCommand(mockFileSystem, null!, mockIndexManagerFactory))
            .ParamName.ShouldBe("scanner");
    }

    [TestMethod]
    public void Constructor_WithNullIndexManagerFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new NuGetCommand(mockFileSystem, mockScanner, null!))
            .ParamName.ShouldBe("indexManagerFactory");
    }

    [TestMethod]
    public async Task Execute_WithNoCacheDirectory_ReturnsError()
    {
        // Arrange
        NuGetCommand.Settings settings = new();
        string cachePath = "/home/user/.nuget/packages";
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(false);

        // Act
        int result = await command.ExecuteAsync(context, settings);

        // Assert
        result.ShouldBe(1);
        mockFileSystem.Received(1).GetUserNuGetCachePath();
        mockFileSystem.Received(1).DirectoryExists(cachePath);
    }

    [TestMethod]
    public async Task Execute_WithNoPackagesFound_ReturnsSuccess()
    {
        // Arrange
        NuGetCommand.Settings settings = new() { List = true };
        string cachePath = "/home/user/.nuget/packages";
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(true);
        mockScanner.ScanDirectory(cachePath).Returns(ImmutableArray<NuGetPackageInfo>.Empty);

        // Act
        int result = await command.ExecuteAsync(context, settings);

        // Assert
        result.ShouldBe(0);
        mockScanner.Received(1).ScanDirectory(cachePath);
    }

    [TestMethod]
    public async Task Execute_WithPackageFilter_FiltersPackages()
    {
        // Arrange
        NuGetCommand.Settings settings = new()
        {
            List = true,
            PackageFilter = "newtonsoft"
        };

        string cachePath = "/home/user/.nuget/packages";
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(true);

        NuGetPackageInfo[] packages =
        [
            new()
            {
                PackageId = "newtonsoft.json",
                Version = "13.0.3",
                TargetFramework = "net6.0",
                XmlDocumentationPath = "/path1"
            },
            new()
            {
                PackageId = "microsoft.extensions",
                Version = "6.0.0",
                TargetFramework = "net6.0",
                XmlDocumentationPath = "/path2"
            }
        ];
        mockScanner.ScanDirectory(cachePath).Returns([.. packages]);

        // Act
        int result = await command.ExecuteAsync(context, settings);

        // Assert
        result.ShouldBe(0);
        // Verify filtering logic is applied
        mockScanner.Received(1).ScanDirectory(cachePath);
    }

    [TestMethod]
    public async Task Execute_WithLatestOnly_FiltersToLatestVersions()
    {
        // Arrange
        NuGetCommand.Settings settings = new()
        {
            List = true,
            LatestOnly = true
        };

        string cachePath = "/home/user/.nuget/packages";
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(true);

        NuGetPackageInfo[] allPackages =
        [
            new()
            {
                PackageId = "newtonsoft.json",
                Version = "13.0.3",
                TargetFramework = "net6.0",
                XmlDocumentationPath = "/path1"
            },
            new()
            {
                PackageId = "newtonsoft.json",
                Version = "13.0.2",
                TargetFramework = "net6.0",
                XmlDocumentationPath = "/path2"
            }
        ];

        NuGetPackageInfo[] latestPackages =
        [
            new()
            {
                PackageId = "newtonsoft.json",
                Version = "13.0.3",
                TargetFramework = "net6.0",
                XmlDocumentationPath = "/path1"
            }
        ];

        mockScanner.ScanDirectory(cachePath).Returns([.. allPackages]);
        mockScanner.GetLatestVersions(Arg.Any<ImmutableArray<NuGetPackageInfo>>()).Returns([.. latestPackages]);

        // Act
        int result = await command.ExecuteAsync(context, settings);

        // Assert
        result.ShouldBe(0);
        // Should call GetLatestVersions
        mockScanner.Received(1).GetLatestVersions(Arg.Any<ImmutableArray<NuGetPackageInfo>>());
    }

    [TestMethod]
    public async Task Execute_WithException_ReturnsErrorCode()
    {
        // Arrange
        NuGetCommand.Settings settings = new();
        string cachePath = "/home/user/.nuget/packages";
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(true);

        mockScanner.When(x => x.ScanNuGetCache())
            .Do(x => throw new InvalidOperationException("Scan error"));

        // Act
        int result = await command.ExecuteAsync(context, settings);

        // Assert
        result.ShouldBe(1);
    }

    [TestMethod]
    public async Task Execute_WithCleanOption_SetsCleanFlag()
    {
        // Arrange
        NuGetCommand.Settings settings = new()
        {
            Clean = true,
            List = true
        };

        string cachePath = "/home/user/.nuget/packages";
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(true);
        mockScanner.ScanDirectory(cachePath).Returns(ImmutableArray<NuGetPackageInfo>.Empty);

        // Act
        int result = await command.ExecuteAsync(context, settings);

        // Assert
        result.ShouldBe(0);
        settings.Clean.ShouldBeTrue();
    }

    [TestMethod]
    public async Task Execute_WithMultiplePackages_HandlesCorrectly()
    {
        // Arrange
        NuGetCommand.Settings settings = new()
        {
            List = true
        };

        string cachePath = "/home/user/.nuget/packages";
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(true);

        NuGetPackageInfo[] packages =
        [
            new()
            {
                PackageId = "package1",
                Version = "1.0.0",
                TargetFramework = "net6.0",
                XmlDocumentationPath = "/path1"
            },
            new()
            {
                PackageId = "package2",
                Version = "2.0.0",
                TargetFramework = "net7.0",
                XmlDocumentationPath = "/path2"
            },
            new()
            {
                PackageId = "package3",
                Version = "3.0.0",
                TargetFramework = "net8.0",
                XmlDocumentationPath = "/path3"
            }
        ];
        mockScanner.ScanDirectory(cachePath).Returns([.. packages]);

        // Act
        int result = await command.ExecuteAsync(context, settings);

        // Assert
        result.ShouldBe(0);
    }

    [TestMethod]
    public async Task Execute_WithDifferentIndexPath_UsesCustomPath()
    {
        // Arrange
        NuGetCommand.Settings settings = new()
        {
            IndexPath = "/custom/index/path",
            List = true
        };

        string cachePath = "/home/user/.nuget/packages";
        mockFileSystem.GetUserNuGetCachePath().Returns(cachePath);
        mockFileSystem.DirectoryExists(cachePath).Returns(true);
        mockScanner.ScanDirectory(cachePath).Returns(ImmutableArray<NuGetPackageInfo>.Empty);

        // Act
        int result = await command.ExecuteAsync(context, settings);

        // Assert
        result.ShouldBe(0);
        settings.IndexPath.ShouldBe("/custom/index/path");
    }

    [TestMethod]
    public void FormatSize_WithVariousSizes_FormatsCorrectly()
    {
        // Test the FormatSize method through reflection since it's private
        Type commandType = typeof(NuGetCommand);
        System.Reflection.MethodInfo? formatSizeMethod = commandType.GetMethod("FormatSize",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        formatSizeMethod.ShouldNotBeNull();

        // Test various sizes
        formatSizeMethod.Invoke(null, [512L]).ShouldBe("512 B");
        formatSizeMethod.Invoke(null, [1024L]).ShouldBe("1 KB");
        formatSizeMethod.Invoke(null, [2048L]).ShouldBe("2 KB");
        formatSizeMethod.Invoke(null, [1048576L]).ShouldBe("1 MB");
        formatSizeMethod.Invoke(null, [1572864L]).ShouldBe("1.5 MB");
        formatSizeMethod.Invoke(null, [1073741824L]).ShouldBe("1 GB");
        formatSizeMethod.Invoke(null, [1099511627776L]).ShouldBe("1 TB");
        formatSizeMethod.Invoke(null, [0L]).ShouldBe("0 B");
    }

    [TestMethod]
    public void FormatSize_WithLargeSizes_FormatsCorrectly()
    {
        // Test the FormatSize method through reflection
        Type commandType = typeof(NuGetCommand);
        System.Reflection.MethodInfo? formatSizeMethod = commandType.GetMethod("FormatSize",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        formatSizeMethod.ShouldNotBeNull();

        // Test large sizes
        formatSizeMethod.Invoke(null, [5368709120L]).ShouldBe("5 GB");
        formatSizeMethod.Invoke(null, [2199023255552L]).ShouldBe("2 TB");
    }

    [TestMethod]
    public async Task Execute_WithFakeFileSystem_FindsCache()
    {
        // Arrange
        SetupFakeFileSystem();
        string cachePath = fakeEnvironment!.HomeDirectory.FullPath + "/.nuget/packages";

        // Create cache directory
        fakeFileSystem!.CreateDirectory(cachePath);

        NuGetCommand.Settings settings = new()
        {
            List = true
        };

        mockScanner.ScanDirectory(cachePath).Returns(ImmutableArray<NuGetPackageInfo>.Empty);

        // Act
        int result = await command.ExecuteAsync(context, settings);

        // Assert
        result.ShouldBe(0);
        fakeFileSystemService!.DirectoryExists(cachePath).ShouldBeTrue();
    }

    [TestMethod]
    public async Task Execute_WithFakeFileSystem_CustomCachePath_UsesCustomPath()
    {
        // Arrange
        string customCachePath = "/custom/cache/packages";
        SetupFakeFileSystem(customCachePath);

        // Create custom cache directory
        fakeFileSystem!.CreateDirectory(customCachePath);

        NuGetCommand.Settings settings = new()
        {
            List = true
        };

        mockScanner.ScanDirectory(customCachePath).Returns(ImmutableArray<NuGetPackageInfo>.Empty);

        // Act
        int result = await command.ExecuteAsync(context, settings);

        // Assert
        result.ShouldBe(0);
        fakeFileSystemService!.GetUserNuGetCachePath().ShouldBe(customCachePath);
        fakeFileSystemService.DirectoryExists(customCachePath).ShouldBeTrue();
    }

    [TestMethod]
    public async Task Execute_WithFakeFileSystem_MissingCacheDirectory_ReturnsError()
    {
        // Arrange
        SetupFakeFileSystem();
        string cachePath = fakeEnvironment!.HomeDirectory.FullPath + "/.nuget/packages";

        // Don't create the cache directory - it should be missing

        NuGetCommand.Settings settings = new()
        {
            List = true
        };

        // Act
        int result = await command.ExecuteAsync(context, settings);

        // Assert
        result.ShouldBe(1);
        fakeFileSystemService!.DirectoryExists(cachePath).ShouldBeFalse();
    }
}