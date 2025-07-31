using ApiLens.Core.Helpers;
using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Helpers;

[TestClass]
public class NuGetHelperTests
{
    #region ExtractNuGetInfo Tests - Valid Paths

    [TestMethod]
    [DataRow(@"C:\Users\test\.nuget\packages\newtonsoft.json\13.0.3\lib\net6.0\Newtonsoft.Json.xml", 
             "newtonsoft.json", "13.0.3", "net6.0")]
    [DataRow(@"C:\Users\test\.nuget\packages\microsoft.extensions.logging\8.0.0\lib\netstandard2.0\Microsoft.Extensions.Logging.xml", 
             "microsoft.extensions.logging", "8.0.0", "netstandard2.0")]
    [DataRow("/home/user/.nuget/packages/serilog/3.1.1/lib/net7.0/Serilog.xml", 
             "serilog", "3.1.1", "net7.0")]
    [DataRow(@"D:\NuGet\packages\my.package-name\1.0.0+build\lib\net48\My.Package-Name.xml", 
             "my.package-name", "1.0.0+build", "net48")]
    public void ExtractNuGetInfo_WithValidPath_ExtractsCorrectInfo(string path, string expectedPackageId, 
        string expectedVersion, string expectedFramework)
    {
        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.PackageId.ShouldBe(expectedPackageId);
        result.Value.Version.ShouldBe(expectedVersion);
        result.Value.Framework.ShouldBe(expectedFramework);
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithPreReleaseVersion_ExtractsCorrectly()
    {
        // Arrange
        string path = @"C:\Users\test\.nuget\packages\microsoft.extensions.hosting\9.0.0-preview.1.24080.9\lib\net9.0\Microsoft.Extensions.Hosting.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.PackageId.ShouldBe("microsoft.extensions.hosting");
        result.Value.Version.ShouldBe("9.0.0-preview.1.24080.9");
        result.Value.Framework.ShouldBe("net9.0");
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithComplexVersion_ExtractsCorrectly()
    {
        // Arrange
        string path = @"C:\Users\test\.nuget\packages\my.package\2.0.0-beta.1+20230515.3\lib\netcoreapp3.1\My.Package.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Version.ShouldBe("2.0.0-beta.1+20230515.3");
    }

    #endregion

    #region ExtractNuGetInfo Tests - Framework Variations

    [TestMethod]
    [DataRow("net48", "net48")]
    [DataRow("net6.0", "net6.0")]
    [DataRow("net7.0-windows", "net7.0-windows")]
    [DataRow("net8.0-android", "net8.0-android")]
    [DataRow("netstandard2.0", "netstandard2.0")]
    [DataRow("netstandard2.1", "netstandard2.1")]
    [DataRow("netcoreapp3.1", "netcoreapp3.1")]
    [DataRow("net5.0-windows7.0", "net5.0-windows7.0")]
    public void ExtractNuGetInfo_WithVariousFrameworks_ExtractsCorrectly(string framework, string expected)
    {
        // Arrange
        string path = $@"C:\Users\test\.nuget\packages\test.package\1.0.0\lib\{framework}\Test.Package.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Framework.ShouldBe(expected);
    }

    #endregion

    #region ExtractNuGetInfo Tests - Invalid Paths

    [TestMethod]
    public void ExtractNuGetInfo_WithNonNuGetPath_ReturnsNull()
    {
        // Arrange
        string path = @"C:\Projects\MyProject\bin\Debug\MyProject.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithEmptyPath_ReturnsNull()
    {
        // Act
        var result = NuGetHelper.ExtractNuGetInfo(string.Empty);

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithNullPath_ReturnsNull()
    {
        // Act
        var result = NuGetHelper.ExtractNuGetInfo(null!);

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithMalformedNuGetPath_ReturnsNull()
    {
        // Arrange - Missing version component
        string path = @"C:\Users\test\.nuget\packages\mypackage\lib\net6.0\MyPackage.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region ExtractNuGetInfo Tests - Edge Cases

    [TestMethod]
    public void ExtractNuGetInfo_WithSpacesInPath_ExtractsCorrectly()
    {
        // Arrange
        string path = @"C:\Program Files\My App\.nuget\packages\my.package\1.0.0\lib\net6.0\My.Package.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.PackageId.ShouldBe("my.package");
        result.Value.Version.ShouldBe("1.0.0");
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithDotsInPackageName_ExtractsCorrectly()
    {
        // Arrange
        string path = @"C:\Users\test\.nuget\packages\company.product.feature.component\2.5.0\lib\net7.0\Company.Product.Feature.Component.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.PackageId.ShouldBe("company.product.feature.component");
        result.Value.Version.ShouldBe("2.5.0");
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithHyphensInPackageName_ExtractsCorrectly()
    {
        // Arrange
        string path = @"C:\Users\test\.nuget\packages\my-package-name\1.0.0\lib\net6.0\My-Package-Name.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.PackageId.ShouldBe("my-package-name");
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithUnderscoresInPackageName_ExtractsCorrectly()
    {
        // Arrange
        string path = @"C:\Users\test\.nuget\packages\my_package_name\1.0.0\lib\net6.0\My_Package_Name.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.PackageId.ShouldBe("my_package_name");
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithVeryLongPath_ExtractsCorrectly()
    {
        // Arrange - Create a path near Windows MAX_PATH limit
        string longPackageName = new string('a', 50);
        string path = $@"C:\Users\verylongusername\AppData\Local\NuGet\packages\{longPackageName}\1.0.0\lib\net6.0\{longPackageName}.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.PackageId.ShouldBe(longPackageName);
    }

    #endregion

    #region ExtractNuGetInfo Tests - Cross-Platform Paths

    [TestMethod]
    public void ExtractNuGetInfo_WithLinuxPath_ExtractsCorrectly()
    {
        // Arrange
        string path = "/home/user/.nuget/packages/newtonsoft.json/13.0.3/lib/net6.0/Newtonsoft.Json.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.PackageId.ShouldBe("newtonsoft.json");
        result.Value.Version.ShouldBe("13.0.3");
        result.Value.Framework.ShouldBe("net6.0");
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithMacPath_ExtractsCorrectly()
    {
        // Arrange
        string path = "/Users/developer/.nuget/packages/serilog/3.1.1/lib/netstandard2.0/Serilog.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.PackageId.ShouldBe("serilog");
        result.Value.Version.ShouldBe("3.1.1");
        result.Value.Framework.ShouldBe("netstandard2.0");
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithMixedSlashes_ExtractsCorrectly()
    {
        // Arrange - Mixed forward and back slashes
        string path = @"C:/Users/test\.nuget\packages/my.package\1.0.0/lib\net6.0/My.Package.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.PackageId.ShouldBe("my.package");
        result.Value.Version.ShouldBe("1.0.0");
        result.Value.Framework.ShouldBe("net6.0");
    }

    #endregion

    #region ExtractNuGetInfo Tests - Non-Standard Locations

    [TestMethod]
    public void ExtractNuGetInfo_WithCustomNuGetLocation_ExtractsCorrectly()
    {
        // Arrange - Custom NuGet packages location
        string path = @"D:\CustomNuGet\packages\my.package\1.0.0\lib\net6.0\My.Package.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.PackageId.ShouldBe("my.package");
    }

    [TestMethod]
    public void ExtractNuGetInfo_WithGlobalPackagesFolder_ExtractsCorrectly()
    {
        // Arrange - Alternative global packages folder structure
        string path = @"C:\ProgramData\NuGet\packages\my.package\1.0.0\lib\net6.0\My.Package.xml";

        // Act
        var result = NuGetHelper.ExtractNuGetInfo(path);

        // Assert
        result.ShouldNotBeNull();
        result.Value.PackageId.ShouldBe("my.package");
    }

    #endregion

    #region IsNuGetPath Tests

    [TestMethod]
    [DataRow(@"C:\Users\test\.nuget\packages\package\1.0.0\lib\net6.0\Package.xml", true)]
    [DataRow("/home/user/.nuget/packages/package/1.0.0/lib/net6.0/Package.xml", true)]
    [DataRow(@"C:\Projects\MyProject\bin\Debug\MyProject.xml", false)]
    [DataRow(@"C:\packages\something\else.xml", false)] // Contains "packages" but not valid NuGet structure
    [DataRow(@"D:\NuGet\packages\test\1.0.0\lib\net6.0\Test.xml", true)]
    [DataRow("", false)]
    public void IsNuGetPath_ReturnsCorrectResult(string path, bool expected)
    {
        // Act
        bool result = NuGetHelper.IsNuGetPath(path);

        // Assert
        result.ShouldBe(expected);
    }

    #endregion
}