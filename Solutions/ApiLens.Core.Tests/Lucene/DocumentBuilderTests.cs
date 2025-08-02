using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using Lucene.Net.Documents;
using Lucene.Net.Index;

namespace ApiLens.Core.Tests.Lucene;

[TestClass]
public class DocumentBuilderTests
{
    private DocumentBuilder builder = null!;

    [TestInitialize]
    public void Setup()
    {
        builder = new DocumentBuilder();
    }

    [TestMethod]
    public void BuildDocument_WithTypeInfo_CreatesDocumentWithAllFields()
    {
        // Arrange
        MemberInfo memberInfo = new()
        {
            Id = "T:System.Collections.Generic.List`1",
            MemberType = MemberType.Type,
            Name = "List`1",
            FullName = "System.Collections.Generic.List`1",
            Assembly = "System.Collections",
            Namespace = "System.Collections.Generic",
            Summary = "Represents a strongly typed list of objects.",
            Remarks = "List<T> is a generic collection."
        };

        // Act
        Document doc = builder.BuildDocument(memberInfo);

        // Assert
        doc.Get("id").ShouldBe("T:System.Collections.Generic.List`1");
        doc.Get("memberType").ShouldBe("Type");
        doc.Get("name").ShouldBe("List`1");
        doc.Get("fullName").ShouldBe("System.Collections.Generic.List`1");
        doc.Get("assembly").ShouldBe("System.Collections");
        doc.Get("namespace").ShouldBe("System.Collections.Generic");
        doc.Get("summary").ShouldBe("Represents a strongly typed list of objects.");
        doc.Get("remarks").ShouldBe("List<T> is a generic collection.");

        // Check searchable content field
        doc.Get("content").ShouldContain("List`1");
        doc.Get("content").ShouldContain("System.Collections.Generic.List`1");
        doc.Get("content").ShouldContain("Represents a strongly typed list");
    }

    [TestMethod]
    public void BuildDocument_WithMethodInfo_CreatesDocumentWithMethodFields()
    {
        // Arrange
        MemberInfo memberInfo = new()
        {
            Id = "M:System.String.Split(System.Char)",
            MemberType = MemberType.Method,
            Name = "Split",
            FullName = "System.String.Split(System.Char)",
            Assembly = "System.Runtime",
            Namespace = "System",
            Summary = "Splits a string into substrings."
        };

        // Act
        Document doc = builder.BuildDocument(memberInfo);

        // Assert
        doc.Get("id").ShouldBe("M:System.String.Split(System.Char)");
        doc.Get("memberType").ShouldBe("Method");
        doc.Get("name").ShouldBe("Split");
        doc.Get("fullName").ShouldBe("System.String.Split(System.Char)");
    }

    [TestMethod]
    public void BuildDocument_WithMinimalInfo_OmitsOptionalFields()
    {
        // Arrange
        MemberInfo memberInfo = new()
        {
            Id = "F:System.String.Empty",
            MemberType = MemberType.Field,
            Name = "Empty",
            FullName = "System.String.Empty",
            Assembly = "System.Runtime",
            Namespace = "System",
            Summary = null,
            Remarks = null
        };

        // Act
        Document doc = builder.BuildDocument(memberInfo);

        // Assert
        doc.Get("summary").ShouldBeNull();
        doc.Get("remarks").ShouldBeNull();
        doc.Get("content").ShouldNotBeNull(); // Content should still be created
    }

    [TestMethod]
    public void BuildDocument_WithCrossReferences_IncludesRelatedTypes()
    {
        // Arrange
        MemberInfo memberInfo = new()
        {
            Id = "T:System.Collections.Generic.List`1",
            MemberType = MemberType.Type,
            Name = "List`1",
            FullName = "System.Collections.Generic.List`1",
            Assembly = "System.Collections",
            Namespace = "System.Collections.Generic",
            CrossReferences =
            [
                new CrossReference
                {
                    SourceId = "T:System.Collections.Generic.List`1",
                    TargetId = "T:System.Collections.Generic.IList`1",
                    Type = ReferenceType.SeeAlso,
                    Context = "seealso"
                }
            ],
            RelatedTypes = ["System.Collections.Generic.IList`1", "System.Collections.IList"]
        };

        // Act
        Document doc = builder.BuildDocument(memberInfo);

        // Assert
        // Check that cross-references are handled (implementation to be added)
        doc.ShouldNotBeNull();
        // In a full implementation, we'd add fields for cross-references
    }

