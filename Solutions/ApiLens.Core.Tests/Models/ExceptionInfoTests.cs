using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class ExceptionInfoTests
{
    [TestMethod]
    public void Constructor_WithRequiredProperties_CreatesInstance()
    {
        // Arrange & Act
        ExceptionInfo exception = new()
        {
            Type = "System.ArgumentNullException"
        };

        // Assert
        exception.Type.ShouldBe("System.ArgumentNullException");
        exception.Condition.ShouldBeNull();
    }

    [TestMethod]
    public void Constructor_WithAllProperties_CreatesInstance()
    {
        // Arrange & Act
        ExceptionInfo exception = new()
        {
            Type = "System.InvalidOperationException",
            Condition = "Thrown when the operation is not valid for the current state"
        };

        // Assert
        exception.Type.ShouldBe("System.InvalidOperationException");
        exception.Condition.ShouldBe("Thrown when the operation is not valid for the current state");
    }

    [TestMethod]
    public void Equality_WithSameValues_ReturnsTrue()
    {
        // Arrange
        ExceptionInfo ex1 = new()
        {
            Type = "System.Exception",
            Condition = "Test condition"
        };

        ExceptionInfo ex2 = new()
        {
            Type = "System.Exception",
            Condition = "Test condition"
        };

        // Act & Assert
        ex1.ShouldBe(ex2);
        ex1.GetHashCode().ShouldBe(ex2.GetHashCode());
    }

    [TestMethod]
    public void Equality_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        ExceptionInfo ex1 = new() { Type = "Exception1" };
        ExceptionInfo ex2 = new() { Type = "Exception2" };

        // Act & Assert
        ex1.ShouldNotBe(ex2);
    }

    [TestMethod]
    public void Equality_WithDifferentCondition_ReturnsFalse()
    {
        // Arrange
        ExceptionInfo ex1 = new()
        {
            Type = "System.Exception",
            Condition = "Condition1"
        };

        ExceptionInfo ex2 = new()
        {
            Type = "System.Exception",
            Condition = "Condition2"
        };

        // Act & Assert
        ex1.ShouldNotBe(ex2);
    }

    [TestMethod]
    public void Equality_WithNullCondition_HandlesCorrectly()
    {
        // Arrange
        ExceptionInfo ex1 = new()
        {
            Type = "System.Exception",
            Condition = null
        };

        ExceptionInfo ex2 = new()
        {
            Type = "System.Exception",
            Condition = null
        };

        // Act & Assert
        ex1.ShouldBe(ex2);
    }

    [TestMethod]
    public void Equality_WithOneNullCondition_ReturnsFalse()
    {
        // Arrange
        ExceptionInfo ex1 = new()
        {
            Type = "System.Exception",
            Condition = null
        };

        ExceptionInfo ex2 = new()
        {
            Type = "System.Exception",
            Condition = "Some condition"
        };

        // Act & Assert
        ex1.ShouldNotBe(ex2);
    }

    [TestMethod]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        ExceptionInfo exception = new()
        {
            Type = "System.ArgumentException",
            Condition = "When argument is invalid"
        };

        // Act
        string? result = exception.ToString();

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("System.ArgumentException");
    }

    [TestMethod]
    public void WithOperator_CreatesNewInstanceWithModifiedProperties()
    {
        // Arrange
        ExceptionInfo original = new()
        {
            Type = "OriginalException",
            Condition = "Original condition"
        };

        // Act
        ExceptionInfo modified = original with { Condition = "Modified condition" };

        // Assert
        original.Condition.ShouldBe("Original condition");
        modified.Condition.ShouldBe("Modified condition");
        modified.Type.ShouldBe("OriginalException");
    }

    [TestMethod]
    public void ExceptionInfo_WithEmptyCondition_HandlesCorrectly()
    {
        // Arrange & Act
        ExceptionInfo exception = new()
        {
            Type = "System.Exception",
            Condition = string.Empty
        };

        // Assert
        exception.Condition.ShouldBe(string.Empty);
    }

    [TestMethod]
    public void ExceptionInfo_WithGenericExceptionType_HandlesCorrectly()
    {
        // Arrange & Act
        ExceptionInfo exception = new()
        {
            Type = "System.Collections.Generic.KeyNotFoundException",
            Condition = "The key was not present in the dictionary"
        };

        // Assert
        exception.Type.ShouldBe("System.Collections.Generic.KeyNotFoundException");
        exception.Condition.ShouldBe("The key was not present in the dictionary");
    }
}