using System.Xml.Linq;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Tests.Helpers;

namespace ApiLens.Core.Tests.Parsing;

[TestClass]
public class XmlMetadataParsingTests
{
    private readonly XmlDocumentParser parser = TestHelpers.CreateTestXmlDocumentParser();

    [TestMethod]
    public void ParseMember_WithCodeExample_ShouldExtractExample()
    {
        // Arrange
        XElement memberElement = XElement.Parse("""
            <member name="M:Test.Calculate(System.Int32)">
                <summary>Calculates a value.</summary>
                <example>
                    <code>
                    var result = Calculate(42);
                    Console.WriteLine(result);
                    </code>
                </example>
            </member>
            """);

        // Act
        MemberInfo? member = parser.ParseMember(memberElement);

        // Assert
        member.ShouldNotBeNull();
        member.CodeExamples.Length.ShouldBe(1);
        member.CodeExamples[0].Code.Trim().ShouldBe("var result = Calculate(42);\nConsole.WriteLine(result);");
        member.CodeExamples[0].Description.ShouldBe("");
        member.CodeExamples[0].Language.ShouldBe("csharp");
    }

    [TestMethod]
    public void ParseMember_WithMultipleExamples_ShouldExtractAll()
    {
        // Arrange
        XElement memberElement = XElement.Parse("""
            <member name="M:Test.Process">
                <summary>Processes data.</summary>
                <example>
                    Basic usage:
                    <code>Process();</code>
                </example>
                <example>
                    Advanced usage:
                    <code>
                    var options = new Options();
                    Process(options);
                    </code>
                </example>
            </member>
            """);

        // Act
        MemberInfo? member = parser.ParseMember(memberElement);

        // Assert
        member.ShouldNotBeNull();
        member.CodeExamples.Length.ShouldBe(2);
        member.CodeExamples[0].Description.ShouldContain("Basic usage:");
        member.CodeExamples[0].Code.Trim().ShouldBe("Process();");
        member.CodeExamples[1].Description.ShouldContain("Advanced usage:");
        member.CodeExamples[1].Code.Trim().ShouldContain("Process(options);");
    }

    [TestMethod]
    public void ParseMember_WithExceptions_ShouldExtractExceptionInfo()
    {
        // Arrange
        XElement memberElement = XElement.Parse("""
            <member name="M:Test.Validate(System.String)">
                <summary>Validates input.</summary>
                <param name="input">The input to validate.</param>
                <exception cref="T:System.ArgumentNullException">Thrown when input is null.</exception>
                <exception cref="T:System.ArgumentException">Thrown when input is empty or whitespace.</exception>
            </member>
            """);

        // Act
        MemberInfo? member = parser.ParseMember(memberElement);

        // Assert
        member.ShouldNotBeNull();
        member.Exceptions.Length.ShouldBe(2);
        member.Exceptions[0].Type.ShouldBe("System.ArgumentNullException");
        member.Exceptions[0].Condition.ShouldBe("Thrown when input is null.");
        member.Exceptions[1].Type.ShouldBe("System.ArgumentException");
        member.Exceptions[1].Condition.ShouldBe("Thrown when input is empty or whitespace.");
    }

    [TestMethod]
    public void ParseMember_WithParameters_ShouldExtractParameterInfo()
    {
        // Arrange
        XElement memberElement = XElement.Parse("""
            <member name="M:Test.Add(System.Int32,System.Int32)">
                <summary>Adds two numbers.</summary>
                <param name="a">The first number.</param>
                <param name="b">The second number.</param>
                <returns>The sum of the two numbers.</returns>
            </member>
            """);

        // Act
        MemberInfo? member = parser.ParseMember(memberElement);

        // Assert
        member.ShouldNotBeNull();
        member.Parameters.Length.ShouldBe(2);
        member.Parameters[0].Name.ShouldBe("a");
        member.Parameters[0].Type.ShouldBe("System.Int32");
        member.Parameters[0].Description.ShouldBe("The first number.");
        member.Parameters[1].Name.ShouldBe("b");
        member.Parameters[1].Type.ShouldBe("System.Int32");
        member.Parameters[1].Description.ShouldBe("The second number.");
    }

    [TestMethod]
    public void ParseMember_WithReturns_ShouldExtractReturnsInfo()
    {
        // Arrange
        XElement memberElement = XElement.Parse("""
            <member name="M:Test.Calculate">
                <summary>Performs calculation.</summary>
                <returns>The calculated result as a decimal value.</returns>
            </member>
            """);

        // Act
        MemberInfo? member = parser.ParseMember(memberElement);

        // Assert
        member.ShouldNotBeNull();
        member.Returns.ShouldBe("The calculated result as a decimal value.");
    }

    [TestMethod]
    public void ParseMember_WithSeeAlso_ShouldExtractReference()
    {
        // Arrange
        XElement memberElement = XElement.Parse("""
            <member name="M:Test.Method">
                <summary>A method.</summary>
                <seealso cref="M:Test.RelatedMethod"/>
                <seealso cref="T:Test.RelatedType"/>
            </member>
            """);

        // Act
        MemberInfo? member = parser.ParseMember(memberElement);

        // Assert
        member.ShouldNotBeNull();
        member.SeeAlso.ShouldNotBeNull();
        member.SeeAlso.ShouldContain("Test.RelatedMethod");
        member.SeeAlso.ShouldContain("Test.RelatedType");
    }

    [TestMethod]
    public void ParseMember_ShouldCalculateComplexityMetrics()
    {
        // Arrange
        XElement memberElement = XElement.Parse("""
            <member name="M:Test.Complex(System.String,System.Int32,System.Boolean)">
                <summary>
                A complex method with multiple parameters.
                This method does many things.
                </summary>
                <param name="text">Text parameter.</param>
                <param name="count">Count parameter.</param>
                <param name="flag">Flag parameter.</param>
                <remarks>
                Additional remarks about the method.
                </remarks>
            </member>
            """);

        // Act
        MemberInfo? member = parser.ParseMember(memberElement);

        // Assert
        member.ShouldNotBeNull();
        member.Complexity.ShouldNotBeNull();
        member.Complexity.ParameterCount.ShouldBe(3);
        member.Complexity.DocumentationLineCount.ShouldBeGreaterThan(0);
    }
}