    [TestMethod]
    public void BuildDocument_WithNullMemberInfo_ThrowsArgumentNullException()
    {
        // Act & Assert
        MemberInfo? nullMember = null;
        Should.Throw<ArgumentNullException>(() => builder.BuildDocument(nullMember!));
    }

    [TestMethod]
    public void BuildDocument_WithTypeInfo_IncludesTypeSpecificFields()
    {
        // Arrange
        TypeInfo typeInfo = new()
        {
            Id = "T:System.Collections.Generic.Dictionary`2",
            Name = "Dictionary`2",
            FullName = "System.Collections.Generic.Dictionary`2",
            Assembly = "System.Collections",
            Namespace = "System.Collections.Generic",
            Kind = TypeKind.Class,
            BaseType = "System.Object",
            Interfaces = ["IDictionary`2", "ICollection`1", "IEnumerable`1"],
            IsGeneric = true,
            GenericArity = 2
        };

        // Act
        Document doc = builder.BuildDocument(typeInfo, "Represents a collection of keys and values.");

        // Assert
        doc.Get("baseType").ShouldBe("System.Object");
        doc.Get("isGeneric").ShouldBe("true");
        doc.Get("typeKind").ShouldBe("Class");
        doc.GetField("genericArity").GetInt32Value().ShouldBe(2);

        // Check interfaces
        IIndexableField[]? interfaceFields = doc.GetFields("interface");
        interfaceFields.Length.ShouldBe(3);
    }

    [TestMethod]
    public void BuildDocument_WithMethodInfo_IncludesMethodSpecificFields()
    {
        // Arrange
        MethodInfo methodInfo = new()
        {
            Id = "M:System.String.Format(System.String,System.Object[])",
            Name = "Format",
            FullName = "System.String.Format(System.String,System.Object[])",
            DeclaringType = "System.String",
            ReturnType = "System.String",
            IsStatic = true,
            IsExtension = false,
            IsAsync = false,
            IsGeneric = false,
            GenericArity = 0,
            Parameters =
            [
                new ParameterInfo
                {
                    Name = "format",
                    Type = "System.String",
                    Position = 0,
                    IsOptional = false,
                    IsParams = false,
                    IsOut = false,
                    IsRef = false
                },
                new ParameterInfo
                {
                    Name = "args",
                    Type = "System.Object[]",
                    Position = 1,
                    IsOptional = false,
                    IsParams = true,
                    IsOut = false,
                    IsRef = false
                }
            ],
            Summary = "Replaces format items in a string with string representations of corresponding objects."
        };

        // Act
        Document doc = builder.BuildDocument(methodInfo);

        // Assert
        doc.Get("returnType").ShouldBe("System.String");
        doc.Get("isStatic").ShouldBe("true");

        // Check parameters
        IIndexableField[]? parameterFields = doc.GetFields("parameter");
        parameterFields.Length.ShouldBe(2);
        parameterFields[0].GetStringValue().ShouldContain("System.String format");
        parameterFields[1].GetStringValue().ShouldContain("System.Object[] args");
    }

