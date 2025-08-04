using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class MemberTypeTests
{
    [TestMethod]
    public void MemberType_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<MemberType>().ShouldContain(MemberType.Type);
        Enum.GetValues<MemberType>().ShouldContain(MemberType.Method);
        Enum.GetValues<MemberType>().ShouldContain(MemberType.Property);
        Enum.GetValues<MemberType>().ShouldContain(MemberType.Field);
        Enum.GetValues<MemberType>().ShouldContain(MemberType.Event);
    }

    [TestMethod]
    public void MemberType_HasCorrectCount()
    {
        // Assert
        Enum.GetValues<MemberType>().Length.ShouldBe(5);
    }

    [TestMethod]
    [DataRow(MemberType.Type, 0)]
    [DataRow(MemberType.Method, 1)]
    [DataRow(MemberType.Property, 2)]
    [DataRow(MemberType.Field, 3)]
    [DataRow(MemberType.Event, 4)]
    public void MemberType_HasExpectedNumericValues(MemberType memberType, int expectedValue)
    {
        // Assert
        ((int)memberType).ShouldBe(expectedValue);
    }

    [TestMethod]
    [DataRow(MemberType.Type, "Type")]
    [DataRow(MemberType.Method, "Method")]
    [DataRow(MemberType.Property, "Property")]
    [DataRow(MemberType.Field, "Field")]
    [DataRow(MemberType.Event, "Event")]
    public void MemberType_ToString_ReturnsExpectedString(MemberType memberType, string expectedString)
    {
        // Assert
        memberType.ToString().ShouldBe(expectedString);
    }

    [TestMethod]
    public void MemberType_CanBeParsedFromString()
    {
        // Act & Assert
        Enum.Parse<MemberType>("Type").ShouldBe(MemberType.Type);
        Enum.Parse<MemberType>("Method").ShouldBe(MemberType.Method);
        Enum.Parse<MemberType>("Property").ShouldBe(MemberType.Property);
        Enum.Parse<MemberType>("Field").ShouldBe(MemberType.Field);
        Enum.Parse<MemberType>("Event").ShouldBe(MemberType.Event);
    }

    [TestMethod]
    public void MemberType_TryParse_WithValidValue_ReturnsTrue()
    {
        // Act
        bool result = Enum.TryParse<MemberType>("Method", out MemberType memberType);

        // Assert
        result.ShouldBeTrue();
        memberType.ShouldBe(MemberType.Method);
    }

    [TestMethod]
    public void MemberType_TryParse_WithInvalidValue_ReturnsFalse()
    {
        // Act
        bool result = Enum.TryParse<MemberType>("InvalidMemberType", out MemberType memberType);

        // Assert
        result.ShouldBeFalse();
        memberType.ShouldBe(default(MemberType));
    }

    [TestMethod]
    public void MemberType_IsDefined_WithValidValue_ReturnsTrue()
    {
        // Assert
        Enum.IsDefined(MemberType.Type).ShouldBeTrue();
        Enum.IsDefined(MemberType.Method).ShouldBeTrue();
        Enum.IsDefined(MemberType.Property).ShouldBeTrue();
        Enum.IsDefined(MemberType.Field).ShouldBeTrue();
        Enum.IsDefined(MemberType.Event).ShouldBeTrue();
    }

    [TestMethod]
    public void MemberType_IsDefined_WithInvalidValue_ReturnsFalse()
    {
        // Assert
        Enum.IsDefined((MemberType)999).ShouldBeFalse();
    }

    [TestMethod]
    public void MemberType_HasFlagsAttribute_ShouldBeFalse()
    {
        // Assert
        typeof(MemberType).GetCustomAttributes(typeof(FlagsAttribute), false).ShouldBeEmpty();
    }
}