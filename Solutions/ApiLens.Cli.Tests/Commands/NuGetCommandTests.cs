using ApiLens.Cli.Commands;
using ApiLens.Cli.Services;
using ApiLens.Cli.Tests.Helpers;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Spectre.IO.Testing;

namespace ApiLens.Cli.Tests.Commands;

[TestClass]
public sealed class NuGetCommandTests : IDisposable
{
    private IFileSystemService mockFileSystem = null!;
    private INuGetCacheScanner mockScanner = null!;
    private IPackageDeduplicationService mockDeduplicationService = null!;
    private IIndexPathResolver mockIndexPathResolver = null!;
    private ILuceneIndexManagerFactory mockIndexManagerFactory = null!;
    private ILuceneIndexManager mockIndexManager = null!;
    private FakeFileSystem? fakeFileSystem;
    private FakeEnvironment? fakeEnvironment;
    private IFileSystemService? fakeFileSystemService;
    private NuGetCommand command = null!;
    private CommandContext context = null!;
    private TestConsole console = null!;

    [TestInitialize]
    public void Setup()
    {
        mockFileSystem = Substitute.For<IFileSystemService>();
        mockScanner = Substitute.For<INuGetCacheScanner>();
        mockDeduplicationService = Substitute.For<IPackageDeduplicationService>();
        mockIndexPathResolver = Substitute.For<IIndexPathResolver>();
        mockIndexManagerFactory = Substitute.For<ILuceneIndexManagerFactory>();
        mockIndexManager = Substitute.For<ILuceneIndexManager>();

        mockIndexPathResolver.ResolveIndexPath(Arg.Any<string>()).Returns(info => info.Arg<string>() ?? "./index");
        mockIndexManagerFactory.Create(Arg.Any<string>()).Returns(mockIndexManager);

        // CommandContext is sealed, so we'll pass null in tests since it's not used
        context = null!;

        console = new TestConsole();
        console.Profile.Width = 120;
        console.Profile.Height = 40;

        command = new NuGetCommand(mockFileSystem, mockScanner, mockDeduplicationService, mockIndexManagerFactory, mockIndexPathResolver, console);
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
        command = new NuGetCommand(fakeFileSystemService, mockScanner, mockDeduplicationService, mockIndexManagerFactory, mockIndexPathResolver, console);
    }

