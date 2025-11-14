using ApiLens.Core.Helpers;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Services;

namespace ApiLens.Core.Tests.Parsing;

[TestClass]
public class XmlDocumentParserComprehensiveTests
{
    private IFileSystemService mockFileSystem = null!;
    private IFileHashHelper mockFileHashHelper = null!;
    private XmlDocumentParser parser = null!;

    [TestInitialize]
    public void Setup()
    {
        mockFileSystem = Substitute.For<IFileSystemService>();
        mockFileHashHelper = Substitute.For<IFileHashHelper>();
        parser = new XmlDocumentParser(mockFileHashHelper, mockFileSystem);
    }

    #region Unique ID Generation Tests

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithNuGetPackage_GeneratesUniqueIdWithPackageVersionFramework()
    {
        // Arrange
        string filePath = @"C:\Users\test\.nuget\packages\newtonsoft.json\13.0.3\lib\net6.0\Newtonsoft.Json.xml";
        string xmlContent = """
                            <?xml version="1.0"?>
                            <doc>
                                <assembly>
                                    <name>Newtonsoft.Json</name>
                                </assembly>
                                <members>
                                    <member name="M:Newtonsoft.Json.JsonConvert.SerializeObject(System.Object)">
                                        <summary>Serializes an object to JSON.</summary>
                                    </member>
                                </members>
                            </doc>
                            """;

        SetupMockFileSystem(filePath, xmlContent);

        // Act
        List<MemberInfo> members = await parser.ParseXmlFileStreamAsync(filePath).ToListAsync();

        // Assert
        members.ShouldHaveSingleItem();
        MemberInfo member = members[0];

        // The unique ID should include package info to prevent duplicates
        member.Id.ShouldBe("M:Newtonsoft.Json.JsonConvert.SerializeObject(System.Object)|newtonsoft.json|13.0.3|net6.0");
        member.PackageId.ShouldBe("newtonsoft.json");
        member.PackageVersion.ShouldBe("13.0.3");
        member.TargetFramework.ShouldBe("net6.0");
        member.IsFromNuGetCache.ShouldBeTrue();
    }

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithSharedXmlAcrossFrameworks_GeneratesDistinctIds()
    {
        // Arrange - Common scenario: netstandard2.0 XML shared by multiple frameworks
        string sharedXmlPath = @"C:\Users\test\.nuget\packages\microsoft.extensions.logging\8.0.0\lib\netstandard2.0\Microsoft.Extensions.Logging.xml";
        string xmlContent = """
                            <?xml version="1.0"?>
                            <doc>
                                <assembly>
                                    <name>Microsoft.Extensions.Logging</name>
                                </assembly>
                                <members>
                                    <member name="T:Microsoft.Extensions.Logging.ILogger">
                                        <summary>Represents a type used to perform logging.</summary>
                                    </member>
                                </members>
                            </doc>
                            """;

        SetupMockFileSystem(sharedXmlPath, xmlContent);

        // Simulate parsing for different framework contexts
        NuGetPackageInfo[] packageInfos =
        [
            new() { PackageId = "microsoft.extensions.logging", Version = "8.0.0", TargetFramework = "net6.0", XmlDocumentationPath = sharedXmlPath },
            new() { PackageId = "microsoft.extensions.logging", Version = "8.0.0", TargetFramework = "net7.0", XmlDocumentationPath = sharedXmlPath },
            new() { PackageId = "microsoft.extensions.logging", Version = "8.0.0", TargetFramework = "net8.0", XmlDocumentationPath = sharedXmlPath }
        ];

        List<MemberInfo> allMembers = [];

        // Act - Parse same file for different frameworks
        foreach (NuGetPackageInfo pkgInfo in packageInfos)
        {
            // Simulate framework-specific parsing by temporarily modifying the path
            string frameworkSpecificPath = sharedXmlPath.Replace("netstandard2.0", pkgInfo.TargetFramework);
            mockFileSystem.OpenReadAsync(frameworkSpecificPath).Returns(CreateStreamFromString(xmlContent));

            List<MemberInfo> members = await parser.ParseXmlFileStreamAsync(frameworkSpecificPath).ToListAsync();
            allMembers.AddRange(members);
        }

        // Assert - Each framework should generate a unique ID
        allMembers.Count.ShouldBe(3);
        allMembers.Select(m => m.Id).Distinct().Count().ShouldBe(3);

        // Verify each has the correct framework
        allMembers[0].TargetFramework.ShouldBe("net6.0");
        allMembers[1].TargetFramework.ShouldBe("net7.0");
        allMembers[2].TargetFramework.ShouldBe("net8.0");
    }

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithLocalFile_GeneratesIdWithFileHash()
    {
        // Arrange
        string localPath = @"C:\Projects\MyLib\bin\Debug\MyLib.xml";
        string xmlContent = """
                            <?xml version="1.0"?>
                            <doc>
                                <assembly>
                                    <name>MyLib</name>
                                </assembly>
                                <members>
                                    <member name="T:MyLib.MyClass">
                                        <summary>Test class.</summary>
                                    </member>
                                </members>
                            </doc>
                            """;

        SetupMockFileSystem(localPath, xmlContent);
        mockFileHashHelper.ComputeFileHashAsync(localPath).Returns("ABC123HASH");

        // Act
        List<MemberInfo> members = await parser.ParseXmlFileStreamAsync(localPath).ToListAsync();

        // Assert
        members.ShouldHaveSingleItem();
        MemberInfo member = members[0];

        // For local files, package version should be the file hash
        member.Id.ShouldBe("T:MyLib.MyClass|mylib|ABC123HASH");
        member.PackageId.ShouldBe("mylib");
        member.PackageVersion.ShouldBe("ABC123HASH");
        member.TargetFramework.ShouldBeNull(); // Local files don't have target framework
        member.IsFromNuGetCache.ShouldBeFalse();
    }

