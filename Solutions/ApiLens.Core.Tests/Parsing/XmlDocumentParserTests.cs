using System.Xml.Linq;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Tests.Helpers;

namespace ApiLens.Core.Tests.Parsing;

[TestClass]
public class XmlDocumentParserTests
{
    private XmlDocumentParser parser = null!;

    [TestInitialize]
    public void Setup()
    {
        parser = TestHelpers.CreateTestXmlDocumentParser();
    }

    [TestMethod]
    public void ParseAssembly_WithValidXml_ExtractsAssemblyInfo()
    {
        // Arrange
        const string xml = """
            <?xml version="1.0"?>
            <doc>
                <assembly>
                    <name>Reaqtive.Core</name>
                </assembly>
                <members>
                </members>
            </doc>
            """;
        XDocument doc = XDocument.Parse(xml);

        // Act
        ApiAssemblyInfo assembly = parser.ParseAssembly(doc);

        // Assert
        assembly.Name.ShouldBe("Reaqtive.Core");
        assembly.Version.ShouldBe("0.0.0.0");
        assembly.Culture.ShouldBe("neutral");
    }

    [TestMethod]
    public void ParseMember_WithTypeElement_ReturnsTypeInfo()
    {
        // Arrange
        const string xml = """
            <member name="T:System.String">
                <summary>Represents text as a sequence of UTF-16 code units.</summary>
            </member>
            """;
        XElement element = XElement.Parse(xml);

        // Act
        MemberInfo? member = parser.ParseMember(element);

        // Assert
        member.ShouldNotBeNull();
        member.Id.ShouldBe("T:System.String");
        member.MemberType.ShouldBe(MemberType.Type);
        member.Name.ShouldBe("String");
        member.FullName.ShouldBe("System.String");
        member.Namespace.ShouldBe("System");
        member.Summary.ShouldBe("Represents text as a sequence of UTF-16 code units.");
    }

    [TestMethod]
    public void ParseMember_WithMethodElement_ReturnsMethodInfo()
    {
        // Arrange
        const string xml = """
            <member name="M:System.String.Split(System.Char)">
                <summary>Splits a string into substrings based on a specified delimiting character.</summary>
                <param name="separator">A character that delimits the substrings in this string.</param>
                <returns>An array whose elements contain the substrings from this instance.</returns>
            </member>
            """;
        XElement element = XElement.Parse(xml);

        // Act
        MemberInfo? member = parser.ParseMember(element);

        // Assert
        member.ShouldNotBeNull();
        member.Id.ShouldBe("M:System.String.Split(System.Char)");
        member.MemberType.ShouldBe(MemberType.Method);
        member.Name.ShouldBe("Split");
        member.Summary.ShouldBe("Splits a string into substrings based on a specified delimiting character.");
    }

    [TestMethod]
    public void ParseMember_WithPropertyElement_ReturnsPropertyInfo()
    {
        // Arrange
        const string xml = """
            <member name="P:System.String.Length">
                <summary>Gets the number of characters in the current String object.</summary>
            </member>
            """;
        XElement element = XElement.Parse(xml);

        // Act
        MemberInfo? member = parser.ParseMember(element);

        // Assert
        member.ShouldNotBeNull();
        member.Id.ShouldBe("P:System.String.Length");
        member.MemberType.ShouldBe(MemberType.Property);
        member.Name.ShouldBe("Length");
    }

    [TestMethod]
    public void ParseMember_WithInvalidName_ReturnsNull()
    {
        // Arrange
        const string xml = """<member><summary>No name attribute</summary></member>""";
        XElement element = XElement.Parse(xml);

        // Act
        MemberInfo? member = parser.ParseMember(element);

        // Assert
        member.ShouldBeNull();
    }

    [TestMethod]
    public void ParseMembers_WithMultipleMembers_ReturnsAll()
    {
        // Arrange
        const string xml = """
            <?xml version="1.0"?>
            <doc>
                <assembly>
                    <name>TestAssembly</name>
                </assembly>
                <members>
                    <member name="T:TestNamespace.TestClass">
                        <summary>Test class</summary>
                    </member>
                    <member name="M:TestNamespace.TestClass.TestMethod">
                        <summary>Test method</summary>
                    </member>
                </members>
            </doc>
            """;
        XDocument doc = XDocument.Parse(xml);

        // Act
        ImmutableArray<MemberInfo> members = parser.ParseMembers(doc, "TestAssembly");

        // Assert
        members.Length.ShouldBe(2);
        members[0].MemberType.ShouldBe(MemberType.Type);
        members[0].Name.ShouldBe("TestClass");
        members[0].Assembly.ShouldBe("TestAssembly");
        members[1].MemberType.ShouldBe(MemberType.Method);
        members[1].Name.ShouldBe("TestMethod");
        members[1].Assembly.ShouldBe("TestAssembly");
    }

