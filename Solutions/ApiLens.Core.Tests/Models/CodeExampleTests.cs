using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class CodeExampleTests
{
    [TestMethod]
    public void Constructor_WithRequiredProperties_CreatesInstance()
    {
        // Arrange & Act
        CodeExample example = new()
        {
            Description = "Example of using the method",
            Code = "var result = Calculate(10, 20);"
        };

        // Assert
        example.Description.ShouldBe("Example of using the method");
        example.Code.ShouldBe("var result = Calculate(10, 20);");
        example.Language.ShouldBe("csharp"); // Default value
    }

    [TestMethod]
    public void Constructor_WithAllProperties_CreatesInstance()
    {
        // Arrange & Act
        CodeExample example = new()
        {
            Description = "Python example",
            Code = "result = calculate(10, 20)",
            Language = "python"
        };

        // Assert
        example.Description.ShouldBe("Python example");
        example.Code.ShouldBe("result = calculate(10, 20)");
        example.Language.ShouldBe("python");
    }

    [TestMethod]
    public void Equality_WithSameValues_ReturnsTrue()
    {
        // Arrange
        CodeExample example1 = new()
        {
            Description = "Test",
            Code = "code",
            Language = "csharp"
        };

        CodeExample example2 = new()
        {
            Description = "Test",
            Code = "code",
            Language = "csharp"
        };

        // Act & Assert
        example1.ShouldBe(example2);
        example1.GetHashCode().ShouldBe(example2.GetHashCode());
    }

    [TestMethod]
    public void Equality_WithDifferentDescription_ReturnsFalse()
    {
        // Arrange
        CodeExample example1 = new()
        {
            Description = "Description1",
            Code = "code"
        };

        CodeExample example2 = new()
        {
            Description = "Description2",
            Code = "code"
        };

        // Act & Assert
        example1.ShouldNotBe(example2);
    }

    [TestMethod]
    public void Equality_WithDifferentCode_ReturnsFalse()
    {
        // Arrange
        CodeExample example1 = new()
        {
            Description = "Test",
            Code = "code1"
        };

        CodeExample example2 = new()
        {
            Description = "Test",
            Code = "code2"
        };

        // Act & Assert
        example1.ShouldNotBe(example2);
    }

    [TestMethod]
    public void Equality_WithDifferentLanguage_ReturnsFalse()
    {
        // Arrange
        CodeExample example1 = new()
        {
            Description = "Test",
            Code = "code",
            Language = "csharp"
        };

        CodeExample example2 = new()
        {
            Description = "Test",
            Code = "code",
            Language = "fsharp"
        };

        // Act & Assert
        example1.ShouldNotBe(example2);
    }

    [TestMethod]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        CodeExample example = new()
        {
            Description = "Calculate sum",
            Code = "return a + b;",
            Language = "csharp"
        };

        // Act
        string? result = example.ToString();

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("Calculate sum");
        result.ShouldContain("return a + b;");
    }

    [TestMethod]
    public void WithOperator_CreatesNewInstanceWithModifiedProperties()
    {
        // Arrange
        CodeExample original = new()
        {
            Description = "Original",
            Code = "original code",
            Language = "csharp"
        };

        // Act
        CodeExample modified = original with { Language = "vb" };

        // Assert
        original.Language.ShouldBe("csharp");
        modified.Language.ShouldBe("vb");
        modified.Description.ShouldBe("Original");
        modified.Code.ShouldBe("original code");
    }

    [TestMethod]
    public void CodeExample_WithMultilineCode_PreservesFormatting()
    {
        // Arrange
        string multilineCode = """
                               public int Calculate(int a, int b)
                               {
                                   return a + b;
                               }
                               """;

        // Act
        CodeExample example = new()
        {
            Description = "Full method example",
            Code = multilineCode
        };

        // Assert
        example.Code.ShouldBe(multilineCode);
        example.Code.ShouldContain("\n");
    }
}