    #endregion

    #region Path Normalization Tests

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithWindowsPath_NormalizesSourceFilePath()
    {
        // Arrange
        string windowsPath = @"C:\Users\test\.nuget\packages\package\1.0.0\lib\net6.0\Package.xml";
        string xmlContent = CreateSimpleXmlContent("Package", "T:Package.TestClass");

        SetupMockFileSystem(windowsPath, xmlContent);

        // Act
        List<MemberInfo> members = await parser.ParseXmlFileStreamAsync(windowsPath).ToListAsync();

        // Assert
        members.ShouldHaveSingleItem();
        // Path should be normalized with forward slashes and GetFullPath adds current directory
        members[0].SourceFilePath.ShouldNotBeNull();
        members[0].SourceFilePath!.ShouldEndWith("C:/Users/test/.nuget/packages/package/1.0.0/lib/net6.0/Package.xml");
        members[0].SourceFilePath!.ShouldNotContain("\\");
    }

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithLinuxPath_PreservesNormalizedPath()
    {
        // Arrange
        string linuxPath = "/home/user/.nuget/packages/package/1.0.0/lib/net6.0/Package.xml";
        string xmlContent = CreateSimpleXmlContent("Package", "T:Package.TestClass");

        SetupMockFileSystem(linuxPath, xmlContent);

        // Act
        List<MemberInfo> members = await parser.ParseXmlFileStreamAsync(linuxPath).ToListAsync();

        // Assert
        members.ShouldHaveSingleItem();
        // On Linux, GetFullPath might add current directory
        members[0].SourceFilePath.ShouldNotBeNull();
        members[0].SourceFilePath!.ShouldEndWith(linuxPath);
        members[0].SourceFilePath!.ShouldNotContain("\\");
    }

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithPathContainingSpaces_HandlesCorrectly()
    {
        // Arrange
        string pathWithSpaces = @"C:\Program Files\My App\.nuget\packages\package\1.0.0\lib\net6.0\Package.xml";
        string xmlContent = CreateSimpleXmlContent("Package", "T:Package.TestClass");

        SetupMockFileSystem(pathWithSpaces, xmlContent);

        // Act
        List<MemberInfo> members = await parser.ParseXmlFileStreamAsync(pathWithSpaces).ToListAsync();

        // Assert
        members.ShouldHaveSingleItem();
        // Path should be normalized with forward slashes
        members[0].SourceFilePath.ShouldNotBeNull();
        members[0].SourceFilePath!.ShouldEndWith("C:/Program Files/My App/.nuget/packages/package/1.0.0/lib/net6.0/Package.xml");
        members[0].SourceFilePath!.ShouldNotContain("\\");
    }

    #endregion

