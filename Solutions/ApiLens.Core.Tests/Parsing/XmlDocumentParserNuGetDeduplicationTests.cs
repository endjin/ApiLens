using ApiLens.Core.Helpers;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Services;
using NSubstitute;
using Shouldly;

namespace ApiLens.Core.Tests.Parsing;

[TestClass]
public class XmlDocumentParserNuGetDeduplicationTests
{
    private XmlDocumentParser parser = null!;
    private IFileHashHelper mockFileHashHelper = null!;
    private IFileSystemService mockFileSystem = null!;

    [TestInitialize]
    public void Initialize()
    {
        mockFileHashHelper = Substitute.For<IFileHashHelper>();
        mockFileSystem = Substitute.For<IFileSystemService>();
        parser = new XmlDocumentParser(mockFileHashHelper, mockFileSystem);
    }

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithDifferentNuGetPackages_GeneratesUniqueIds()
    {
        // Arrange
        string xmlContent = @"<?xml version=""1.0""?>
<doc>
    <assembly>
        <name>TestAssembly</name>
    </assembly>
    <members>
        <member name=""T:TestNamespace.TestClass"">
            <summary>Test class</summary>
        </member>
        <member name=""M:TestNamespace.TestClass.TestMethod"">
            <summary>Test method</summary>
        </member>
    </members>
</doc>";

        // Create paths that simulate different NuGet packages
        string path1 = @"C:\Users\test\.nuget\packages\testpackage\1.0.0\lib\net6.0\TestAssembly.xml";
        string path2 = @"C:\Users\test\.nuget\packages\testpackage\1.0.0\lib\netstandard2.0\TestAssembly.xml";
        string path3 = @"C:\Users\test\.nuget\packages\testpackage\2.0.0\lib\net6.0\TestAssembly.xml";

        // Setup file system mock
        mockFileSystem.OpenReadAsync(Arg.Any<string>()).Returns(_ => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xmlContent)));

        // Act - Parse the same XML from different package locations
        List<MemberInfo> members1 = new();
        await foreach (var member in parser.ParseXmlFileStreamAsync(path1))
        {
            members1.Add(member);
        }

        List<MemberInfo> members2 = new();
        await foreach (var member in parser.ParseXmlFileStreamAsync(path2))
        {
            members2.Add(member);
        }

        List<MemberInfo> members3 = new();
        await foreach (var member in parser.ParseXmlFileStreamAsync(path3))
        {
            members3.Add(member);
        }

        // Assert - Each set should have 2 members
        members1.Count.ShouldBe(2);
        members2.Count.ShouldBe(2);
        members3.Count.ShouldBe(2);

        // All members should have the same original member names
        members1[0].Name.ShouldBe("TestClass");
        members2[0].Name.ShouldBe("TestClass");
        members3[0].Name.ShouldBe("TestClass");

        // But their IDs should be unique due to package/version/framework differences
        members1[0].Id.ShouldNotBe(members2[0].Id); // Different framework
        members1[0].Id.ShouldNotBe(members3[0].Id); // Different version
        members2[0].Id.ShouldNotBe(members3[0].Id); // Different framework and version

        // Verify the ID format includes package info
        members1[0].Id.ShouldContain("testpackage");
        members1[0].Id.ShouldContain("1.0.0");
        members1[0].Id.ShouldContain("net6.0");

        members2[0].Id.ShouldContain("testpackage");
        members2[0].Id.ShouldContain("1.0.0");
        members2[0].Id.ShouldContain("netstandard2.0");

        members3[0].Id.ShouldContain("testpackage");
        members3[0].Id.ShouldContain("2.0.0");
        members3[0].Id.ShouldContain("net6.0");
    }

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithNonNuGetFile_UsesFileHash()
    {
        // Arrange
        string xmlContent = @"<?xml version=""1.0""?>
<doc>
    <assembly>
        <name>TestAssembly</name>
    </assembly>
    <members>
        <member name=""T:TestNamespace.TestClass"">
            <summary>Test class</summary>
        </member>
    </members>
</doc>";

        string localPath = @"C:\Projects\MyProject\bin\Debug\TestAssembly.xml";
        string fileHash = "abc123hash";

        // Setup mocks
        mockFileSystem.OpenReadAsync(localPath).Returns(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xmlContent)));
        mockFileHashHelper.ComputeFileHashAsync(localPath).Returns(fileHash);

        // Act
        List<MemberInfo> members = new();
        await foreach (var member in parser.ParseXmlFileStreamAsync(localPath))
        {
            members.Add(member);
        }

        // Assert
        members.Count.ShouldBe(1);
        members[0].Id.ShouldContain(fileHash);
        members[0].Id.ShouldContain("testassembly"); // Lowercase assembly name
        members[0].IsFromNuGetCache.ShouldBe(false);
    }

    [TestMethod]
    public async Task ParseXmlFileStreamAsync_WithSameMemberInDifferentPackages_AllIndexed()
    {
        // Arrange
        string xmlContent = @"<?xml version=""1.0""?>
<doc>
    <assembly>
        <name>Newtonsoft.Json</name>
    </assembly>
    <members>
        <member name=""T:Newtonsoft.Json.JsonSerializer"">
            <summary>Serializes and deserializes objects into and from the JSON format.</summary>
        </member>
    </members>
</doc>";

        // Simulate the same type in different framework targets
        string[] paths = 
        {
            @"C:\Users\test\.nuget\packages\newtonsoft.json\13.0.3\lib\net6.0\Newtonsoft.Json.xml",
            @"C:\Users\test\.nuget\packages\newtonsoft.json\13.0.3\lib\netstandard2.0\Newtonsoft.Json.xml",
            @"C:\Users\test\.nuget\packages\newtonsoft.json\13.0.3\lib\net45\Newtonsoft.Json.xml",
            @"C:\Users\test\.nuget\packages\newtonsoft.json\12.0.3\lib\netstandard2.0\Newtonsoft.Json.xml"
        };

        mockFileSystem.OpenReadAsync(Arg.Any<string>()).Returns(_ => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xmlContent)));

        // Act
        List<string> allIds = new();
        foreach (string path in paths)
        {
            List<MemberInfo> members = new();
            await foreach (var member in parser.ParseXmlFileStreamAsync(path))
            {
                members.Add(member);
            }
            allIds.Add(members[0].Id);
        }

        // Assert - All IDs should be unique
        allIds.Distinct().Count().ShouldBe(4);
        
        // All should reference the same base member
        foreach (string id in allIds)
        {
            id.ShouldStartWith("T:Newtonsoft.Json.JsonSerializer|");
        }
    }
}