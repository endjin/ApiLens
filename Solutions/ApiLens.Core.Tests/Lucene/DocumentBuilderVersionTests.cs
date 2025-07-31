using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using Lucene.Net.Documents;

namespace ApiLens.Core.Tests.Lucene;

[TestClass]
public class DocumentBuilderVersionTests
{
    private DocumentBuilder builder = null!;

    [TestInitialize]
    public void Setup()
    {
        builder = new DocumentBuilder();
    }

    [TestMethod]
    public void BuildDocument_WithVersionFields_StoresAllVersionData()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "M:System.String.Concat",
            MemberType = MemberType.Method,
            Name = "Concat",
            FullName = "System.String.Concat",
            Assembly = "System.Runtime",
            Namespace = "System",
            PackageId = "System.Runtime",
            PackageVersion = "8.0.0",
            TargetFramework = "net8.0",
            IsFromNuGetCache = true,
            SourceFilePath = "/home/user/.nuget/packages/system.runtime/8.0.0/lib/net8.0/System.Runtime.xml"
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert
        doc.Get("packageId").ShouldBe("System.Runtime");
        doc.Get("packageVersion").ShouldBe("8.0.0");
        doc.Get("targetFramework").ShouldBe("net8.0");
        doc.Get("isFromNuGetCache").ShouldBe("true");
        doc.Get("sourceFilePath").ShouldBe("/home/user/.nuget/packages/system.runtime/8.0.0/lib/net8.0/System.Runtime.xml");
    }

    [TestMethod]
    public void BuildDocument_WithoutVersionFields_DoesNotAddFields()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "M:System.String.Concat",
            MemberType = MemberType.Method,
            Name = "Concat",
            FullName = "System.String.Concat",
            Assembly = "System.Runtime",
            Namespace = "System"
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert - fields should not exist when not provided
        doc.Get("packageId").ShouldBeNull();
        doc.Get("packageVersion").ShouldBeNull();
        doc.Get("targetFramework").ShouldBeNull();
        doc.Get("isFromNuGetCache").ShouldBe("false");
        doc.Get("sourceFilePath").ShouldBeNull();
    }

    [TestMethod]
    public void BuildDocument_WithVersionFields_CreatesSearchableVersionField()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "M:System.String.Concat",
            MemberType = MemberType.Method,
            Name = "Concat",
            FullName = "System.String.Concat",
            Assembly = "System.Runtime",
            Namespace = "System",
            PackageVersion = "8.0.0"
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert - version should be searchable
        doc.Get("versionSearch").ShouldBe("8.0.0");
    }
}