using ApiLens.Cli.Commands;
using ApiLens.Core.Lucene;
using ApiLens.Core.Parsing;
using ApiLens.Core.Services;
namespace ApiLens.Cli.Tests.Commands;

[TestClass]
public class IndexCommandNuGetTests
{
    private IXmlDocumentParser mockParser = null!;
    private IDocumentBuilder mockDocumentBuilder = null!;
    private IFileSystemService mockFileSystem = null!;
    private ILuceneIndexManagerFactory mockIndexManagerFactory = null!;
    //private IndexCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        mockParser = Substitute.For<IXmlDocumentParser>();
        mockDocumentBuilder = Substitute.For<IDocumentBuilder>();
        mockFileSystem = Substitute.For<IFileSystemService>();
        mockIndexManagerFactory = Substitute.For<ILuceneIndexManagerFactory>();
        //command = new IndexCommand(mockParser, mockDocumentBuilder, mockFileSystem, mockIndexManagerFactory);
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithValidNuGetPath_ReturnsPackageInfo()
    {
        // Arrange
        string path = "/home/user/.nuget/packages/newtonsoft.json/13.0.1/lib/net6.0/Newtonsoft.Json.xml";

        // Act
        (string PackageId, string Version, string Framework)? info = IndexCommand.ExtractNuGetInfo(path);

        // Assert
        info.ShouldNotBeNull();
        info.Value.PackageId.ShouldBe("newtonsoft.json");
        info.Value.Version.ShouldBe("13.0.1");
        info.Value.Framework.ShouldBe("net6.0");
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithRefPath_ReturnsPackageInfo()
    {
        // Arrange
        string path = "/home/user/.nuget/packages/system.text.json/8.0.0/ref/net8.0/System.Text.Json.xml";

        // Act
        (string PackageId, string Version, string Framework)? info = IndexCommand.ExtractNuGetInfo(path);

        // Assert
        info.ShouldNotBeNull();
        info.Value.PackageId.ShouldBe("system.text.json");
        info.Value.Version.ShouldBe("8.0.0");
        info.Value.Framework.ShouldBe("net8.0");
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithWindowsPath_ReturnsPackageInfo()
    {
        // Arrange
        string path = @"C:\Users\user\.nuget\packages\newtonsoft.json\13.0.1\lib\net6.0\Newtonsoft.Json.xml";

        // Act
        (string PackageId, string Version, string Framework)? info = IndexCommand.ExtractNuGetInfo(path);

        // Assert
        info.ShouldNotBeNull();
        info.Value.PackageId.ShouldBe("newtonsoft.json");
        info.Value.Version.ShouldBe("13.0.1");
        info.Value.Framework.ShouldBe("net6.0");
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithNonNuGetPath_ReturnsNull()
    {
        // Arrange
        string path = "/home/user/projects/myapp/docs/MyLib.xml";

        // Act
        (string PackageId, string Version, string Framework)? info = IndexCommand.ExtractNuGetInfo(path);

        // Assert
        info.ShouldBeNull();
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithInvalidStructure_ReturnsNull()
    {
        // Arrange
        string path = "/home/user/.nuget/packages/package.xml";

        // Act
        (string PackageId, string Version, string Framework)? info = IndexCommand.ExtractNuGetInfo(path);

        // Assert
        info.ShouldBeNull();
    }
}