using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class ComplexityMetricsTests
{
    [TestMethod]
    public void Constructor_WithAllProperties_CreatesInstance()
    {
        // Arrange & Act
        ComplexityMetrics metrics = new()
        {
            ParameterCount = 3,
            CyclomaticComplexity = 5,
            DocumentationLineCount = 10
        };

        // Assert
        metrics.ParameterCount.ShouldBe(3);
        metrics.CyclomaticComplexity.ShouldBe(5);
        metrics.DocumentationLineCount.ShouldBe(10);
    }

    [TestMethod]
    public void Constructor_WithZeroValues_CreatesInstance()
    {
        // Arrange & Act
        ComplexityMetrics metrics = new()
        {
            ParameterCount = 0,
            CyclomaticComplexity = 0,
            DocumentationLineCount = 0
        };

        // Assert
        metrics.ParameterCount.ShouldBe(0);
        metrics.CyclomaticComplexity.ShouldBe(0);
        metrics.DocumentationLineCount.ShouldBe(0);
    }

    [TestMethod]
    public void Equality_WithSameValues_ReturnsTrue()
    {
        // Arrange
        ComplexityMetrics metrics1 = new()
        {
            ParameterCount = 2,
            CyclomaticComplexity = 4,
            DocumentationLineCount = 8
        };

        ComplexityMetrics metrics2 = new()
        {
            ParameterCount = 2,
            CyclomaticComplexity = 4,
            DocumentationLineCount = 8
        };

        // Act & Assert
        metrics1.ShouldBe(metrics2);
        metrics1.GetHashCode().ShouldBe(metrics2.GetHashCode());
    }

    [TestMethod]
    public void Equality_WithDifferentParameterCount_ReturnsFalse()
    {
        // Arrange
        ComplexityMetrics metrics1 = new()
        {
            ParameterCount = 1,
            CyclomaticComplexity = 2,
            DocumentationLineCount = 3
        };

        ComplexityMetrics metrics2 = new()
        {
            ParameterCount = 2,
            CyclomaticComplexity = 2,
            DocumentationLineCount = 3
        };

        // Act & Assert
        metrics1.ShouldNotBe(metrics2);
    }

    [TestMethod]
    public void Equality_WithDifferentCyclomaticComplexity_ReturnsFalse()
    {
        // Arrange
        ComplexityMetrics metrics1 = new()
        {
            ParameterCount = 1,
            CyclomaticComplexity = 2,
            DocumentationLineCount = 3
        };

        ComplexityMetrics metrics2 = new()
        {
            ParameterCount = 1,
            CyclomaticComplexity = 5,
            DocumentationLineCount = 3
        };

        // Act & Assert
        metrics1.ShouldNotBe(metrics2);
    }

    [TestMethod]
    public void Equality_WithDifferentDocumentationLineCount_ReturnsFalse()
    {
        // Arrange
        ComplexityMetrics metrics1 = new()
        {
            ParameterCount = 1,
            CyclomaticComplexity = 2,
            DocumentationLineCount = 3
        };

        ComplexityMetrics metrics2 = new()
        {
            ParameterCount = 1,
            CyclomaticComplexity = 2,
            DocumentationLineCount = 10
        };

        // Act & Assert
        metrics1.ShouldNotBe(metrics2);
    }

    [TestMethod]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        ComplexityMetrics metrics = new()
        {
            ParameterCount = 4,
            CyclomaticComplexity = 6,
            DocumentationLineCount = 15
        };

        // Act
        string? result = metrics.ToString();

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("4");
        result.ShouldContain("6");
        result.ShouldContain("15");
    }

    [TestMethod]
    public void WithOperator_CreatesNewInstanceWithModifiedProperties()
    {
        // Arrange
        ComplexityMetrics original = new()
        {
            ParameterCount = 1,
            CyclomaticComplexity = 2,
            DocumentationLineCount = 3
        };

        // Act
        ComplexityMetrics modified = original with { ParameterCount = 5 };

        // Assert
        original.ParameterCount.ShouldBe(1);
        modified.ParameterCount.ShouldBe(5);
        modified.CyclomaticComplexity.ShouldBe(2);
        modified.DocumentationLineCount.ShouldBe(3);
    }

    [TestMethod]
    public void ComplexityMetrics_HighComplexityValues_HandlesCorrectly()
    {
        // Arrange & Act
        ComplexityMetrics metrics = new()
        {
            ParameterCount = 20,
            CyclomaticComplexity = 50,
            DocumentationLineCount = 1000
        };

        // Assert
        metrics.ParameterCount.ShouldBe(20);
        metrics.CyclomaticComplexity.ShouldBe(50);
        metrics.DocumentationLineCount.ShouldBe(1000);
    }
}