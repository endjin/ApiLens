using System.Xml.Linq;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;

namespace ApiLens.Core.Tests.Parsing;

[TestClass]
public class CrossReferenceExtractorTests
{

    [TestMethod]
    public void ExtractReferences_WithSeeElement_ExtractsCrossReference()
    {
        // Arrange
        const string xml = """
            <member name="M:System.String.Split">
                <summary>Splits a string. See <see cref="T:System.Char"/> for character information.</summary>
            </member>
            """;
        XElement element = XElement.Parse(xml);
        const string memberId = "M:System.String.Split";

        // Act
        ImmutableArray<CrossReference> references = CrossReferenceExtractor.ExtractReferences(element, memberId);

        // Assert
        references.Length.ShouldBe(1);
        references[0].SourceId.ShouldBe(memberId);
        references[0].TargetId.ShouldBe("T:System.Char");
        references[0].Type.ShouldBe(ReferenceType.See);
        references[0].Context.ShouldBe("summary");
    }

    [TestMethod]
    public void ExtractReferences_WithSeeAlsoElement_ExtractsCrossReference()
    {
        // Arrange
        const string xml = """
            <member name="T:System.String">
                <summary>Represents text.</summary>
                <seealso cref="T:System.Text.StringBuilder"/>
            </member>
            """;
        XElement element = XElement.Parse(xml);
        const string memberId = "T:System.String";

        // Act
        ImmutableArray<CrossReference> references = CrossReferenceExtractor.ExtractReferences(element, memberId);

        // Assert
        references.Length.ShouldBe(1);
        references[0].SourceId.ShouldBe(memberId);
        references[0].TargetId.ShouldBe("T:System.Text.StringBuilder");
        references[0].Type.ShouldBe(ReferenceType.SeeAlso);
        references[0].Context.ShouldBe("seealso");
    }

    [TestMethod]
    public void ExtractReferences_WithExceptionElement_ExtractsCrossReference()
    {
        // Arrange
        const string xml = """
            <member name="M:System.String.Substring(System.Int32)">
                <summary>Retrieves a substring.</summary>
                <exception cref="T:System.ArgumentOutOfRangeException">
                    startIndex is less than zero or greater than the length of this instance.
                </exception>
            </member>
            """;
        XElement element = XElement.Parse(xml);
        const string memberId = "M:System.String.Substring(System.Int32)";

        // Act
        ImmutableArray<CrossReference> references = CrossReferenceExtractor.ExtractReferences(element, memberId);

        // Assert
        references.Length.ShouldBe(1);
        references[0].SourceId.ShouldBe(memberId);
        references[0].TargetId.ShouldBe("T:System.ArgumentOutOfRangeException");
        references[0].Type.ShouldBe(ReferenceType.Exception);
        references[0].Context.ShouldBe("exception");
    }

    [TestMethod]
    public void ExtractReferences_WithMultipleReferences_ExtractsAll()
    {
        // Arrange
        const string xml = """
            <member name="M:System.Collections.Generic.List`1.Add(`0)">
                <summary>Adds an object. See <see cref="T:System.Collections.IList"/>.</summary>
                <param name="item">The object to add. See <see cref="T:System.Object"/>.</param>
                <exception cref="T:System.NotSupportedException">The list is read-only.</exception>
                <seealso cref="M:System.Collections.Generic.List`1.Remove(`0)"/>
            </member>
            """;
        XElement element = XElement.Parse(xml);
        const string memberId = "M:System.Collections.Generic.List`1.Add(`0)";

        // Act
        ImmutableArray<CrossReference> references = CrossReferenceExtractor.ExtractReferences(element, memberId);

        // Assert
        references.Length.ShouldBe(4);
        references.ShouldContain(r => r.TargetId == "T:System.Collections.IList" && r.Type == ReferenceType.See);
        references.ShouldContain(r => r.TargetId == "T:System.Object" && r.Type == ReferenceType.See);
        references.ShouldContain(r => r.TargetId == "T:System.NotSupportedException" && r.Type == ReferenceType.Exception);
        references.ShouldContain(r => r.TargetId == "M:System.Collections.Generic.List`1.Remove(`0)" && r.Type == ReferenceType.SeeAlso);
    }

    [TestMethod]
    public void ExtractReferences_WithNoReferences_ReturnsEmpty()
    {
        // Arrange
        const string xml = """
            <member name="T:System.Int32">
                <summary>Represents a 32-bit signed integer.</summary>
            </member>
            """;
        XElement element = XElement.Parse(xml);
        const string memberId = "T:System.Int32";

        // Act
        ImmutableArray<CrossReference> references = CrossReferenceExtractor.ExtractReferences(element, memberId);

        // Assert
        references.ShouldBeEmpty();
    }
}