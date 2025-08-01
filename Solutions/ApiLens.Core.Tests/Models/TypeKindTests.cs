using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class TypeKindTests
{
    [TestMethod]
    public void TypeKind_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<TypeKind>().ShouldContain(TypeKind.Class);
        Enum.GetValues<TypeKind>().ShouldContain(TypeKind.Interface);
        Enum.GetValues<TypeKind>().ShouldContain(TypeKind.Struct);
        Enum.GetValues<TypeKind>().ShouldContain(TypeKind.Enum);
        Enum.GetValues<TypeKind>().ShouldContain(TypeKind.Delegate);
    }

    [TestMethod]
    public void TypeKind_HasCorrectCount()
    {
        // Assert
        Enum.GetValues<TypeKind>().Length.ShouldBe(5);
    }

    [TestMethod]
    [DataRow(TypeKind.Class, 0)]
    [DataRow(TypeKind.Interface, 1)]
    [DataRow(TypeKind.Struct, 2)]
    [DataRow(TypeKind.Enum, 3)]
    [DataRow(TypeKind.Delegate, 4)]
    public void TypeKind_HasExpectedNumericValues(TypeKind typeKind, int expectedValue)
    {
        // Assert
        ((int)typeKind).ShouldBe(expectedValue);
    }

    [TestMethod]
    [DataRow(TypeKind.Class, "Class")]
    [DataRow(TypeKind.Interface, "Interface")]
    [DataRow(TypeKind.Struct, "Struct")]
    [DataRow(TypeKind.Enum, "Enum")]
    [DataRow(TypeKind.Delegate, "Delegate")]
    public void TypeKind_ToString_ReturnsExpectedString(TypeKind typeKind, string expectedString)
    {
        // Assert
        typeKind.ToString().ShouldBe(expectedString);
    }

    [TestMethod]
    public void TypeKind_CanBeParsedFromString()
    {
        // Act & Assert
        Enum.Parse<TypeKind>("Class").ShouldBe(TypeKind.Class);
        Enum.Parse<TypeKind>("Interface").ShouldBe(TypeKind.Interface);
        Enum.Parse<TypeKind>("Struct").ShouldBe(TypeKind.Struct);
        Enum.Parse<TypeKind>("Enum").ShouldBe(TypeKind.Enum);
        Enum.Parse<TypeKind>("Delegate").ShouldBe(TypeKind.Delegate);
    }

    [TestMethod]
    public void TypeKind_TryParse_WithValidValue_ReturnsTrue()
    {
        // Act
        bool result = Enum.TryParse<TypeKind>("Interface", out TypeKind typeKind);

        // Assert
        result.ShouldBeTrue();
        typeKind.ShouldBe(TypeKind.Interface);
    }

    [TestMethod]
    public void TypeKind_TryParse_WithInvalidValue_ReturnsFalse()
    {
        // Act
        bool result = Enum.TryParse<TypeKind>("InvalidTypeKind", out TypeKind typeKind);

        // Assert
        result.ShouldBeFalse();
        typeKind.ShouldBe(default(TypeKind));
    }

    [TestMethod]
    public void TypeKind_IsDefined_WithValidValue_ReturnsTrue()
    {
        // Assert
        Enum.IsDefined(TypeKind.Class).ShouldBeTrue();
        Enum.IsDefined(TypeKind.Interface).ShouldBeTrue();
        Enum.IsDefined(TypeKind.Struct).ShouldBeTrue();
        Enum.IsDefined(TypeKind.Enum).ShouldBeTrue();
        Enum.IsDefined(TypeKind.Delegate).ShouldBeTrue();
    }

    [TestMethod]
    public void TypeKind_IsDefined_WithInvalidValue_ReturnsFalse()
    {
        // Assert
        Enum.IsDefined((TypeKind)999).ShouldBeFalse();
    }

    [TestMethod]
    public void TypeKind_CanBeGroupedByCharacteristics()
    {
        // Arrange
        TypeKind[] referenceTypes = [TypeKind.Class, TypeKind.Interface, TypeKind.Delegate];
        TypeKind[] valueTypes = [TypeKind.Struct, TypeKind.Enum];

        // Act & Assert
        referenceTypes.ShouldContain(TypeKind.Class);
        referenceTypes.ShouldContain(TypeKind.Interface);
        referenceTypes.ShouldContain(TypeKind.Delegate);
        valueTypes.ShouldContain(TypeKind.Struct);
        valueTypes.ShouldContain(TypeKind.Enum);
    }

    [TestMethod]
    public void TypeKind_HasFlagsAttribute_ShouldBeFalse()
    {
        // Assert
        typeof(TypeKind).GetCustomAttributes(typeof(FlagsAttribute), false).ShouldBeEmpty();
    }

    [TestMethod]
    public void TypeKind_CaseInsensitiveParse_ThrowsException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => Enum.Parse<TypeKind>("class"));
    }

    [TestMethod]
    public void TypeKind_CaseInsensitiveTryParse_CanSucceed()
    {
        // Act
        bool result = Enum.TryParse<TypeKind>("class", ignoreCase: true, out TypeKind typeKind);

        // Assert
        result.ShouldBeTrue();
        typeKind.ShouldBe(TypeKind.Class);
    }
}