    [TestMethod]
    public void Settings_WithDefaults_IsValid()
    {
        // Arrange
        NuGetCommand.Settings settings = new();

        // Assert
        settings.IndexPath.ShouldBe("./index");
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
                XmlDocumentationPath = "/home/user/.nuget/packages/newtonsoft.json/13.0.3/lib/net6.0/Newtonsoft.Json.xml"
            }
        ];
        mockScanner.ScanDirectory(cachePath).Returns([.. packages]);
        mockScanner.ScanDirectoryAsync(cachePath, Arg.Any<CancellationToken>(), Arg.Any<IProgress<int>?>())
            .Returns(Task.FromResult(ImmutableArray.Create(packages)));

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldNotContain("Error:");
        result.ShouldBe(0);
        // Should scan but not index
        await mockScanner.Received(1).ScanDirectoryAsync(cachePath, Arg.Any<CancellationToken>(), Arg.Any<IProgress<int>?>());
        mockFileSystem.DidNotReceive().FileExists(Arg.Any<string>());
    }

    [TestMethod]
    public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new NuGetCommand(null!, mockScanner, mockDeduplicationService, mockIndexManagerFactory, mockIndexPathResolver, console))
            .ParamName.ShouldBe("fileSystem");
    }

    [TestMethod]
    public void Constructor_WithNullScanner_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new NuGetCommand(mockFileSystem, null!, mockDeduplicationService, mockIndexManagerFactory, mockIndexPathResolver, console))
            .ParamName.ShouldBe("scanner");
    }

    [TestMethod]
    public void Constructor_WithNullDeduplicationService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new NuGetCommand(mockFileSystem, mockScanner, null!, mockIndexManagerFactory, mockIndexPathResolver, console))
            .ParamName.ShouldBe("deduplicationService");
    }

    [TestMethod]
    public void Constructor_WithNullIndexManagerFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new NuGetCommand(mockFileSystem, mockScanner, mockDeduplicationService, null!, mockIndexPathResolver, console))
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
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

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
        mockScanner.ScanDirectoryAsync(cachePath, Arg.Any<CancellationToken>(), Arg.Any<IProgress<int>?>())
            .Returns(Task.FromResult(ImmutableArray<NuGetPackageInfo>.Empty));

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        await mockScanner.Received(1).ScanDirectoryAsync(cachePath, Arg.Any<CancellationToken>(), Arg.Any<IProgress<int>?>());
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
                XmlDocumentationPath = "/home/user/.nuget/packages/newtonsoft.json/13.0.3/lib/net6.0/Newtonsoft.Json.xml"
            },
            new()
            {
                PackageId = "microsoft.extensions",
                Version = "6.0.0",
                TargetFramework = "net6.0",
                XmlDocumentationPath = "/home/user/.nuget/packages/microsoft.extensions/6.0.0/lib/net6.0/Microsoft.Extensions.xml"
            }
        ];

        // Setup mocks with helper methods
        mockScanner.SetupScannerWithPackages(cachePath, packages);
        // Filter should only return newtonsoft.json
        NuGetPackageInfo[] filteredPackages = [packages[0]];
        mockDeduplicationService.SetupPassThroughDeduplication(filteredPackages);

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        // Verify async method was called
        await mockScanner.Received(1).ScanDirectoryAsync(cachePath, Arg.Any<CancellationToken>(), Arg.Any<IProgress<int>?>());
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
                XmlDocumentationPath = "/home/user/.nuget/packages/newtonsoft.json/13.0.3/lib/net6.0/Newtonsoft.Json.xml"
            },
            new()
            {
                PackageId = "newtonsoft.json",
                Version = "13.0.2",
                TargetFramework = "net6.0",
                XmlDocumentationPath = "/home/user/.nuget/packages/newtonsoft.json/13.0.2/lib/net6.0/Newtonsoft.Json.xml"
            }
        ];

        NuGetPackageInfo[] latestPackages =
        [
            new()
            {
                PackageId = "newtonsoft.json",
                Version = "13.0.3",
                TargetFramework = "net6.0",
                XmlDocumentationPath = "/home/user/.nuget/packages/newtonsoft.json/13.0.3/lib/net6.0/Newtonsoft.Json.xml"
            }
        ];

        TestMockSetup.SetupScannerMock(mockScanner, cachePath, [.. allPackages]);
        mockScanner.GetLatestVersions(Arg.Any<ImmutableArray<NuGetPackageInfo>>()).Returns([.. latestPackages]);

        // Setup deduplication to return latest packages
        TestMockSetup.SetupDeduplicationMock(
            mockDeduplicationService,
            latestPackages,
            new HashSet<string>(),
            allPackages.Length - latestPackages.Length,
            new ApiLens.Core.Services.DeduplicationStats
            {
                TotalScannedPackages = allPackages.Length,
                UniqueXmlFiles = latestPackages.Length,
                EmptyXmlFilesSkipped = 0,
                AlreadyIndexedSkipped = 0,
                NewPackages = latestPackages.Length,
                UpdatedPackages = 0
            });

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldNotContain("Error:");
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
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

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
        mockScanner.ScanDirectoryAsync(cachePath, Arg.Any<CancellationToken>(), Arg.Any<IProgress<int>?>())
            .Returns(Task.FromResult(ImmutableArray<NuGetPackageInfo>.Empty));

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldNotContain("Error:");
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
                XmlDocumentationPath = "/home/user/.nuget/packages/newtonsoft.json/13.0.3/lib/net6.0/Newtonsoft.Json.xml"
            },
            new()
            {
                PackageId = "package2",
                Version = "2.0.0",
                TargetFramework = "net7.0",
                XmlDocumentationPath = "/home/user/.nuget/packages/microsoft.extensions/6.0.0/lib/net6.0/Microsoft.Extensions.xml"
            },
            new()
            {
                PackageId = "package3",
                Version = "3.0.0",
                TargetFramework = "net8.0",
                XmlDocumentationPath = "/home/user/.nuget/packages/package3/3.0.0/lib/net8.0/Package3.xml"
            }
        ];
        mockScanner.ScanDirectory(cachePath).Returns([.. packages]);
        mockScanner.ScanDirectoryAsync(cachePath, Arg.Any<CancellationToken>(), Arg.Any<IProgress<int>?>())
            .Returns(Task.FromResult(ImmutableArray.Create(packages)));

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

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
        mockScanner.ScanDirectoryAsync(cachePath, Arg.Any<CancellationToken>(), Arg.Any<IProgress<int>?>())
            .Returns(Task.FromResult(ImmutableArray<NuGetPackageInfo>.Empty));

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

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
        mockScanner.ScanDirectoryAsync(cachePath, Arg.Any<CancellationToken>(), Arg.Any<IProgress<int>?>())
            .Returns(Task.FromResult(ImmutableArray<NuGetPackageInfo>.Empty));

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

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
        mockScanner.ScanDirectoryAsync(customCachePath, Arg.Any<CancellationToken>(), Arg.Any<IProgress<int>?>())
            .Returns(Task.FromResult(ImmutableArray<NuGetPackageInfo>.Empty));

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

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
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        fakeFileSystemService!.DirectoryExists(cachePath).ShouldBeFalse();
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
}