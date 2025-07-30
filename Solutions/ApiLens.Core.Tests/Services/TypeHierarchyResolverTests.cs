using ApiLens.Core.Models;
using ApiLens.Core.Querying;
using ApiLens.Core.Services;

namespace ApiLens.Core.Tests.Services;

[TestClass]
public class TypeHierarchyResolverTests
{
    private TypeHierarchyResolver resolver = null!;
    private IQueryEngine mockQueryEngine = null!;

    [TestInitialize]
    public void Setup()
    {
        mockQueryEngine = Substitute.For<IQueryEngine>();
        resolver = new TypeHierarchyResolver(mockQueryEngine);
    }

    [TestMethod]
    public void GetBaseTypeChain_WithRelatedTypes_ReturnsBaseTypes()
    {
        // Arrange
        MemberInfo derivedType = new()
        {
            Id = "T:MyNamespace.DerivedClass",
            MemberType = MemberType.Type,
            Name = "DerivedClass",
            FullName = "MyNamespace.DerivedClass",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace",
            RelatedTypes = ["MyNamespace.BaseClass"]
        };

        MemberInfo baseType = new()
        {
            Id = "T:MyNamespace.BaseClass",
            MemberType = MemberType.Type,
            Name = "BaseClass",
            FullName = "MyNamespace.BaseClass",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace",
            RelatedTypes = ["System.Object"]
        };

        mockQueryEngine.SearchByName("DerivedClass", 10).Returns([derivedType]);
        mockQueryEngine.SearchByName("BaseClass", 10).Returns([baseType]);
        mockQueryEngine.SearchByName("Object", 10).Returns([]);

        // Act
        List<MemberInfo> baseTypes = resolver.GetBaseTypeChain("MyNamespace.DerivedClass");

        // Assert
        baseTypes.Count.ShouldBe(2);
        baseTypes[0].FullName.ShouldBe("MyNamespace.BaseClass");
        baseTypes[1].FullName.ShouldBe("System.Object");
    }

    [TestMethod]
    public void GetDerivedTypes_WithRelatedTypes_ReturnsDerivedTypes()
    {
        // Arrange
        MemberInfo baseType = new()
        {
            Id = "T:MyNamespace.BaseClass",
            MemberType = MemberType.Type,
            Name = "BaseClass",
            FullName = "MyNamespace.BaseClass",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace"
        };

        MemberInfo derivedType1 = new()
        {
            Id = "T:MyNamespace.DerivedClass1",
            MemberType = MemberType.Type,
            Name = "DerivedClass1",
            FullName = "MyNamespace.DerivedClass1",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace",
            RelatedTypes = ["MyNamespace.BaseClass"]
        };

        MemberInfo derivedType2 = new()
        {
            Id = "T:MyNamespace.DerivedClass2",
            MemberType = MemberType.Type,
            Name = "DerivedClass2",
            FullName = "MyNamespace.DerivedClass2",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace",
            RelatedTypes = ["MyNamespace.BaseClass"]
        };

        MemberInfo unrelatedType = new()
        {
            Id = "T:MyNamespace.UnrelatedClass",
            MemberType = MemberType.Type,
            Name = "UnrelatedClass",
            FullName = "MyNamespace.UnrelatedClass",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace",
            RelatedTypes = []
        };

        mockQueryEngine.GetByType(MemberType.Type, 1000)
            .Returns([derivedType1, derivedType2, unrelatedType]);

        // Act
        List<MemberInfo> derivedTypes = resolver.GetDerivedTypes("MyNamespace.BaseClass");

        // Assert
        derivedTypes.Count.ShouldBe(2);
        derivedTypes.ShouldContain(t => t.FullName == "MyNamespace.DerivedClass1");
        derivedTypes.ShouldContain(t => t.FullName == "MyNamespace.DerivedClass2");
        derivedTypes.ShouldNotContain(t => t.FullName == "MyNamespace.UnrelatedClass");
    }

