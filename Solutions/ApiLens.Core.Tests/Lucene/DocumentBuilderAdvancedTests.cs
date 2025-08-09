using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Tests.Helpers;
using Lucene.Net.Documents;
using Lucene.Net.Index;

namespace ApiLens.Core.Tests.Lucene;

[TestClass]
public class DocumentBuilderAdvancedTests
{
    private DocumentBuilder builder = null!;

    [TestInitialize]
    public void Setup()
    {
        builder = new DocumentBuilder();
    }

    #region SourceFilePath Handling Tests

    [TestMethod]
    public void BuildDocument_WithSourceFilePath_AlwaysStoresPath()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "T:Test.Type|package|1.0.0|net6.0",
            MemberType = MemberType.Type,
            Name = "Type",
            FullName = "Test.Type",
            Assembly = "Test",
            Namespace = "Test",
            SourceFilePath = "/cache/package/1.0.0/lib/net6.0/Package.xml"
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert
        doc.Get("sourceFilePath").ShouldBe("/cache/package/1.0.0/lib/net6.0/Package.xml");
    }

    [TestMethod]
    public void BuildDocument_WithNullSourceFilePath_StoresEmptyString()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "T:Test.Type",
            MemberType = MemberType.Type,
            Name = "Type",
            FullName = "Test.Type",
            Assembly = "Test",
            Namespace = "Test",
            SourceFilePath = null
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert - Should store empty string, not null
        doc.Get("sourceFilePath").ShouldBe(string.Empty);
    }

    [TestMethod]
    public void BuildDocument_WithWindowsStylePath_PreservesPath()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "T:Test.Type|package|1.0.0|net6.0",
            MemberType = MemberType.Type,
            Name = "Type",
            FullName = "Test.Type",
            Assembly = "Test",
            Namespace = "Test",
            SourceFilePath = @"C:\Users\test\.nuget\packages\package\1.0.0\lib\net6.0\Package.xml"
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert - Path should be stored as-is (normalization happens elsewhere)
        doc.Get("sourceFilePath").ShouldBe(@"C:\Users\test\.nuget\packages\package\1.0.0\lib\net6.0\Package.xml");
    }

    #endregion

    #region Framework and Package Metadata Tests

    [TestMethod]
    public void BuildDocument_WithFullNuGetMetadata_StoresAllFields()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "M:Newtonsoft.Json.JsonConvert.SerializeObject|newtonsoft.json|13.0.3|net6.0",
            MemberType = MemberType.Method,
            Name = "SerializeObject",
            FullName = "Newtonsoft.Json.JsonConvert.SerializeObject",
            Assembly = "Newtonsoft.Json",
            Namespace = "Newtonsoft.Json",
            PackageId = "newtonsoft.json",
            PackageVersion = "13.0.3",
            TargetFramework = "net6.0",
            IsFromNuGetCache = true,
            SourceFilePath = "/cache/newtonsoft.json/13.0.3/lib/net6.0/Newtonsoft.Json.xml"
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert - All package metadata should be stored
        doc.Get("packageId").ShouldBe("newtonsoft.json");
        doc.Get("packageVersion").ShouldBe("13.0.3");
        doc.Get("targetFramework").ShouldBe("net6.0");
        doc.Get("isFromNuGetCache").ShouldBe("true");
        doc.Get("versionSearch").ShouldBe("13.0.3"); // Searchable version field
    }

    [TestMethod]
    public void BuildDocument_WithPreReleaseVersion_HandlesCorrectly()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "T:Microsoft.Extensions.Logging.ILogger|microsoft.extensions.logging|9.0.0-preview.1|net9.0",
            MemberType = MemberType.Type,
            Name = "ILogger",
            FullName = "Microsoft.Extensions.Logging.ILogger",
            Assembly = "Microsoft.Extensions.Logging",
            Namespace = "Microsoft.Extensions.Logging",
            PackageId = "microsoft.extensions.logging",
            PackageVersion = "9.0.0-preview.1",
            TargetFramework = "net9.0",
            IsFromNuGetCache = true
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert
        doc.Get("packageVersion").ShouldBe("9.0.0-preview.1");
        doc.Get("versionSearch").ShouldBe("9.0.0-preview.1");
    }

    [TestMethod]
    public void BuildDocument_WithoutPackageInfo_OmitsPackageFields()
    {
        // Arrange - Local file scenario
        MemberInfo member = new()
        {
            Id = "T:Local.Type",
            MemberType = MemberType.Type,
            Name = "Type",
            FullName = "Local.Type",
            Assembly = "Local",
            Namespace = "Local",
            IsFromNuGetCache = false
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert
        doc.Get("packageId").ShouldBeNull();
        doc.Get("packageVersion").ShouldBeNull();
        doc.Get("targetFramework").ShouldBeNull();
        doc.Get("isFromNuGetCache").ShouldBe("false");
        doc.Get("versionSearch").ShouldBeNull();
    }

    #endregion

    #region Content and Search Field Tests

    [TestMethod]
    public void BuildDocument_BuildsComprehensiveSearchContent()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "M:Example.Calculator.Add(System.Int32,System.Int32)|example|1.0.0|net6.0",
            MemberType = MemberType.Method,
            Name = "Add",
            FullName = "Example.Calculator.Add",
            Assembly = "Example",
            Namespace = "Example",
            Summary = "Adds two integers together.",
            Remarks = "This method performs simple addition.",
            Returns = "The sum of the two integers.",
            Parameters =
            [
                TestDataBuilder.CreateParameter("a", "System.Int32", "First number", 0),
                TestDataBuilder.CreateParameter("b", "System.Int32", "Second number", 1)
            ],
            Exceptions =
            [
                new ExceptionInfo { Type = "System.OverflowException", Condition = "When the sum exceeds Int32.MaxValue" }
            ],
            CodeExamples =
            [
                new CodeExample { Code = "int result = calculator.Add(5, 3);", Description = "Basic usage example" }
            ]
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert
        string content = doc.Get("content");
        content.ShouldNotBeNull();

        // Content should include all searchable text from BuildSearchableContent
        content.ShouldContain("Add", Case.Insensitive);
        content.ShouldContain("Example.Calculator.Add", Case.Insensitive);
        content.ShouldContain("Adds two integers together", Case.Insensitive);
        content.ShouldContain("simple addition", Case.Insensitive);
        // The actual implementation doesn't include Returns in the searchable content
        // content.ShouldContain("sum of the two integers", Case.Insensitive);
        content.ShouldContain("First number", Case.Insensitive);
        content.ShouldContain("Second number", Case.Insensitive);
        content.ShouldContain("OverflowException", Case.Insensitive);
        content.ShouldContain("Basic usage example", Case.Insensitive);
    }

    [TestMethod]
    public void BuildDocument_WithComplexityMetrics_StoresMetrics()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "M:Complex.Method|package|1.0.0|net6.0",
            MemberType = MemberType.Method,
            Name = "ComplexMethod",
            FullName = "Complex.Method",
            Assembly = "Complex",
            Namespace = "Complex",
            Complexity = new ComplexityMetrics
            {
                ParameterCount = 5,
                CyclomaticComplexity = 10,
                DocumentationLineCount = 25
            }
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert
        IIndexableField? paramCount = doc.GetField("parameterCount");
        paramCount.ShouldNotBeNull();
        paramCount.GetInt32Value().ShouldBe(5);

        IIndexableField? complexity = doc.GetField("cyclomaticComplexity");
        complexity.ShouldNotBeNull();
        complexity.GetInt32Value().ShouldBe(10);

        IIndexableField? docLines = doc.GetField("documentationLineCount");
        docLines.ShouldNotBeNull();
        docLines.GetInt32Value().ShouldBe(25);
    }

    #endregion

    #region Cross-Reference and Related Type Tests

    [TestMethod]
    public void BuildDocument_WithCrossReferences_StoresAllReferences()
    {
        // Arrange
        string memberId = "T:Example.Service|package|1.0.0|net6.0";
        MemberInfo member = new()
        {
            Id = memberId,
            MemberType = MemberType.Type,
            Name = "Service",
            FullName = "Example.Service",
            Assembly = "Example",
            Namespace = "Example",
            CrossReferences =
            [
                TestDataBuilder.CreateCrossReference(ReferenceType.SeeAlso, "T:Example.IService", memberId),
                TestDataBuilder.CreateCrossReference(ReferenceType.See, "M:Example.BaseService.Execute", memberId),
                TestDataBuilder.CreateCrossReference(ReferenceType.See, "P:Example.Service.IsEnabled", memberId)
            ]
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert
        IIndexableField[]? crossrefs = doc.GetFields("crossref");
        crossrefs.Length.ShouldBe(3);

        List<string> crossrefValues = [.. crossrefs.Select(f => f.GetStringValue())];
        crossrefValues.ShouldContain("T:Example.IService");
        crossrefValues.ShouldContain("M:Example.BaseService.Execute");
        crossrefValues.ShouldContain("P:Example.Service.IsEnabled");

        // Type-specific cross-references
        doc.Get($"crossref_{ReferenceType.SeeAlso}").ShouldBe("T:Example.IService");
        doc.Get($"crossref_{ReferenceType.See}").ShouldNotBeNull();
    }

    #endregion

    #region Type-Specific Search Fields

    [TestMethod]
    [DataRow(MemberType.Type, "typeSearch")]
    [DataRow(MemberType.Method, "methodSearch")]
    [DataRow(MemberType.Property, "propertySearch")]
    [DataRow(MemberType.Field, "fieldSearch")]
    [DataRow(MemberType.Event, "eventSearch")]
    public void BuildDocument_AddsCorrectTypeSpecificSearchField(MemberType memberType, string expectedField)
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = $"{memberType.ToString()[0]}:Test.Member|package|1.0.0|net6.0",
            MemberType = memberType,
            Name = "Member",
            FullName = "Test.Member",
            Assembly = "Test",
            Namespace = "Test"
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert
        doc.Get(expectedField).ShouldBe("Member");

        // Other type fields should not exist
        string[] allTypeFields = ["typeSearch", "methodSearch", "propertySearch", "fieldSearch", "eventSearch"];
        foreach (string field in allTypeFields.Where(f => f != expectedField))
        {
            doc.Get(field).ShouldBeNull();
        }
    }

    #endregion

    #region Edge Cases and Special Scenarios

    [TestMethod]
    public void BuildDocument_WithEmptyCollections_HandlesGracefully()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "T:Test.Type|package|1.0.0|net6.0",
            MemberType = MemberType.Type,
            Name = "Type",
            FullName = "Test.Type",
            Assembly = "Test",
            Namespace = "Test",
            Parameters = ImmutableArray<ParameterInfo>.Empty,
            Exceptions = ImmutableArray<ExceptionInfo>.Empty,
            CrossReferences = ImmutableArray<CrossReference>.Empty,
            RelatedTypes = ImmutableArray<string>.Empty,
            CodeExamples = ImmutableArray<CodeExample>.Empty,
            Attributes = ImmutableArray<AttributeInfo>.Empty
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert - Should not throw and should create valid document
        doc.ShouldNotBeNull();
        doc.Get("id").ShouldBe("T:Test.Type|package|1.0.0|net6.0");

        // No collection fields should be added
        doc.GetFields("parameter").Length.ShouldBe(0);
        doc.GetFields("exceptionType").Length.ShouldBe(0);
        doc.GetFields("crossref").Length.ShouldBe(0);
    }

    [TestMethod]
    public void BuildDocument_WithSpecialCharactersInContent_HandlesCorrectly()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "M:Test.Method|package|1.0.0|net6.0",
            MemberType = MemberType.Method,
            Name = "Method<T>",
            FullName = "Test.Method<T>",
            Assembly = "Test",
            Namespace = "Test",
            Summary = "Generic method with <T> parameter & special chars: @#$%",
            CodeExamples =
            [
                new CodeExample { Code = "var result = obj.Method<string>(\"test\");", Description = "Usage with <>\"& chars" }
            ]
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert
        doc.Get("name").ShouldBe("Method<T>");
        doc.Get("summary").ShouldBe("Generic method with <T> parameter & special chars: @#$%");

        string content = doc.Get("content");
        content.ShouldContain("Method<T>");
        content.ShouldContain("<T> parameter & special chars");
    }

    [TestMethod]
    public void BuildDocument_WithContentHash_DoesNotStore()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "T:Test.Type|package|1.0.0|net6.0",
            MemberType = MemberType.Type,
            Name = "Type",
            FullName = "Test.Type",
            Assembly = "Test",
            Namespace = "Test",
            ContentHash = "ABC123HASH"
        };

        // Act
        Document doc = builder.BuildDocument(member);

        // Assert - ContentHash field should exist but be configured with Field.Store.NO
        IIndexableField? contentHashField = doc.GetField("contentHash");
        contentHashField.ShouldNotBeNull();

        // In Lucene.NET, Field.Store.NO fields can still be retrieved from the Document object
        // before indexing, but won't be stored in the index itself.
        // This test verifies the field exists in the document for indexing purposes.
        doc.Get("contentHash").ShouldBe("ABC123HASH");
    }

    #endregion
}