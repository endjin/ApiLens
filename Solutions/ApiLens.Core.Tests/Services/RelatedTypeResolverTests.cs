using ApiLens.Core.Models;
using ApiLens.Core.Querying;
using ApiLens.Core.Services;

namespace ApiLens.Core.Tests.Services;

[TestClass]
public class RelatedTypeResolverTests
{
    private RelatedTypeResolver resolver = null!;
    private IQueryEngine mockQueryEngine = null!;

    [TestInitialize]
    public void Setup()
    {
        mockQueryEngine = Substitute.For<IQueryEngine>();
        resolver = new RelatedTypeResolver(mockQueryEngine);
    }

    [TestMethod]
    public void GetRelatedTypes_FromCrossReferences_ReturnsReferencedTypes()
    {
        // Arrange
        MemberInfo sourceType = new()
        {
            Id = "T:MyNamespace.MyClass",
            MemberType = MemberType.Type,
            Name = "MyClass",
            FullName = "MyNamespace.MyClass",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace",
            CrossReferences =
            [
                new CrossReference
                {
                    SourceId = "T:MyNamespace.MyClass",
                    TargetId = "T:MyNamespace.IMyInterface",
                    Type = ReferenceType.SeeAlso,
                    Context = "seealso"
                },
                new CrossReference
                {
                    SourceId = "T:MyNamespace.MyClass",
                    TargetId = "T:System.IDisposable",
                    Type = ReferenceType.See,
                    Context = "see"
                }
            ]
        };

        mockQueryEngine.GetById("T:MyNamespace.MyClass").Returns(sourceType);

        // Act
        List<string> relatedTypes = resolver.GetRelatedTypes("T:MyNamespace.MyClass");

        // Assert
        relatedTypes.Count.ShouldBe(2);
        relatedTypes.ShouldContain("T:MyNamespace.IMyInterface");
        relatedTypes.ShouldContain("T:System.IDisposable");
    }

    [TestMethod]
    public void GetRelatedTypes_FromMethodParameters_ReturnsParameterTypes()
    {
        // Arrange
        MemberInfo method = new()
        {
            Id = "M:MyNamespace.MyClass.Process(System.String,System.Int32)",
            MemberType = MemberType.Method,
            Name = "Process",
            FullName = "MyNamespace.MyClass.Process(System.String,System.Int32)",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace"
        };

        mockQueryEngine.GetById("M:MyNamespace.MyClass.Process(System.String,System.Int32)").Returns(method);

        // Act
        List<string> relatedTypes = resolver.GetRelatedTypes("M:MyNamespace.MyClass.Process(System.String,System.Int32)");

        // Assert
        relatedTypes.Count.ShouldBe(3); // Declaring type + 2 parameters
        relatedTypes.ShouldContain("T:MyNamespace.MyClass");
        relatedTypes.ShouldContain("T:System.String");
        relatedTypes.ShouldContain("T:System.Int32");
    }

    [TestMethod]
    public void GetRelatedTypes_WithExceptionReferences_IncludesExceptionTypes()
    {
        // Arrange
        MemberInfo method = new()
        {
            Id = "M:MyNamespace.MyClass.Validate",
            MemberType = MemberType.Method,
            Name = "Validate",
            FullName = "MyNamespace.MyClass.Validate",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace",
            CrossReferences =
            [
                new CrossReference
                {
                    SourceId = "M:MyNamespace.MyClass.Validate",
                    TargetId = "T:System.ArgumentNullException",
                    Type = ReferenceType.Exception,
                    Context = "exception"
                },
                new CrossReference
                {
                    SourceId = "M:MyNamespace.MyClass.Validate",
                    TargetId = "T:System.InvalidOperationException",
                    Type = ReferenceType.Exception,
                    Context = "exception"
                }
            ]
        };

        mockQueryEngine.GetById("M:MyNamespace.MyClass.Validate").Returns(method);

        // Act
        List<string> relatedTypes = resolver.GetRelatedTypes("M:MyNamespace.MyClass.Validate");

        // Assert
        relatedTypes.Count.ShouldBe(3); // Declaring type + 2 exceptions
        relatedTypes.ShouldContain("T:MyNamespace.MyClass");
        relatedTypes.ShouldContain("T:System.ArgumentNullException");
        relatedTypes.ShouldContain("T:System.InvalidOperationException");
    }

    [TestMethod]
    public void GetRelatedTypes_WithGenericTypes_ExtractsGenericArguments()
    {
        // Arrange
        MemberInfo method = new()
        {
            Id = "M:MyNamespace.MyClass.GetItems",
            MemberType = MemberType.Method,
            Name = "GetItems",
            FullName = "MyNamespace.MyClass.GetItems",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace",
            Summary = "Returns List<String>"
        };

        mockQueryEngine.GetById("M:MyNamespace.MyClass.GetItems").Returns(method);

        // Act
        List<string> relatedTypes = resolver.GetRelatedTypes("M:MyNamespace.MyClass.GetItems");

        // Assert
        relatedTypes.Count.ShouldBeGreaterThan(0);
        relatedTypes.ShouldContain("T:MyNamespace.MyClass");
        // Should detect List and String from return type
        relatedTypes.ShouldContain("T:System.Collections.Generic.List`1");
        relatedTypes.ShouldContain("T:System.String");
    }

    [TestMethod]
    public void GetRelatedTypes_FromPropertyType_ReturnsPropertyType()
    {
        // Arrange
        MemberInfo property = new()
        {
            Id = "P:MyNamespace.MyClass.Name",
            MemberType = MemberType.Property,
            Name = "Name",
            FullName = "MyNamespace.MyClass.Name",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace"
        };

        mockQueryEngine.GetById("P:MyNamespace.MyClass.Name").Returns(property);

        // Act
        List<string> relatedTypes = resolver.GetRelatedTypes("P:MyNamespace.MyClass.Name");

        // Assert
        relatedTypes.Count.ShouldBeGreaterThan(0);
        relatedTypes.ShouldContain("T:MyNamespace.MyClass");
        relatedTypes.ShouldContain("T:System.String"); // Common property type
    }

    [TestMethod]
    public void GetRelatedTypes_WithDuplicates_ReturnsUniqueTypes()
    {
        // Arrange
        MemberInfo member = new()
        {
            Id = "T:MyNamespace.MyClass",
            MemberType = MemberType.Type,
            Name = "MyClass",
            FullName = "MyNamespace.MyClass",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace",
            RelatedTypes = ["System.String", "System.String", "System.Int32"],
            CrossReferences =
            [
                new CrossReference
                {
                    SourceId = "T:MyNamespace.MyClass",
                    TargetId = "T:System.String",
                    Type = ReferenceType.See,
                    Context = "see"
                }
            ]
        };

        mockQueryEngine.GetById("T:MyNamespace.MyClass").Returns(member);

        // Act
        List<string> relatedTypes = resolver.GetRelatedTypes("T:MyNamespace.MyClass");

        // Assert
        relatedTypes.Count.ShouldBe(2);
        relatedTypes.ShouldContain("T:System.String");
        relatedTypes.ShouldContain("T:System.Int32");
        relatedTypes.Count(t => t == "T:System.String").ShouldBe(1); // No duplicates
    }

    [TestMethod]
    public void GetRelatedTypes_ForNonExistentMember_ReturnsEmpty()
    {
        // Arrange
        mockQueryEngine.GetById("T:NonExistent.Type").Returns((MemberInfo?)null);

        // Act
        List<string> relatedTypes = resolver.GetRelatedTypes("T:NonExistent.Type");

        // Assert
        relatedTypes.ShouldBeEmpty();
    }
}