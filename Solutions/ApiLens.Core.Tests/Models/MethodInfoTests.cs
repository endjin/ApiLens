using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class MethodInfoTests
{
    [TestMethod]
    public void MethodInfo_WithRequiredProperties_CreatesSuccessfully()
    {
        // Arrange & Act
        MethodInfo method = new()
        {
            Id = "M:System.String.Split(System.Char)",
            Name = "Split",
            FullName = "System.String.Split(System.Char)",
            DeclaringType = "System.String",
            ReturnType = "System.String[]",
            IsStatic = false,
            IsExtension = false,
            IsAsync = false,
            IsGeneric = false,
            GenericArity = 0
        };

        // Assert
        method.Id.ShouldBe("M:System.String.Split(System.Char)");
        method.Name.ShouldBe("Split");
        method.FullName.ShouldBe("System.String.Split(System.Char)");
        method.DeclaringType.ShouldBe("System.String");
        method.ReturnType.ShouldBe("System.String[]");
        method.IsStatic.ShouldBeFalse();
        method.IsExtension.ShouldBeFalse();
        method.IsAsync.ShouldBeFalse();
        method.IsGeneric.ShouldBeFalse();
        method.GenericArity.ShouldBe(0);
    }

    [TestMethod]
    public void MethodInfo_WithParameters_StoresCorrectly()
    {
        // Arrange
        ParameterInfo param1 = new()
        {
            Name = "separator",
            Type = "System.Char",
            Position = 0,
            IsOptional = false,
            IsParams = false,
            IsOut = false,
            IsRef = false
        };

        // Act
        MethodInfo method = new()
        {
            Id = "M:System.String.Split(System.Char)",
            Name = "Split",
            FullName = "System.String.Split(System.Char)",
            DeclaringType = "System.String",
            ReturnType = "System.String[]",
            IsStatic = false,
            IsExtension = false,
            IsAsync = false,
            IsGeneric = false,
            GenericArity = 0,
            Parameters = [param1]
        };

        // Assert
        method.Parameters.ShouldNotBeEmpty();
        method.Parameters.Length.ShouldBe(1);
        method.Parameters[0].Name.ShouldBe("separator");
        method.Parameters[0].Type.ShouldBe("System.Char");
        method.Parameters[0].Position.ShouldBe(0);
    }

    [TestMethod]
    public void MethodInfo_ExtensionMethod_PropertiesSetCorrectly()
    {
        // Arrange & Act
        MethodInfo method = new()
        {
            Id = "M:Reaqtive.ObservableExtensions.ToSubscribable``1(System.IObservable{``0})",
            Name = "ToSubscribable",
            FullName = "Reaqtive.ObservableExtensions.ToSubscribable<TSource>(this IObservable<TSource>)",
            DeclaringType = "Reaqtive.ObservableExtensions",
            ReturnType = "ISubscribable<TSource>",
            IsStatic = true,
            IsExtension = true,
            IsAsync = false,
            IsGeneric = true,
            GenericArity = 1,
            GenericParameters = ["TSource"]
        };

        // Assert
        method.IsStatic.ShouldBeTrue();
        method.IsExtension.ShouldBeTrue();
        method.IsGeneric.ShouldBeTrue();
        method.GenericArity.ShouldBe(1);
        method.GenericParameters.ShouldNotBeEmpty();
        method.GenericParameters[0].ShouldBe("TSource");
    }

    [TestMethod]
    public void MethodInfo_CollectionsNotSet_DefaultToEmptyArrays()
    {
        // Arrange & Act
        MethodInfo method = new()
        {
            Id = "M:System.Object.ToString",
            Name = "ToString",
            FullName = "System.Object.ToString()",
            DeclaringType = "System.Object",
            ReturnType = "System.String",
            IsStatic = false,
            IsExtension = false,
            IsAsync = false,
            IsGeneric = false,
            GenericArity = 0
        };

        // Assert
        method.Parameters.ShouldBeEmpty();
        method.GenericParameters.ShouldBeEmpty();
        method.Exceptions.ShouldBeEmpty();
        method.Summary.ShouldBeNull();
        method.Returns.ShouldBeNull();
        method.Remarks.ShouldBeNull();
    }
}