    [TestMethod]
    public void GetRelatedTypes_WithExistingType_ReturnsRelatedTypes()
    {
        // Arrange
        MemberInfo targetType = new()
        {
            Id = "T:MyNamespace.MyClass",
            MemberType = MemberType.Type,
            Name = "MyClass",
            FullName = "MyNamespace.MyClass",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace",
            RelatedTypes = ["MyNamespace.IMyInterface", "MyNamespace.BaseClass"]
        };

        MemberInfo interfaceType = new()
        {
            Id = "T:MyNamespace.IMyInterface",
            MemberType = MemberType.Type,
            Name = "IMyInterface",
            FullName = "MyNamespace.IMyInterface",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace"
        };

        mockQueryEngine.GetById("T:MyNamespace.MyClass").Returns(targetType);
        mockQueryEngine.GetById("T:MyNamespace.IMyInterface").Returns(interfaceType);
        mockQueryEngine.GetById("T:MyNamespace.BaseClass").Returns((MemberInfo?)null);

        // Act
        List<MemberInfo> relatedTypes = resolver.GetRelatedTypes("MyNamespace.MyClass");

        // Assert
        relatedTypes.Count.ShouldBe(2);
        relatedTypes[0].FullName.ShouldBe("MyNamespace.IMyInterface");
        relatedTypes[1].FullName.ShouldBe("MyNamespace.BaseClass");
        relatedTypes[1].Assembly.ShouldBe("Unknown"); // Placeholder for unresolved type
    }

    [TestMethod]
    public void GetRelatedTypes_WithNonExistentType_ReturnsEmpty()
    {
        // Arrange
        mockQueryEngine.GetById("T:NonExistent.Type").Returns((MemberInfo?)null);
        mockQueryEngine.SearchByName("Type", 10).Returns([]);

        // Act
        List<MemberInfo> relatedTypes = resolver.GetRelatedTypes("NonExistent.Type");

        // Assert
        relatedTypes.ShouldBeEmpty();
    }

    [TestMethod]
    public void GetBaseTypeChain_WithNoRelatedTypes_ReturnsEmpty()
    {
        // Arrange
        MemberInfo type = new()
        {
            Id = "T:System.Object",
            MemberType = MemberType.Type,
            Name = "Object",
            FullName = "System.Object",
            Assembly = "System.Runtime",
            Namespace = "System",
            RelatedTypes = []
        };

        mockQueryEngine.SearchByName("Object", 10).Returns([type]);

        // Act
        List<MemberInfo> baseTypes = resolver.GetBaseTypeChain("System.Object");

        // Assert
        baseTypes.ShouldBeEmpty();
    }

    [TestMethod]
    public void GetBaseTypeChain_WithCircularReference_HandlesGracefully()
    {
        // Arrange
        MemberInfo typeA = new()
        {
            Id = "T:MyNamespace.TypeA",
            MemberType = MemberType.Type,
            Name = "TypeA",
            FullName = "MyNamespace.TypeA",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace",
            RelatedTypes = ["MyNamespace.TypeB"]
        };

        MemberInfo typeB = new()
        {
            Id = "T:MyNamespace.TypeB",
            MemberType = MemberType.Type,
            Name = "TypeB",
            FullName = "MyNamespace.TypeB",
            Assembly = "MyAssembly",
            Namespace = "MyNamespace",
            RelatedTypes = ["MyNamespace.TypeA"]
        };

        mockQueryEngine.SearchByName("TypeA", 10).Returns([typeA]);
        mockQueryEngine.SearchByName("TypeB", 10).Returns([typeB]);

        // Act
        List<MemberInfo> baseTypes = resolver.GetBaseTypeChain("MyNamespace.TypeA");

        // Assert
        // Should not get stuck in infinite loop
        baseTypes.Count.ShouldBe(1);
        baseTypes[0].FullName.ShouldBe("MyNamespace.TypeB");
    }
}