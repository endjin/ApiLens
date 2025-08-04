using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class MemberInfoTests
{
    [TestMethod]
    public void MemberInfo_WithRequiredProperties_CreatesSuccessfully()
    {
        // Arrange & Act
        MemberInfo member = new()
        {
            Id = "T:System.String",
            MemberType = MemberType.Type,
            Name = "String",
            FullName = "System.String",
            Assembly = "System.Runtime",
            Namespace = "System"
        };

        // Assert
        member.Id.ShouldBe("T:System.String");
        member.MemberType.ShouldBe(MemberType.Type);
        member.Name.ShouldBe("String");
        member.FullName.ShouldBe("System.String");
        member.Assembly.ShouldBe("System.Runtime");
        member.Namespace.ShouldBe("System");
    }

    [TestMethod]
    public void MemberInfo_OptionalPropertiesNotSet_DefaultToNull()
    {
        // Arrange & Act
        MemberInfo member = new()
        {
            Id = "M:System.String.Split",
            MemberType = MemberType.Method,
            Name = "Split",
            FullName = "System.String.Split",
            Assembly = "System.Runtime",
            Namespace = "System"
        };

        // Assert
        member.Summary.ShouldBeNull();
        member.Remarks.ShouldBeNull();
    }

    [TestMethod]
    public void MemberInfo_WithCrossReferences_StoresCorrectly()
    {
        // Arrange
        CrossReference reference1 = new()
        {
            SourceId = "M:System.String.Split",
            TargetId = "T:System.Char",
            Type = ReferenceType.Parameter,
            Context = "separator parameter"
        };

        CrossReference reference2 = new()
        {
            SourceId = "M:System.String.Split",
            TargetId = "T:System.String[]",
            Type = ReferenceType.ReturnType,
            Context = "return type"
        };

        // Act
        MemberInfo member = new()
        {
            Id = "M:System.String.Split",
            MemberType = MemberType.Method,
            Name = "Split",
            FullName = "System.String.Split",
            Assembly = "System.Runtime",
            Namespace = "System",
            CrossReferences = [reference1, reference2]
        };

        // Assert
        member.CrossReferences.ShouldNotBeEmpty();
        member.CrossReferences.Length.ShouldBe(2);
        member.CrossReferences[0].TargetId.ShouldBe("T:System.Char");
        member.CrossReferences[1].TargetId.ShouldBe("T:System.String[]");
    }

    [TestMethod]
    public void MemberInfo_CollectionsNotSet_DefaultToEmptyArrays()
    {
        // Arrange & Act
        MemberInfo member = new()
        {
            Id = "T:System.Int32",
            MemberType = MemberType.Type,
            Name = "Int32",
            FullName = "System.Int32",
            Assembly = "System.Runtime",
            Namespace = "System"
        };

        // Assert
        member.CrossReferences.ShouldBeEmpty();
        member.RelatedTypes.ShouldBeEmpty();
    }
}