using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Models;

[TestClass]
public class ApiAssemblyInfoTests
{
    [TestMethod]
    public void ApiAssemblyInfo_WithRequiredProperties_CreatesSuccessfully()
    {
        // Arrange & Act
        ApiAssemblyInfo assembly = new()
        {
            Name = "System.Collections",
            Version = "9.0.0.0",
            Culture = "neutral",
            PublicKeyToken = "b03f5f7f11d50a3a"
        };

        // Assert
        assembly.Name.ShouldBe("System.Collections");
        assembly.Version.ShouldBe("9.0.0.0");
        assembly.Culture.ShouldBe("neutral");
        assembly.PublicKeyToken.ShouldBe("b03f5f7f11d50a3a");
    }

    [TestMethod]
    public void ApiAssemblyInfo_WithTypes_StoresCorrectly()
    {
        // Arrange
        string[] types = ["List<T>", "Dictionary<TKey, TValue>", "HashSet<T>"];
        string[] namespaces = ["System.Collections.Generic", "System.Collections"];

        // Act
        ApiAssemblyInfo assembly = new()
        {
            Name = "System.Collections",
            Version = "9.0.0.0",
            Culture = "neutral",
            PublicKeyToken = "b03f5f7f11d50a3a",
            Types = [.. types],
            Namespaces = [.. namespaces]
        };

        // Assert
        assembly.Types.ShouldNotBeEmpty();
        assembly.Types.Length.ShouldBe(3);
        assembly.Types.ShouldContain("List<T>");
        assembly.Namespaces.Length.ShouldBe(2);
        assembly.Namespaces.ShouldContain("System.Collections.Generic");
    }

    [TestMethod]
    public void ApiAssemblyInfo_CollectionsNotSet_DefaultToEmptyArrays()
    {
        // Arrange & Act
        ApiAssemblyInfo assembly = new()
        {
            Name = "MyAssembly",
            Version = "1.0.0.0",
            Culture = "neutral",
            PublicKeyToken = null
        };

        // Assert
        assembly.Types.ShouldBeEmpty();
        assembly.Namespaces.ShouldBeEmpty();
        assembly.Description.ShouldBeNull();
        assembly.PublicKeyToken.ShouldBeNull();
    }
}