    [TestMethod]
    public void BuildDocument_AddsTypeSpecificFieldsForFiltering()
    {
        // Arrange
        MemberInfo memberInfo = new()
        {
            Id = "M:System.Linq.Enumerable.Where``1(System.Collections.Generic.IEnumerable{``0},System.Func{``0,System.Boolean})",
            MemberType = MemberType.Method,
            Name = "Where``1",
            FullName = "System.Linq.Enumerable.Where``1(System.Collections.Generic.IEnumerable{``0},System.Func{``0,System.Boolean})",
            Assembly = "System.Linq",
            Namespace = "System.Linq",
            Summary = "Filters a sequence based on a predicate."
        };

        // Act
        Document doc = builder.BuildDocument(memberInfo);

        // Assert
        // Should have a facet field for member type
        doc.Get("memberTypeFacet").ShouldBe("Method");

        // Should have a searchable field specific to methods
        IIndexableField? methodSearchField = doc.GetField("methodSearch");
        methodSearchField.ShouldNotBeNull();
        methodSearchField.GetStringValue().ShouldContain("Where");
    }

    [TestMethod]
    public void BuildDocument_AddsPropertySpecificFields()
    {
        // Arrange
        MemberInfo propertyInfo = new()
        {
            Id = "P:System.String.Length",
            MemberType = MemberType.Property,
            Name = "Length",
            FullName = "System.String.Length",
            Assembly = "System.Runtime",
            Namespace = "System",
            Summary = "Gets the number of characters in the current String object."
        };

        // Act
        Document doc = builder.BuildDocument(propertyInfo);

        // Assert
        doc.Get("memberTypeFacet").ShouldBe("Property");

        // Should have a searchable field specific to properties
        IIndexableField? propertySearchField = doc.GetField("propertySearch");
        propertySearchField.ShouldNotBeNull();
        propertySearchField.GetStringValue().ShouldContain("Length");
    }

    [TestMethod]
    public void BuildDocument_AddsEventSpecificFields()
    {
        // Arrange
        MemberInfo eventInfo = new()
        {
            Id = "E:System.AppDomain.DomainUnload",
            MemberType = MemberType.Event,
            Name = "DomainUnload",
            FullName = "System.AppDomain.DomainUnload",
            Assembly = "System.Runtime",
            Namespace = "System",
            Summary = "Occurs when an AppDomain is about to be unloaded."
        };

        // Act
        Document doc = builder.BuildDocument(eventInfo);

        // Assert
        doc.Get("memberTypeFacet").ShouldBe("Event");

        // Should have a searchable field specific to events
        IIndexableField? eventSearchField = doc.GetField("eventSearch");
        eventSearchField.ShouldNotBeNull();
        eventSearchField.GetStringValue().ShouldContain("DomainUnload");
    }

    [TestMethod]
    public void BuildDocument_AddsFieldSpecificFields()
    {
        // Arrange
        MemberInfo fieldInfo = new()
        {
            Id = "F:System.String.Empty",
            MemberType = MemberType.Field,
            Name = "Empty",
            FullName = "System.String.Empty",
            Assembly = "System.Runtime",
            Namespace = "System",
            Summary = "Represents the empty string. This field is read-only."
        };

        // Act
        Document doc = builder.BuildDocument(fieldInfo);

        // Assert
        doc.Get("memberTypeFacet").ShouldBe("Field");

        // Should have a searchable field specific to fields
        IIndexableField? fieldSearchField = doc.GetField("fieldSearch");
        fieldSearchField.ShouldNotBeNull();
        fieldSearchField.GetStringValue().ShouldContain("Empty");
    }

    [TestMethod]
    public void BuildDocument_TypeSpecificSearchFieldsUseCustomAnalyzer()
    {
        // Arrange
        MemberInfo typeInfo = new()
        {
            Id = "T:System.Collections.Generic.Dictionary`2",
            MemberType = MemberType.Type,
            Name = "Dictionary`2",
            FullName = "System.Collections.Generic.Dictionary`2",
            Assembly = "System.Collections",
            Namespace = "System.Collections.Generic",
            Summary = "Represents a collection of keys and values."
        };

        // Act
        Document doc = builder.BuildDocument(typeInfo);

        // Assert
        // Type-specific search field should exist
        IIndexableField? typeSearchField = doc.GetField("typeSearch");
        typeSearchField.ShouldNotBeNull();
        typeSearchField.GetStringValue().ShouldContain("Dictionary`2");
    }
}