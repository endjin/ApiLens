using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class TypeInfoTests
{
    [TestMethod]
    public void TypeInfo_WithRequiredProperties_CreatesSuccessfully()
    {
        // Arrange & Act
        TypeInfo type = new()
        {
            Id = "T:System.Collections.Generic.List`1",
            Name = "List<T>",
            FullName = "System.Collections.Generic.List<T>",
            Assembly = "System.Collections",
            Namespace = "System.Collections.Generic",
            Kind = TypeKind.Class,
            IsGeneric = true,
            GenericArity = 1
        };

        // Assert
        type.Id.ShouldBe("T:System.Collections.Generic.List`1");
        type.Name.ShouldBe("List<T>");
        type.FullName.ShouldBe("System.Collections.Generic.List<T>");
        type.Assembly.ShouldBe("System.Collections");
        type.Namespace.ShouldBe("System.Collections.Generic");
        type.Kind.ShouldBe(TypeKind.Class);
        type.IsGeneric.ShouldBeTrue();
        type.GenericArity.ShouldBe(1);
    }

    [TestMethod]
    public void TypeInfo_WithInheritanceInfo_StoresCorrectly()
    {
        // Arrange & Act
        TypeInfo type = new()
        {
            Id = "T:System.ArgumentException",
            Name = "ArgumentException",
            FullName = "System.ArgumentException",
            Assembly = "System.Runtime",
            Namespace = "System",
            Kind = TypeKind.Class,
            IsGeneric = false,
            GenericArity = 0,
            BaseType = "System.SystemException",
            Interfaces = ["System.Runtime.Serialization.ISerializable"]
        };

        // Assert
        type.BaseType.ShouldBe("System.SystemException");
        type.Interfaces.ShouldNotBeEmpty();
        type.Interfaces.Length.ShouldBe(1);
        type.Interfaces[0].ShouldBe("System.Runtime.Serialization.ISerializable");
    }

    [TestMethod]
    public void TypeInfo_CollectionsNotSet_DefaultToEmptyArrays()
    {
        // Arrange & Act
        TypeInfo type = new()
        {
            Id = "T:System.Int32",
            Name = "Int32",
            FullName = "System.Int32",
            Assembly = "System.Runtime",
            Namespace = "System",
            Kind = TypeKind.Struct,
            IsGeneric = false,
            GenericArity = 0
        };

        // Assert
        type.Interfaces.ShouldBeEmpty();
        type.GenericParameters.ShouldBeEmpty();
        type.DerivedTypes.ShouldBeEmpty();
        type.BaseType.ShouldBeNull();
    }
}