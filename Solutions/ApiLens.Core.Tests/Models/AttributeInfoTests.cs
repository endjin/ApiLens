using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class AttributeInfoTests
{
    [TestMethod]
    public void Constructor_WithRequiredProperties_CreatesInstance()
    {
        // Arrange & Act
        AttributeInfo attribute = new()
        {
            Type = "System.ObsoleteAttribute"
        };

        // Assert
        attribute.Type.ShouldBe("System.ObsoleteAttribute");
        attribute.Properties.ShouldBeEmpty();
    }

    [TestMethod]
    public void Constructor_WithAllProperties_CreatesInstance()
    {
        // Arrange
        ImmutableDictionary<string, string> properties = ImmutableDictionary<string, string>.Empty
            .Add("Message", "This method is obsolete")
            .Add("IsError", "false");

        // Act
        AttributeInfo attribute = new()
        {
            Type = "System.ObsoleteAttribute",
            Properties = properties
        };

        // Assert
        attribute.Type.ShouldBe("System.ObsoleteAttribute");
        attribute.Properties.Count.ShouldBe(2);
        attribute.Properties["Message"].ShouldBe("This method is obsolete");
        attribute.Properties["IsError"].ShouldBe("false");
    }

    [TestMethod]
    public void Equality_WithSameValues_ReturnsTrue()
    {
        // Arrange
        ImmutableDictionary<string, string> properties = ImmutableDictionary<string, string>.Empty
            .Add("Message", "Test");

        AttributeInfo attr1 = new()
        {
            Type = "TestAttribute",
            Properties = properties
        };

        AttributeInfo attr2 = new()
        {
            Type = "TestAttribute",
            Properties = properties
        };

        // Act & Assert
        attr1.ShouldBe(attr2);
        attr1.GetHashCode().ShouldBe(attr2.GetHashCode());
    }

    [TestMethod]
    public void Equality_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        AttributeInfo attr1 = new() { Type = "Attribute1" };
        AttributeInfo attr2 = new() { Type = "Attribute2" };

        // Act & Assert
        attr1.ShouldNotBe(attr2);
    }

    [TestMethod]
    public void Equality_WithDifferentProperties_ReturnsFalse()
    {
        // Arrange
        AttributeInfo attr1 = new()
        {
            Type = "TestAttribute",
            Properties = ImmutableDictionary<string, string>.Empty.Add("Key1", "Value1")
        };

        AttributeInfo attr2 = new()
        {
            Type = "TestAttribute",
            Properties = ImmutableDictionary<string, string>.Empty.Add("Key2", "Value2")
        };

        // Act & Assert
        attr1.ShouldNotBe(attr2);
    }

    [TestMethod]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        AttributeInfo attribute = new()
        {
            Type = "System.SerializableAttribute",
            Properties = ImmutableDictionary<string, string>.Empty.Add("Key", "Value")
        };

        // Act
        string? result = attribute.ToString();

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("System.SerializableAttribute");
    }

    [TestMethod]
    public void WithOperator_CreatesNewInstanceWithModifiedProperties()
    {
        // Arrange
        AttributeInfo original = new()
        {
            Type = "OriginalAttribute",
            Properties = ImmutableDictionary<string, string>.Empty
        };

        // Act
        AttributeInfo modified = original with { Type = "ModifiedAttribute" };

        // Assert
        original.Type.ShouldBe("OriginalAttribute");
        modified.Type.ShouldBe("ModifiedAttribute");
        modified.Properties.ShouldBeEmpty();
    }
}