using ApiLens.Cli.Services;
using ApiLens.Core.Models;

namespace ApiLens.Cli.Tests.Services;

[TestClass]
public class ResultDeduplicationServiceTests
{
    private ResultDeduplicationService service = null!;

    [TestInitialize]
    public void Setup()
    {
        service = new ResultDeduplicationService();
    }

    [TestMethod]
    public void DeduplicateResults_WithDuplicates_ReturnsSingleBestVersion()
    {
        // Arrange
        var members = new List<MemberInfo>
        {
            CreateMemberInfo("TestType", "net6.0"),
            CreateMemberInfo("TestType", "net8.0"),
            CreateMemberInfo("TestType", "net9.0"),
            CreateMemberInfo("TestType", "netstandard2.0")
        };

        // Act
        var result = service.DeduplicateResults(members, true);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("TestType");
        result[0].TargetFramework!.ShouldContain("net9.0");
    }

    [TestMethod]
    public void DeduplicateResults_WithMultipleFrameworks_CombinesFrameworkInfo()
    {
        // Arrange
        var members = new List<MemberInfo>
        {
            CreateMemberInfo("Method1", "net8.0"),
            CreateMemberInfo("Method1", "net9.0"),
            CreateMemberInfo("Method1", "netstandard2.1")
        };

        // Act
        var result = service.DeduplicateResults(members, true);

        // Assert
        result.Count.ShouldBe(1);
        result[0].TargetFramework.ShouldNotBeNullOrWhiteSpace();
        // Should show best framework with indication of others
        result[0].TargetFramework!.ShouldContain("net9.0");
    }

    [TestMethod]
    public void DeduplicateResults_WithDifferentMembers_KeepsAllDistinct()
    {
        // Arrange
        var members = new List<MemberInfo>
        {
            CreateMemberInfo("Type1", "net9.0"),
            CreateMemberInfo("Type2", "net9.0"),
            CreateMemberInfo("Type3", "net9.0")
        };

        // Act
        var result = service.DeduplicateResults(members, true);

        // Assert
        result.Count.ShouldBe(3);
        result.Select(m => m.Name).ShouldBe(new[] { "Type1", "Type2", "Type3" });
    }

    [TestMethod]
    public void DeduplicateResults_WhenDisabled_ReturnsOriginalList()
    {
        // Arrange
        var members = new List<MemberInfo>
        {
            CreateMemberInfo("TestType", "net8.0"),
            CreateMemberInfo("TestType", "net9.0")
        };

        // Act
        var result = service.DeduplicateResults(members, false);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldBe(members);
    }

    [TestMethod]
    public void DeduplicateResults_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var members = new List<MemberInfo>();

        // Act
        var result = service.DeduplicateResults(members, true);

        // Assert
        result.Count.ShouldBe(0);
    }

    [TestMethod]
    public void DeduplicateResults_WithNoFrameworkInfo_HandlesGracefully()
    {
        // Arrange
        var members = new List<MemberInfo>
        {
            CreateMemberInfo("TestType", null),
            CreateMemberInfo("TestType", null)
        };

        // Act
        var result = service.DeduplicateResults(members, true);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("TestType");
    }

    [TestMethod]
    public void GetStatistics_CalculatesCorrectReduction()
    {
        // Arrange
        var original = new List<MemberInfo>
        {
            CreateMemberInfo("Type1", "net8.0"),
            CreateMemberInfo("Type1", "net9.0"),
            CreateMemberInfo("Type2", "net8.0"),
            CreateMemberInfo("Type2", "net9.0")
        };
        var deduplicated = service.DeduplicateResults(original, true);

        // Act
        var stats = service.GetStatistics(original, deduplicated);

        // Assert
        stats.OriginalCount.ShouldBe(4);
        stats.DeduplicatedCount.ShouldBe(2);
        stats.ReductionPercentage.ShouldBe(50.0);
        stats.AverageFrameworksPerMember.ShouldBe(2.0);
    }

    [TestMethod]
    public void GetStatistics_WithEmptyLists_HandlesGracefully()
    {
        // Arrange
        var original = new List<MemberInfo>();
        var deduplicated = new List<MemberInfo>();

        // Act
        var stats = service.GetStatistics(original, deduplicated);

        // Assert
        stats.OriginalCount.ShouldBe(0);
        stats.DeduplicatedCount.ShouldBe(0);
        stats.ReductionPercentage.ShouldBe(0);
        stats.AverageFrameworksPerMember.ShouldBe(1);
    }

    private static MemberInfo CreateMemberInfo(string name, string? targetFramework)
    {
        return new MemberInfo
        {
            Id = $"T:{name}",
            Name = name,
            FullName = $"TestNamespace.{name}",
            MemberType = MemberType.Type,
            Assembly = "TestAssembly",
            Namespace = "TestNamespace",
            PackageId = "TestPackage",
            TargetFramework = targetFramework
        };
    }
}