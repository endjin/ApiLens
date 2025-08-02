using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class MemberInfoVersionTests
{
    [TestMethod]
    public void MemberInfo_WithVersionFields_IsValid()
    {
        // Arrange & Act
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

        // Assert
        member.PackageId.ShouldBe("System.Runtime");
        member.PackageVersion.ShouldBe("8.0.0");
        member.TargetFramework.ShouldBe("net8.0");
        member.IsFromNuGetCache.ShouldBeTrue();
        member.SourceFilePath.ShouldBe("/home/user/.nuget/packages/system.runtime/8.0.0/lib/net8.0/System.Runtime.xml");
    }

    [TestMethod]
    public void MemberInfo_WithoutVersionFields_HasNullValues()
    {
        // Arrange & Act
        MemberInfo member = new()
        {
            Id = "M:System.String.Concat",
            MemberType = MemberType.Method,
            Name = "Concat",
            FullName = "System.String.Concat",
            Assembly = "System.Runtime",
            Namespace = "System"
        };

        // Assert - version fields should be null/default when not set
        member.PackageId.ShouldBeNull();
        member.PackageVersion.ShouldBeNull();
        member.TargetFramework.ShouldBeNull();
        member.IsFromNuGetCache.ShouldBeFalse();
        member.SourceFilePath.ShouldBeNull();
        member.ContentHash.ShouldBeNull();
        member.IndexedAt.ShouldBeNull();
    }

    [TestMethod]
    public void MemberInfo_WithContentHash_IsValid()
    {
        // Arrange
        DateTime indexedTime = DateTime.UtcNow;

        // Act
        MemberInfo member = new()
        {
            Id = "M:System.String.Concat",
            MemberType = MemberType.Method,
            Name = "Concat",
            FullName = "System.String.Concat",
            Assembly = "System.Runtime",
            Namespace = "System",
            ContentHash = "sha256:abcdef1234567890",
            IndexedAt = indexedTime
        };

        // Assert
        member.ContentHash.ShouldBe("sha256:abcdef1234567890");
        member.IndexedAt.ShouldBe(indexedTime);
    }
}