using ApiLens.Core.Models;
using ApiLens.Core.Parsing;

namespace ApiLens.Core.Tests.Parsing;

[TestClass]
public class MemberIdParserTests
{

    [TestMethod]
    public void Parse_WithTypeId_ExtractsTypeInfo()
    {
        // Arrange
        const string memberId = "T:System.String";

        // Act
        ParsedMemberId result = MemberIdParser.Parse(memberId);

        // Assert
        result.ShouldNotBeNull();
        result.MemberType.ShouldBe(MemberType.Type);
        result.Namespace.ShouldBe("System");
        result.TypeName.ShouldBe("String");
        result.MemberName.ShouldBe("String");
        result.FullName.ShouldBe("System.String");
        result.Parameters.ShouldBeEmpty();
    }

    [TestMethod]
    public void Parse_WithGenericTypeId_ExtractsGenericInfo()
    {
        // Arrange
        const string memberId = "T:System.Collections.Generic.List`1";

        // Act
        ParsedMemberId result = MemberIdParser.Parse(memberId);

        // Assert
        result.MemberType.ShouldBe(MemberType.Type);
        result.Namespace.ShouldBe("System.Collections.Generic");
        result.TypeName.ShouldBe("List`1");
        result.MemberName.ShouldBe("List`1");
        result.FullName.ShouldBe("System.Collections.Generic.List`1");
        result.GenericArity.ShouldBe(1);
    }

    [TestMethod]
    public void Parse_WithNestedTypeId_ExtractsNestedInfo()
    {
        // Arrange
        const string memberId = "T:System.Environment+SpecialFolder";

        // Act
        ParsedMemberId result = MemberIdParser.Parse(memberId);

        // Assert
        result.MemberType.ShouldBe(MemberType.Type);
        result.Namespace.ShouldBe("System");
        result.TypeName.ShouldBe("Environment+SpecialFolder");
        result.MemberName.ShouldBe("Environment+SpecialFolder");
        result.FullName.ShouldBe("System.Environment+SpecialFolder");
        result.IsNested.ShouldBe(true);
        result.ParentType.ShouldBe("Environment");
        result.NestedTypeName.ShouldBe("SpecialFolder");
    }

    [TestMethod]
    public void Parse_WithMethodId_ExtractsMethodInfo()
    {
        // Arrange
        const string memberId = "M:System.String.Split(System.Char)";

        // Act
        ParsedMemberId result = MemberIdParser.Parse(memberId);

        // Assert
        result.MemberType.ShouldBe(MemberType.Method);
        result.Namespace.ShouldBe("System");
        result.TypeName.ShouldBe("String");
        result.MemberName.ShouldBe("Split");
        result.FullName.ShouldBe("System.String.Split(System.Char)");
        result.Parameters.Length.ShouldBe(1);
        result.Parameters[0].ShouldBe("System.Char");
    }

    [TestMethod]
    public void Parse_WithPropertyId_ExtractsPropertyInfo()
    {
        // Arrange
        const string memberId = "P:System.String.Length";

        // Act
        ParsedMemberId result = MemberIdParser.Parse(memberId);

        // Assert
        result.MemberType.ShouldBe(MemberType.Property);
        result.Namespace.ShouldBe("System");
        result.TypeName.ShouldBe("String");
        result.MemberName.ShouldBe("Length");
        result.FullName.ShouldBe("System.String.Length");
        result.Parameters.ShouldBeEmpty();
    }

    [TestMethod]
    public void Parse_WithInvalidFormat_ThrowsArgumentException()
    {
        // Arrange
        const string memberId = "InvalidFormat";

        // Act & Assert
        Should.Throw<ArgumentException>(() => MemberIdParser.Parse(memberId))
            .Message.ShouldContain("Invalid member ID format");
    }

    [TestMethod]
    public void Parse_WithUnknownPrefix_ThrowsArgumentException()
    {
        // Arrange
        const string memberId = "X:UnknownType";

        // Act & Assert
        Should.Throw<ArgumentException>(() => MemberIdParser.Parse(memberId))
            .Message.ShouldContain("Unknown member type prefix");
    }

    [TestMethod]
    public void Parse_WithMethodMultipleParameters_ExtractsAllParameters()
    {
        // Arrange
        const string memberId = "M:System.String.Join(System.String,System.String[])";

        // Act
        ParsedMemberId result = MemberIdParser.Parse(memberId);

        // Assert
        result.MemberType.ShouldBe(MemberType.Method);
        result.Namespace.ShouldBe("System");
        result.TypeName.ShouldBe("String");
        result.MemberName.ShouldBe("Join");
        result.Parameters.Length.ShouldBe(2);
        result.Parameters[0].ShouldBe("System.String");
        result.Parameters[1].ShouldBe("System.String[]");
    }

    [TestMethod]
    public void Parse_WithFieldId_ExtractsFieldInfo()
    {
        // Arrange
        const string memberId = "F:System.String.Empty";

        // Act
        ParsedMemberId result = MemberIdParser.Parse(memberId);

        // Assert
        result.MemberType.ShouldBe(MemberType.Field);
        result.Namespace.ShouldBe("System");
        result.TypeName.ShouldBe("String");
        result.MemberName.ShouldBe("Empty");
        result.FullName.ShouldBe("System.String.Empty");
        result.Parameters.ShouldBeEmpty();
    }

    [TestMethod]
    public void Parse_WithEventId_ExtractsEventInfo()
    {
        // Arrange
        const string memberId = "E:System.AppDomain.ProcessExit";

        // Act
        ParsedMemberId result = MemberIdParser.Parse(memberId);

        // Assert
        result.MemberType.ShouldBe(MemberType.Event);
        result.Namespace.ShouldBe("System");
        result.TypeName.ShouldBe("AppDomain");
        result.MemberName.ShouldBe("ProcessExit");
        result.FullName.ShouldBe("System.AppDomain.ProcessExit");
        result.Parameters.ShouldBeEmpty();
    }

    [TestMethod]
    public void Parse_WithGenericMethod_ExtractsGenericParameters()
    {
        // Arrange
        const string memberId = "M:System.Linq.Enumerable.Select``2(System.Collections.Generic.IEnumerable{``0},System.Func{``0,``1})";

        // Act
        ParsedMemberId result = MemberIdParser.Parse(memberId);

        // Assert
        result.MemberType.ShouldBe(MemberType.Method);
        result.Namespace.ShouldBe("System.Linq");
        result.TypeName.ShouldBe("Enumerable");
        result.MemberName.ShouldBe("Select``2");
        result.Parameters.Length.ShouldBe(2);
        result.Parameters[0].ShouldBe("System.Collections.Generic.IEnumerable{``0}");
        result.Parameters[1].ShouldBe("System.Func{``0,``1}");
    }
}