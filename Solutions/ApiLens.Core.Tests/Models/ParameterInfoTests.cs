using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class ParameterInfoTests
{
    [TestMethod]
    public void Constructor_WithRequiredProperties_CreatesInstance()
    {
        // Arrange & Act
        ParameterInfo parameter = new()
        {
            Name = "value",
            Type = "System.String",
            Position = 0,
            IsOptional = false,
            IsParams = false,
            IsOut = false,
            IsRef = false
        };

        // Assert
        parameter.Name.ShouldBe("value");
        parameter.Type.ShouldBe("System.String");
        parameter.Position.ShouldBe(0);
        parameter.IsOptional.ShouldBe(false);
        parameter.IsParams.ShouldBe(false);
        parameter.IsOut.ShouldBe(false);
        parameter.IsRef.ShouldBe(false);
        parameter.DefaultValue.ShouldBeNull();
        parameter.Description.ShouldBeNull();
    }

    [TestMethod]
    public void Constructor_WithAllProperties_CreatesInstance()
    {
        // Arrange & Act
        ParameterInfo parameter = new()
        {
            Name = "count",
            Type = "System.Int32",
            Position = 1,
            IsOptional = true,
            IsParams = false,
            IsOut = false,
            IsRef = false,
            DefaultValue = "10",
            Description = "The count parameter"
        };

        // Assert
        parameter.Name.ShouldBe("count");
        parameter.Type.ShouldBe("System.Int32");
        parameter.Position.ShouldBe(1);
        parameter.IsOptional.ShouldBe(true);
        parameter.DefaultValue.ShouldBe("10");
        parameter.Description.ShouldBe("The count parameter");
    }

    [TestMethod]
    public void Constructor_WithParamsParameter_CreatesInstance()
    {
        // Arrange & Act
        ParameterInfo parameter = new()
        {
            Name = "args",
            Type = "System.Object[]",
            Position = 2,
            IsOptional = false,
            IsParams = true,
            IsOut = false,
            IsRef = false
        };

        // Assert
        parameter.IsParams.ShouldBe(true);
        parameter.Type.ShouldBe("System.Object[]");
    }

    [TestMethod]
    public void Constructor_WithOutParameter_CreatesInstance()
    {
        // Arrange & Act
        ParameterInfo parameter = new()
        {
            Name = "result",
            Type = "System.Boolean",
            Position = 1,
            IsOptional = false,
            IsParams = false,
            IsOut = true,
            IsRef = false
        };

        // Assert
        parameter.IsOut.ShouldBe(true);
        parameter.IsRef.ShouldBe(false);
    }

    [TestMethod]
    public void Constructor_WithRefParameter_CreatesInstance()
    {
        // Arrange & Act
        ParameterInfo parameter = new()
        {
            Name = "value",
            Type = "System.Int32",
            Position = 0,
            IsOptional = false,
            IsParams = false,
            IsOut = false,
            IsRef = true
        };

        // Assert
        parameter.IsRef.ShouldBe(true);
        parameter.IsOut.ShouldBe(false);
    }

    [TestMethod]
    public void Equality_WithSameValues_ReturnsTrue()
    {
        // Arrange
        ParameterInfo param1 = new()
        {
            Name = "test",
            Type = "System.String",
            Position = 0,
            IsOptional = true,
            IsParams = false,
            IsOut = false,
            IsRef = false,
            DefaultValue = "default",
            Description = "Test parameter"
        };

        ParameterInfo param2 = new()
        {
            Name = "test",
            Type = "System.String",
            Position = 0,
            IsOptional = true,
            IsParams = false,
            IsOut = false,
            IsRef = false,
            DefaultValue = "default",
            Description = "Test parameter"
        };

        // Act & Assert
        param1.ShouldBe(param2);
        param1.GetHashCode().ShouldBe(param2.GetHashCode());
    }

    [TestMethod]
    public void Equality_WithDifferentName_ReturnsFalse()
    {
        // Arrange
        ParameterInfo param1 = CreateBasicParameter() with { Name = "param1" };
        ParameterInfo param2 = CreateBasicParameter() with { Name = "param2" };

        // Act & Assert
        param1.ShouldNotBe(param2);
    }

    [TestMethod]
    public void Equality_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        ParameterInfo param1 = CreateBasicParameter() with { Type = "System.String" };
        ParameterInfo param2 = CreateBasicParameter() with { Type = "System.Int32" };

        // Act & Assert
        param1.ShouldNotBe(param2);
    }

    [TestMethod]
    public void Equality_WithDifferentPosition_ReturnsFalse()
    {
        // Arrange
        ParameterInfo param1 = CreateBasicParameter() with { Position = 0 };
        ParameterInfo param2 = CreateBasicParameter() with { Position = 1 };

        // Act & Assert
        param1.ShouldNotBe(param2);
    }

    [TestMethod]
    public void Equality_WithDifferentFlags_ReturnsFalse()
    {
        // Arrange
        ParameterInfo param1 = CreateBasicParameter() with { IsOptional = true };
        ParameterInfo param2 = CreateBasicParameter() with { IsOptional = false };

        // Act & Assert
        param1.ShouldNotBe(param2);
    }

    [TestMethod]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        ParameterInfo parameter = new()
        {
            Name = "value",
            Type = "System.String",
            Position = 0,
            IsOptional = true,
            IsParams = false,
            IsOut = false,
            IsRef = false,
            DefaultValue = "\"default\"",
            Description = "The value parameter"
        };

        // Act
        string? result = parameter.ToString();

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("value");
        result.ShouldContain("System.String");
    }

    [TestMethod]
    public void WithOperator_CreatesNewInstanceWithModifiedProperties()
    {
        // Arrange
        ParameterInfo original = CreateBasicParameter();

        // Act
        ParameterInfo modified = original with { IsOptional = true, DefaultValue = "100" };

        // Assert
        original.IsOptional.ShouldBe(false);
        original.DefaultValue.ShouldBeNull();
        modified.IsOptional.ShouldBe(true);
        modified.DefaultValue.ShouldBe("100");
    }

    [TestMethod]
    public void ParameterInfo_GenericType_HandlesCorrectly()
    {
        // Arrange & Act
        ParameterInfo parameter = new()
        {
            Name = "items",
            Type = "System.Collections.Generic.List`1[System.String]",
            Position = 0,
            IsOptional = false,
            IsParams = false,
            IsOut = false,
            IsRef = false
        };

        // Assert
        parameter.Type.ShouldBe("System.Collections.Generic.List`1[System.String]");
    }

    [TestMethod]
    public void ParameterInfo_NullableType_HandlesCorrectly()
    {
        // Arrange & Act
        ParameterInfo parameter = new()
        {
            Name = "count",
            Type = "System.Nullable`1[System.Int32]",
            Position = 0,
            IsOptional = true,
            IsParams = false,
            IsOut = false,
            IsRef = false,
            DefaultValue = "null"
        };

        // Assert
        parameter.Type.ShouldBe("System.Nullable`1[System.Int32]");
        parameter.DefaultValue.ShouldBe("null");
    }

    private static ParameterInfo CreateBasicParameter()
    {
        return new ParameterInfo
        {
            Name = "test",
            Type = "System.Object",
            Position = 0,
            IsOptional = false,
            IsParams = false,
            IsOut = false,
            IsRef = false
        };
    }
}