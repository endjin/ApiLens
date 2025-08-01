using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class ReferenceTypeTests
{
    [TestMethod]
    public void ReferenceType_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<ReferenceType>().ShouldContain(ReferenceType.See);
        Enum.GetValues<ReferenceType>().ShouldContain(ReferenceType.SeeAlso);
        Enum.GetValues<ReferenceType>().ShouldContain(ReferenceType.Param);
        Enum.GetValues<ReferenceType>().ShouldContain(ReferenceType.Return);
        Enum.GetValues<ReferenceType>().ShouldContain(ReferenceType.Exception);
        Enum.GetValues<ReferenceType>().ShouldContain(ReferenceType.Inheritance);
        Enum.GetValues<ReferenceType>().ShouldContain(ReferenceType.Parameter);
        Enum.GetValues<ReferenceType>().ShouldContain(ReferenceType.ReturnType);
        Enum.GetValues<ReferenceType>().ShouldContain(ReferenceType.GenericConstraint);
    }

    [TestMethod]
    public void ReferenceType_HasCorrectCount()
    {
        // Assert
        Enum.GetValues<ReferenceType>().Length.ShouldBe(9);
    }

    [TestMethod]
    [DataRow(ReferenceType.See, 0)]
    [DataRow(ReferenceType.SeeAlso, 1)]
    [DataRow(ReferenceType.Param, 2)]
    [DataRow(ReferenceType.Return, 3)]
    [DataRow(ReferenceType.Exception, 4)]
    [DataRow(ReferenceType.Inheritance, 5)]
    [DataRow(ReferenceType.Parameter, 6)]
    [DataRow(ReferenceType.ReturnType, 7)]
    [DataRow(ReferenceType.GenericConstraint, 8)]
    public void ReferenceType_HasExpectedNumericValues(ReferenceType referenceType, int expectedValue)
    {
        // Assert
        ((int)referenceType).ShouldBe(expectedValue);
    }

    [TestMethod]
    [DataRow(ReferenceType.See, "See")]
    [DataRow(ReferenceType.SeeAlso, "SeeAlso")]
    [DataRow(ReferenceType.Param, "Param")]
    [DataRow(ReferenceType.Return, "Return")]
    [DataRow(ReferenceType.Exception, "Exception")]
    [DataRow(ReferenceType.Inheritance, "Inheritance")]
    [DataRow(ReferenceType.Parameter, "Parameter")]
    [DataRow(ReferenceType.ReturnType, "ReturnType")]
    [DataRow(ReferenceType.GenericConstraint, "GenericConstraint")]
    public void ReferenceType_ToString_ReturnsExpectedString(ReferenceType referenceType, string expectedString)
    {
        // Assert
        referenceType.ToString().ShouldBe(expectedString);
    }

    [TestMethod]
    public void ReferenceType_CanBeParsedFromString()
    {
        // Act & Assert
        Enum.Parse<ReferenceType>("See").ShouldBe(ReferenceType.See);
        Enum.Parse<ReferenceType>("SeeAlso").ShouldBe(ReferenceType.SeeAlso);
        Enum.Parse<ReferenceType>("Param").ShouldBe(ReferenceType.Param);
        Enum.Parse<ReferenceType>("Return").ShouldBe(ReferenceType.Return);
        Enum.Parse<ReferenceType>("Exception").ShouldBe(ReferenceType.Exception);
        Enum.Parse<ReferenceType>("Inheritance").ShouldBe(ReferenceType.Inheritance);
        Enum.Parse<ReferenceType>("Parameter").ShouldBe(ReferenceType.Parameter);
        Enum.Parse<ReferenceType>("ReturnType").ShouldBe(ReferenceType.ReturnType);
        Enum.Parse<ReferenceType>("GenericConstraint").ShouldBe(ReferenceType.GenericConstraint);
    }

    [TestMethod]
    public void ReferenceType_TryParse_WithValidValue_ReturnsTrue()
    {
        // Act
        bool result = Enum.TryParse<ReferenceType>("Exception", out ReferenceType referenceType);

        // Assert
        result.ShouldBeTrue();
        referenceType.ShouldBe(ReferenceType.Exception);
    }

    [TestMethod]
    public void ReferenceType_TryParse_WithInvalidValue_ReturnsFalse()
    {
        // Act
        bool result = Enum.TryParse<ReferenceType>("InvalidReferenceType", out ReferenceType referenceType);

        // Assert
        result.ShouldBeFalse();
        referenceType.ShouldBe(default(ReferenceType));
    }

    [TestMethod]
    public void ReferenceType_IsDefined_WithValidValue_ReturnsTrue()
    {
        // Assert
        Enum.IsDefined(ReferenceType.See).ShouldBeTrue();
        Enum.IsDefined(ReferenceType.SeeAlso).ShouldBeTrue();
        Enum.IsDefined(ReferenceType.Param).ShouldBeTrue();
        Enum.IsDefined(ReferenceType.Return).ShouldBeTrue();
        Enum.IsDefined(ReferenceType.Exception).ShouldBeTrue();
        Enum.IsDefined(ReferenceType.Inheritance).ShouldBeTrue();
        Enum.IsDefined(ReferenceType.Parameter).ShouldBeTrue();
        Enum.IsDefined(ReferenceType.ReturnType).ShouldBeTrue();
        Enum.IsDefined(ReferenceType.GenericConstraint).ShouldBeTrue();
    }

    [TestMethod]
    public void ReferenceType_IsDefined_WithInvalidValue_ReturnsFalse()
    {
        // Assert
        Enum.IsDefined((ReferenceType)999).ShouldBeFalse();
    }

    [TestMethod]
    public void ReferenceType_CanBeGroupedByCategory()
    {
        // Arrange
        ReferenceType[] documentationRefs = [ReferenceType.See, ReferenceType.SeeAlso];
        ReferenceType[] xmlDocRefs = [ReferenceType.Param, ReferenceType.Return, ReferenceType.Exception];
        ReferenceType[] codeRefs = [ReferenceType.Inheritance, ReferenceType.Parameter, ReferenceType.ReturnType, ReferenceType.GenericConstraint
        ];

        // Act & Assert
        documentationRefs.ShouldContain(ReferenceType.See);
        xmlDocRefs.ShouldContain(ReferenceType.Exception);
        codeRefs.ShouldContain(ReferenceType.Inheritance);
    }

    [TestMethod]
    public void ReferenceType_HasFlagsAttribute_ShouldBeFalse()
    {
        // Assert
        typeof(ReferenceType).GetCustomAttributes(typeof(FlagsAttribute), false).ShouldBeEmpty();
    }
}