    #region Empty and Invalid XML Tests

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithEmptyMembersSection_ReturnsNoMembers()
    {
        // Arrange
        string filePath = @"C:\test\empty.xml";
        string xmlContent = """
                            <?xml version="1.0"?>
                            <doc>
                                <assembly>
                                    <name>EmptyAssembly</name>
                                </assembly>
                                <members>
                                </members>
                            </doc>
                            """;

        SetupMockFileSystem(filePath, xmlContent);

        // Act
        List<MemberInfo> members = await parser.ParseXmlFileStreamAsync(filePath).ToListAsync();

        // Assert
        members.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithNoMembersSection_ReturnsNoMembers()
    {
        // Arrange
        string filePath = @"C:\test\no-members.xml";
        string xmlContent = """
                            <?xml version="1.0"?>
                            <doc>
                                <assembly>
                                    <name>NoMembersAssembly</name>
                                </assembly>
                            </doc>
                            """;

        SetupMockFileSystem(filePath, xmlContent);

        // Act
        List<MemberInfo> members = await parser.ParseXmlFileStreamAsync(filePath).ToListAsync();

        // Assert
        members.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithInvalidMemberPrefixes_SkipsInvalidMembers()
    {
        // Arrange
        string filePath = @"C:\test\invalid-prefixes.xml";
        string xmlContent = """
                            <?xml version="1.0"?>
                            <doc>
                                <assembly>
                                    <name>TestAssembly</name>
                                </assembly>
                                <members>
                                    <member name="X:Invalid.Member">
                                        <summary>Invalid prefix X</summary>
                                    </member>
                                    <member name="T:Valid.Type">
                                        <summary>Valid type</summary>
                                    </member>
                                    <member name="InvalidFormat">
                                        <summary>No prefix</summary>
                                    </member>
                                    <member name="">
                                        <summary>Empty name</summary>
                                    </member>
                                </members>
                            </doc>
                            """;

        SetupMockFileSystem(filePath, xmlContent);

        // Act
        List<MemberInfo> members = await parser.ParseXmlFileStreamAsync(filePath).ToListAsync();

        // Assert
        members.ShouldHaveSingleItem();
        members[0].Name.ShouldBe("Type"); // ExtractNameFromId returns just "Type" for types
    }

    #endregion

    #region Framework Detection Tests

    [TestMethod]
    [DataRow(@"C:\Users\test\.nuget\packages\pkg\1.0\lib\net48\pkg.xml", "net48")]
    [DataRow(@"C:\Users\test\.nuget\packages\pkg\1.0\lib\net6.0\pkg.xml", "net6.0")]
    [DataRow(@"C:\Users\test\.nuget\packages\pkg\1.0\lib\net7.0-windows\pkg.xml", "net7.0-windows")]
    [DataRow(@"C:\Users\test\.nuget\packages\pkg\1.0\lib\netstandard2.0\pkg.xml", "netstandard2.0")]
    [DataRow(@"C:\Users\test\.nuget\packages\pkg\1.0\lib\netcoreapp3.1\pkg.xml", "netcoreapp3.1")]
    public async Task ParseXmlFileStreamAsync_ExtractsCorrectFramework(string filePath, string expectedFramework)
    {
        // Arrange
        string xmlContent = CreateSimpleXmlContent("Package", "T:Package.TestClass");
        SetupMockFileSystem(filePath, xmlContent);

        // Act
        List<MemberInfo> members = await parser.ParseXmlFileStreamAsync(filePath).ToListAsync();

        // Assert
        members.ShouldHaveSingleItem();
        members[0].TargetFramework.ShouldBe(expectedFramework);
    }

    #endregion

    #region Edge Cases and Special Scenarios

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithVeryLongPath_HandlesCorrectly()
    {
        // Arrange - Create a path near Windows MAX_PATH limit
        string longPath = @"C:\" + string.Join(@"\", Enumerable.Repeat("verylongfoldername", 10)) + @"\.nuget\packages\pkg\1.0.0\lib\net6.0\Package.xml";
        string xmlContent = CreateSimpleXmlContent("Package", "T:Package.TestClass");

        SetupMockFileSystem(longPath, xmlContent);

        // Act
        List<MemberInfo> members = await parser.ParseXmlFileStreamAsync(longPath).ToListAsync();

        // Assert
        members.ShouldHaveSingleItem();
        members[0].SourceFilePath.ShouldNotBeNullOrWhiteSpace();
        members[0].SourceFilePath!.ShouldContain("/"); // Should be normalized
    }

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithSpecialCharactersInPath_HandlesCorrectly()
    {
        // Arrange
        string specialPath = @"C:\Users\test-user\.nuget\packages\my.package-name\1.0.0+build\lib\net6.0\My.Package-Name.xml";
        string xmlContent = CreateSimpleXmlContent("My.Package-Name", "T:My.Package.TestClass");

        SetupMockFileSystem(specialPath, xmlContent);

        // Act
        List<MemberInfo> members = await parser.ParseXmlFileStreamAsync(specialPath).ToListAsync();

        // Assert
        members.ShouldHaveSingleItem();
        members[0].PackageId.ShouldBe("my.package-name");
        members[0].PackageVersion.ShouldBe("1.0.0+build");
    }

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithPreviewVersion_ExtractsCorrectly()
    {
        // Arrange
        string previewPath = @"C:\Users\test\.nuget\packages\microsoft.extensions.logging\9.0.0-preview.1.24080.9\lib\net9.0\Microsoft.Extensions.Logging.xml";
        string xmlContent = CreateSimpleXmlContent("Microsoft.Extensions.Logging", "T:Microsoft.Extensions.Logging.ILogger");

        SetupMockFileSystem(previewPath, xmlContent);

        // Act
        List<MemberInfo> members = await parser.ParseXmlFileStreamAsync(previewPath).ToListAsync();

        // Assert
        members.ShouldHaveSingleItem();
        members[0].PackageVersion.ShouldBe("9.0.0-preview.1.24080.9");
        members[0].TargetFramework.ShouldBe("net9.0");
    }

    #endregion

    #region Helper Methods

    private void SetupMockFileSystem(string filePath, string xmlContent)
    {
        mockFileSystem.OpenReadAsync(filePath).Returns(CreateStreamFromString(xmlContent));
    }

    private static Stream CreateStreamFromString(string content)
    {
        MemoryStream stream = new();
        StreamWriter writer = new(stream);
        writer.Write(content);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    private static string CreateSimpleXmlContent(string assemblyName, string memberName)
    {
        return $"""
                <?xml version="1.0"?>
                <doc>
                    <assembly>
                        <name>{assemblyName}</name>
                    </assembly>
                    <members>
                        <member name="{memberName}">
                            <summary>Test summary.</summary>
                        </member>
                    </members>
                </doc>
                """;
    }

    #endregion
}