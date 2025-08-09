using ApiLens.Cli.Services;
using ApiLens.Core.Services;
using Spectre.IO.Testing;

namespace ApiLens.Cli.Tests.Services;

[TestClass]
public class FileSystemServiceTests
{
    private FakeFileSystem fakeFileSystem = null!;
    private FakeEnvironment fakeEnvironment = null!;
    private IFileSystemService service = null!;

    [TestInitialize]
    public void Setup()
    {
        // Always use FakeFileSystem for more realistic testing
        SetupFakeFileSystem();
    }

    private void SetupFakeFileSystem(bool isWindows = false)
    {
        // Create fake environment based on platform
        fakeEnvironment = isWindows
            ? FakeEnvironment.CreateWindowsEnvironment()
            : FakeEnvironment.CreateLinuxEnvironment();
        fakeFileSystem = new FakeFileSystem(fakeEnvironment);
        service = new FileSystemService(fakeFileSystem, fakeEnvironment);
    }

    [TestMethod]
    public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new FileSystemService(null!, fakeEnvironment)).ParamName.ShouldBe("fileSystem");
    }

    [TestMethod]
    public void Constructor_WithNullEnvironment_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new FileSystemService(fakeFileSystem, null!)).ParamName.ShouldBe("environment");
    }

    [TestMethod]
    public void FileExists_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        string path = "/test/existing.txt";
        fakeFileSystem.CreateFile(path).SetTextContent("test");

        // Act
        bool exists = service.FileExists(path);

        // Assert
        exists.ShouldBeTrue();
    }

    [TestMethod]
    public void FileExists_WithNonExistingFile_ReturnsFalse()
    {
        // Arrange
        string path = "/test/missing.txt";
        // Don't create the file - it should not exist

        // Act
        bool exists = service.FileExists(path);

        // Assert
        exists.ShouldBeFalse();
    }

    [TestMethod]
    public void FileExists_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.FileExists(null!));
    }

    [TestMethod]
    public void FileExists_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.FileExists(""));
    }

    [TestMethod]
    public void DirectoryExists_WithExistingDirectory_ReturnsTrue()
    {
        // Arrange
        string path = "/test/existing";
        fakeFileSystem.CreateDirectory(path);

        // Act
        bool exists = service.DirectoryExists(path);

        // Assert
        exists.ShouldBeTrue();
    }

    [TestMethod]
    public void GetUserNuGetCachePath_WithEnvironmentVariable_ReturnsEnvironmentPath()
    {
        // Arrange
        string customPath = "/custom/nuget/packages";
        fakeEnvironment.SetEnvironmentVariable("NUGET_PACKAGES", customPath);

        // Act
        string result = service.GetUserNuGetCachePath();

        // Assert
        result.ShouldBe(customPath);
    }

    [TestMethod]
    public void GetUserNuGetCachePath_WithoutEnvironmentVariable_ReturnsDefaultPath()
    {
        // Arrange
        // fakeEnvironment doesn't have NUGET_PACKAGES set by default
        string homeDirectory = fakeEnvironment.HomeDirectory.FullPath;
        string expectedPath = service.CombinePath(homeDirectory, ".nuget", "packages");

        // Act
        string result = service.GetUserNuGetCachePath();

        // Assert
        result.ShouldBe(expectedPath);
    }

    [TestMethod]
    public void CombinePath_WithMultipleSegments_ReturnsCombinedPath()
    {
        // Act
        string result = service.CombinePath("/root", "folder", "file.txt");

        // Assert
        // The exact format depends on platform, but it should contain all segments
        result.ShouldContain("root");
        result.ShouldContain("folder");
        result.ShouldContain("file.txt");
    }

    [TestMethod]
    public void CombinePath_WithNullPaths_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.CombinePath(null!));
    }

    [TestMethod]
    public void CombinePath_WithEmptyArray_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.CombinePath());
    }


    [TestMethod]
    public void GetFiles_WithNonExistingDirectory_ReturnsEmpty()
    {
        // Arrange
        string path = "/missing";
        string pattern = "*.xml";
        // Don't create the directory - it should not exist

        // Act
        List<string> files = [.. service.GetFiles(path, pattern, false)];

        // Assert
        files.ShouldBeEmpty();
    }

    [TestMethod]
    public void GetFileName_WithValidPath_ReturnsFileName()
    {
        // Act
        string result = service.GetFileName("/path/to/file.txt");

        // Assert
        result.ShouldBe("file.txt");
    }

    [TestMethod]
    public void GetDirectoryName_WithValidPath_ReturnsDirectoryPath()
    {
        // Act
        string? result = service.GetDirectoryName("/path/to/file.txt");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain("path");
        result.ShouldContain("to");
    }

    [TestMethod]
    public void DirectoryExists_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.DirectoryExists(null!));
    }

    [TestMethod]
    public void DirectoryExists_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.DirectoryExists(""));
    }

    [TestMethod]
    public void GetFiles_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetFiles(null!, "*.xml", false));
    }

    [TestMethod]
    public void GetFiles_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetFiles("", "*.xml", false));
    }

    [TestMethod]
    public void GetFiles_WithNullPattern_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetFiles("/path", null!, false));
    }

    [TestMethod]
    public void GetFiles_WithEmptyPattern_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetFiles("/path", "", false));
    }

    [TestMethod]
    public void GetFileInfo_WithValidPath_ReturnsFileInfo()
    {
        // Arrange
        string path = "/test/file.txt";

        // Act
        FileInfo result = service.GetFileInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("file.txt");

        // Verify the full path contains expected segments (platform-agnostic)
        string fullPath = result.FullName;
        fullPath.ShouldContain("test");
        fullPath.ShouldContain("file.txt");

        // The FileInfo object should be properly constructed
        result.Extension.ShouldBe(".txt");
    }

    [TestMethod]
    public void GetFileInfo_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetFileInfo(null!));
    }

    [TestMethod]
    public void GetFileInfo_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetFileInfo(""));
    }

    [TestMethod]
    public void GetDirectoryInfo_WithValidPath_ReturnsDirectoryInfo()
    {
        // Arrange
        string path = "/test/directory";

        // Act
        DirectoryInfo result = service.GetDirectoryInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("directory");

        // Verify the full path contains expected segments (platform-agnostic)
        string fullPath = result.FullName;
        fullPath.ShouldContain("test");
        fullPath.ShouldContain("directory");

        // The DirectoryInfo object should be properly constructed
        result.ToString().ShouldContain("directory");
    }

    [TestMethod]
    public void GetDirectoryInfo_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetDirectoryInfo(null!));
    }

    [TestMethod]
    public void GetDirectoryInfo_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetDirectoryInfo(""));
    }


    [TestMethod]
    public void EnumerateFiles_WithNonExistingDirectory_ReturnsEmpty()
    {
        // Arrange
        string path = "/missing";
        // Don't create the directory - it should not exist

        // Act
        List<FileInfo> files = [.. service.EnumerateFiles(path)];

        // Assert
        files.ShouldBeEmpty();
    }

    [TestMethod]
    public void EnumerateFiles_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.EnumerateFiles(null!));
    }

    [TestMethod]
    public void EnumerateFiles_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.EnumerateFiles(""));
    }


    [TestMethod]
    public void GetFileName_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetFileName(null!));
    }

    [TestMethod]
    public void GetFileName_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetFileName(""));
    }

    [TestMethod]
    public void GetDirectoryName_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetDirectoryName(null!));
    }

    [TestMethod]
    public void GetDirectoryName_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetDirectoryName(""));
    }


    [TestMethod]
    public void DirectoryExists_WithNonExistingDirectory_ReturnsFalse()
    {
        // Arrange
        string path = "/test/missing";
        // Don't create the directory - it should not exist

        // Act
        bool exists = service.DirectoryExists(path);

        // Assert
        exists.ShouldBeFalse();
    }

    [TestMethod]
    public void CombinePath_WithSingleSegment_ReturnsPath()
    {
        // Act
        string result = service.CombinePath("/single");

        // Assert
        result.ShouldBe("/single");
    }

    [TestMethod]
    public void GetUserNuGetCachePath_WithEmptyEnvironmentVariable_UsesDefault()
    {
        // Arrange
        fakeEnvironment.SetEnvironmentVariable("NUGET_PACKAGES", "");
        string homeDirectory = fakeEnvironment.HomeDirectory.FullPath;
        string expectedPath = service.CombinePath(homeDirectory, ".nuget", "packages");

        // Act
        string result = service.GetUserNuGetCachePath();

        // Assert
        result.ShouldBe(expectedPath);
    }

    [TestMethod]
    public void GetUserNuGetCachePath_WithWhitespaceEnvironmentVariable_UsesDefault()
    {
        // Arrange
        fakeEnvironment.SetEnvironmentVariable("NUGET_PACKAGES", "   ");
        string homeDirectory = fakeEnvironment.HomeDirectory.FullPath;
        string expectedPath = service.CombinePath(homeDirectory, ".nuget", "packages");

        // Act
        string result = service.GetUserNuGetCachePath();

        // Assert
        result.ShouldBe(expectedPath);
    }

    [TestMethod]
    public void GetFiles_WithFileStructure_ReturnsMatchingFiles()
    {
        // Arrange
        // Create test file structure
        fakeFileSystem.CreateFile("/test/file1.xml").SetTextContent("xml1");
        fakeFileSystem.CreateFile("/test/file2.txt").SetTextContent("text");
        fakeFileSystem.CreateFile("/test/sub/file3.xml").SetTextContent("xml3");
        fakeFileSystem.CreateFile("/test/sub/deep/file4.xml").SetTextContent("xml4");

        // Act
        List<string> nonRecursive = [.. service.GetFiles("/test", "*.xml", false)];
        List<string> recursive = [.. service.GetFiles("/test", "*.xml", true)];

        // Assert
        nonRecursive.Count.ShouldBe(1);
        nonRecursive[0].ShouldEndWith("file1.xml");

        recursive.Count.ShouldBe(3);
        recursive.ShouldContain(f => f.EndsWith("file1.xml"));
        recursive.ShouldContain(f => f.EndsWith("file3.xml"));
        recursive.ShouldContain(f => f.EndsWith("file4.xml"));
    }

    [TestMethod]
    public void EnumerateFiles_WithFileStructure_ReturnsFileInfoObjects()
    {
        // Arrange
        // Create test files with different sizes
        fakeFileSystem.CreateFile("/test/small.txt").SetTextContent("small");
        fakeFileSystem.CreateFile("/test/medium.txt").SetTextContent("medium file content");
        fakeFileSystem.CreateFile("/test/sub/large.txt").SetTextContent("large file with lots of content here");

        // Act
        List<FileInfo> files = [.. service.EnumerateFiles("/test", "*.txt", true)];

        // Assert
        files.Count.ShouldBe(3);
        files.All(f => f.Extension == ".txt").ShouldBeTrue();

        // Verify we get the correct file names
        files.Select(f => f.Name).ShouldContain("small.txt");
        files.Select(f => f.Name).ShouldContain("medium.txt");
        files.Select(f => f.Name).ShouldContain("large.txt");
    }

}