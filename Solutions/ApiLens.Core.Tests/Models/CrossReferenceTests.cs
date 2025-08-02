using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class CrossReferenceTests
{
    [TestMethod]
    public void Constructor_WithAllProperties_CreatesInstance()
    {
        // Arrange & Act
        CrossReference reference = new()
        {
            SourceId = "M:System.String.Split",
            TargetId = "T:System.Char",
            Type = ReferenceType.Parameter,
            Context = "First parameter type"
        };

        // Assert
        reference.SourceId.ShouldBe("M:System.String.Split");
        reference.TargetId.ShouldBe("T:System.Char");
        reference.Type.ShouldBe(ReferenceType.Parameter);
        reference.Context.ShouldBe("First parameter type");
    }

    [TestMethod]
    [DataRow(ReferenceType.See)]
    [DataRow(ReferenceType.SeeAlso)]
    [DataRow(ReferenceType.Param)]
    [DataRow(ReferenceType.Return)]
    [DataRow(ReferenceType.Exception)]
    [DataRow(ReferenceType.Inheritance)]
    [DataRow(ReferenceType.Parameter)]
    [DataRow(ReferenceType.ReturnType)]
    [DataRow(ReferenceType.GenericConstraint)]
    public void Constructor_WithDifferentReferenceTypes_CreatesInstance(ReferenceType refType)
    {
        // Arrange & Act
        CrossReference reference = new()
        {
            SourceId = "Source",
            TargetId = "Target",
            Type = refType,
            Context = "Test context"
        };

        // Assert
        reference.Type.ShouldBe(refType);
    }

    [TestMethod]
    public void Equality_WithSameValues_ReturnsTrue()
    {
        // Arrange
        CrossReference ref1 = new()
        {
            SourceId = "Source",
            TargetId = "Target",
            Type = ReferenceType.SeeAlso,
            Context = "Context"
        };

        CrossReference ref2 = new()
        {
            SourceId = "Source",
            TargetId = "Target",
            Type = ReferenceType.SeeAlso,
            Context = "Context"
        };

        // Act & Assert
        ref1.ShouldBe(ref2);
        ref1.GetHashCode().ShouldBe(ref2.GetHashCode());
    }

    [TestMethod]
    public void Equality_WithDifferentSourceId_ReturnsFalse()
    {
        // Arrange
        CrossReference ref1 = new()
        {
            SourceId = "Source1",
            TargetId = "Target",
            Type = ReferenceType.See,
            Context = "Context"
        };

        CrossReference ref2 = new()
        {
            SourceId = "Source2",
            TargetId = "Target",
            Type = ReferenceType.See,
            Context = "Context"
        };

        // Act & Assert
        ref1.ShouldNotBe(ref2);
    }

    [TestMethod]
    public void Equality_WithDifferentTargetId_ReturnsFalse()
    {
        // Arrange
        CrossReference ref1 = new()
        {
            SourceId = "Source",
            TargetId = "Target1",
            Type = ReferenceType.See,
            Context = "Context"
        };

        CrossReference ref2 = new()
        {
            SourceId = "Source",
            TargetId = "Target2",
            Type = ReferenceType.See,
            Context = "Context"
        };

        // Act & Assert
        ref1.ShouldNotBe(ref2);
    }

    [TestMethod]
    public void Equality_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        CrossReference ref1 = new()
        {
            SourceId = "Source",
            TargetId = "Target",
            Type = ReferenceType.See,
            Context = "Context"
        };

        CrossReference ref2 = new()
        {
            SourceId = "Source",
            TargetId = "Target",
            Type = ReferenceType.SeeAlso,
            Context = "Context"
        };

        // Act & Assert
        ref1.ShouldNotBe(ref2);
    }

    [TestMethod]
    public void Equality_WithDifferentContext_ReturnsFalse()
    {
        // Arrange
        CrossReference ref1 = new()
        {
            SourceId = "Source",
            TargetId = "Target",
            Type = ReferenceType.See,
            Context = "Context1"
        };

        CrossReference ref2 = new()
        {
            SourceId = "Source",
            TargetId = "Target",
            Type = ReferenceType.See,
            Context = "Context2"
        };

        // Act & Assert
        ref1.ShouldNotBe(ref2);
    }

    [TestMethod]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        CrossReference reference = new()
        {
            SourceId = "M:MyClass.MyMethod",
            TargetId = "T:System.Exception",
            Type = ReferenceType.Exception,
            Context = "Throws when invalid"
        };

        // Act
        string? result = reference.ToString();

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("MyClass.MyMethod");
        result.ShouldContain("System.Exception");
    }

    [TestMethod]
    public void WithOperator_CreatesNewInstanceWithModifiedProperties()
    {
        // Arrange
        CrossReference original = new()
        {
            SourceId = "Source",
            TargetId = "Target",
            Type = ReferenceType.See,
            Context = "Original"
        };

        // Act
        CrossReference modified = original with { Context = "Modified" };

        // Assert
        original.Context.ShouldBe("Original");
        modified.Context.ShouldBe("Modified");
        modified.SourceId.ShouldBe("Source");
        modified.TargetId.ShouldBe("Target");
        modified.Type.ShouldBe(ReferenceType.See);
    }

    [TestMethod]
    public void CrossReference_ComplexIds_HandlesCorrectly()
    {
        // Arrange & Act
        CrossReference reference = new()
        {
            SourceId = "M:System.Collections.Generic.List`1.Add(`0)",
            TargetId = "T:System.Collections.Generic.IEnumerable`1",
            Type = ReferenceType.Inheritance,
            Context = "Generic type inheritance"
        };

        // Assert
        reference.SourceId.ShouldBe("M:System.Collections.Generic.List`1.Add(`0)");
        reference.TargetId.ShouldBe("T:System.Collections.Generic.IEnumerable`1");
    }
}