using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class RichMetadataTests
{
    [TestMethod]
    public void CodeExample_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        CodeExample example = new()
        {
            Description = "Shows how to use the method",
            Code = "var result = Calculate(42);"
        };

        // Assert
        example.Description.ShouldBe("Shows how to use the method");
        example.Code.ShouldBe("var result = Calculate(42);");
        example.Language.ShouldBe("csharp"); // Default value
    }

    [TestMethod]
    public void CodeExample_ShouldAllowCustomLanguage()
    {
        // Arrange & Act
        CodeExample example = new()
        {
            Description = "Python example",
            Code = "result = calculate(42)",
            Language = "python"
        };

        // Assert
        example.Language.ShouldBe("python");
    }

    [TestMethod]
    public void ExceptionInfo_ShouldCreateWithType()
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
    public void ExceptionInfo_ShouldCreateWithCondition()
    {
        // Arrange & Act
        ExceptionInfo exception = new()
        {
            Type = "System.ArgumentOutOfRangeException",
            Condition = "value is less than zero"
        };

        // Assert
        exception.Type.ShouldBe("System.ArgumentOutOfRangeException");
        exception.Condition.ShouldBe("value is less than zero");
    }

    [TestMethod]
    public void AttributeInfo_ShouldCreateWithType()
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
    public void AttributeInfo_ShouldCreateWithProperties()
    {
        // Arrange
        ImmutableDictionary<string, string> properties = ImmutableDictionary<string, string>.Empty
            .Add("Message", "Use NewMethod instead")
            .Add("IsError", "true");

        // Act
        AttributeInfo attribute = new()
        {
            Type = "System.ObsoleteAttribute",
            Properties = properties
        };

        // Assert
        attribute.Properties.Count.ShouldBe(2);
        attribute.Properties["Message"].ShouldBe("Use NewMethod instead");
        attribute.Properties["IsError"].ShouldBe("true");
    }

    [TestMethod]
    public void ComplexityMetrics_ShouldCreateWithAllMetrics()
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
    public void MemberInfo_ShouldHaveEmptyCollectionsByDefault()
    {
        // Arrange & Act
        MemberInfo member = new()
        {
            Id = "M:Test.Method",
            MemberType = MemberType.Method,
            Name = "Method",
            FullName = "Test.Method",
            Assembly = "Test.dll",
            Namespace = "Test"
        };

        // Assert
        member.CodeExamples.ShouldBeEmpty();
        member.Exceptions.ShouldBeEmpty();
        member.Attributes.ShouldBeEmpty();
        member.Parameters.ShouldBeEmpty();
        member.Complexity.ShouldBeNull();
        member.Returns.ShouldBeNull();
        member.SeeAlso.ShouldBeNull();
    }

    [TestMethod]
    public void MemberInfo_ShouldAcceptRichMetadata()
    {
        // Arrange
        CodeExample[] examples =
        [
            new() { Description = "Basic usage", Code = "Method();" }
        ];

        ExceptionInfo[] exceptions =
        [
            new() { Type = "System.ArgumentNullException", Condition = "input is null" }
        ];

        AttributeInfo[] attributes =
        [
            new() { Type = "System.ObsoleteAttribute" }
        ];

        ParameterInfo[] parameters =
        [
            new()
            {
                Name = "value",
                Type = "int",
                Position = 0,
                IsOptional = false,
                IsParams = false,
                IsOut = false,
                IsRef = false,
                Description = "The input value"
            }
        ];

        ComplexityMetrics complexity = new()
        {
            ParameterCount = 1,
            CyclomaticComplexity = 1,
            DocumentationLineCount = 5
        };

        // Act
        MemberInfo member = new()
        {
            Id = "M:Test.Method(System.Int32)",
            MemberType = MemberType.Method,
            Name = "Method",
            FullName = "Test.Method",
            Assembly = "Test.dll",
            Namespace = "Test",
            CodeExamples = [.. examples],
            Exceptions = [.. exceptions],
            Attributes = [.. attributes],
            Parameters = [.. parameters],
            Complexity = complexity,
            Returns = "The calculated result",
            SeeAlso = "Test.OtherMethod"
        };

        // Assert
        member.CodeExamples.Length.ShouldBe(1);
        member.CodeExamples[0].Description.ShouldBe("Basic usage");

        member.Exceptions.Length.ShouldBe(1);
        member.Exceptions[0].Type.ShouldBe("System.ArgumentNullException");

        member.Attributes.Length.ShouldBe(1);
        member.Attributes[0].Type.ShouldBe("System.ObsoleteAttribute");

        member.Parameters.Length.ShouldBe(1);
        member.Parameters[0].Name.ShouldBe("value");

        member.Complexity.ShouldNotBeNull();
        member.Complexity.ParameterCount.ShouldBe(1);

        member.Returns.ShouldBe("The calculated result");
        member.SeeAlso.ShouldBe("Test.OtherMethod");
    }
}