    [TestMethod]
    public void LinkPropertyTypes_WithPropertyAndGetter_UpdatesPropertyType()
    {
        // Arrange
        var members = new List<MemberInfo>
        {
            new MemberInfo
            {
                Id = "P:TestNamespace.TestClass.TestProperty",
                Name = "TestProperty",
                FullName = "TestNamespace.TestClass.TestProperty",
                MemberType = MemberType.Property,
                Assembly = "TestAssembly",
                Namespace = "TestNamespace",
                ReturnType = null
            },
            new MemberInfo
            {
                Id = "M:TestNamespace.TestClass.get_TestProperty",
                Name = "get_TestProperty",
                FullName = "TestNamespace.TestClass.get_TestProperty",
                MemberType = MemberType.Method,
                Assembly = "TestAssembly",
                Namespace = "TestNamespace",
                ReturnType = "System.String"
            }
        };

        // Act
        XmlDocumentParser.LinkPropertyTypes(members);

        // Assert
        members[0].ReturnType.ShouldBe("System.String");
    }

    [TestMethod]
    public void LinkPropertyTypes_WithoutGetter_LeavesPropertyUnchanged()
    {
        // Arrange
        var members = new List<MemberInfo>
        {
            new MemberInfo
            {
                Id = "P:TestNamespace.TestClass.TestProperty",
                Name = "TestProperty",
                FullName = "TestNamespace.TestClass.TestProperty",
                MemberType = MemberType.Property,
                Assembly = "TestAssembly",
                Namespace = "TestNamespace",
                ReturnType = null
            }
        };

        // Act
        XmlDocumentParser.LinkPropertyTypes(members);

        // Assert
        members[0].ReturnType.ShouldBeNull();
    }

    [TestMethod]
    public void LinkPropertyTypes_WithFields_ExtractsTypeFromReturns()
    {
        // Arrange
        var members = new List<MemberInfo>
        {
            new MemberInfo
            {
                Id = "F:TestNamespace.TestClass.TestField",
                Name = "TestField",
                FullName = "TestNamespace.TestClass.TestField",
                MemberType = MemberType.Field,
                Assembly = "TestAssembly",
                Namespace = "TestNamespace",
                ReturnType = null,
                Returns = "An Int32 value representing the count"
            }
        };

        // Act
        XmlDocumentParser.LinkPropertyTypes(members);

        // Assert
        members[0].ReturnType.ShouldBe("Int32");
    }

    [TestMethod]
    public void LinkPropertyTypes_WithEmptyList_HandlesGracefully()
    {
        // Arrange
        var members = new List<MemberInfo>();

        // Act & Assert (should not throw)
        XmlDocumentParser.LinkPropertyTypes(members);
        members.Count.ShouldBe(0);
    }

    [TestMethod]
    public void LinkPropertyTypes_WithMultiplePropertiesAndGetters_LinksCorrectly()
    {
        // Arrange
        var members = new List<MemberInfo>
        {
            new MemberInfo
            {
                Id = "P:TestNamespace.TestClass.Property1",
                Name = "Property1",
                FullName = "TestNamespace.TestClass.Property1",
                MemberType = MemberType.Property,
                Assembly = "TestAssembly",
                Namespace = "TestNamespace"
            },
            new MemberInfo
            {
                Id = "P:TestNamespace.TestClass.Property2",
                Name = "Property2",
                FullName = "TestNamespace.TestClass.Property2",
                MemberType = MemberType.Property,
                Assembly = "TestAssembly",
                Namespace = "TestNamespace"
            },
            new MemberInfo
            {
                Id = "M:TestNamespace.TestClass.get_Property1",
                Name = "get_Property1",
                FullName = "TestNamespace.TestClass.get_Property1",
                MemberType = MemberType.Method,
                Assembly = "TestAssembly",
                Namespace = "TestNamespace",
                ReturnType = "System.String"
            },
            new MemberInfo
            {
                Id = "M:TestNamespace.TestClass.get_Property2",
                Name = "get_Property2",
                FullName = "TestNamespace.TestClass.get_Property2",
                MemberType = MemberType.Method,
                Assembly = "TestAssembly",
                Namespace = "TestNamespace",
                ReturnType = "System.Int32"
            }
        };

        // Act
        XmlDocumentParser.LinkPropertyTypes(members);

        // Assert
        members[0].ReturnType.ShouldBe("System.String");
        members[1].ReturnType.ShouldBe("System.Int32");
    }
}