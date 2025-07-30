using System.Xml.Linq;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Querying;
using Lucene.Net.Documents;
using Lucene.Net.Store;

namespace ApiLens.Core.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public class RichMetadataIntegrationTests : IDisposable
{
    private readonly RAMDirectory directory = new();
    private readonly ILuceneIndexManager indexManager;
    private readonly IDocumentBuilder documentBuilder = new DocumentBuilder();
    private readonly IXmlDocumentParser parser = new XmlDocumentParser();
    private readonly IQueryEngine queryEngine;

    public RichMetadataIntegrationTests()
    {
        indexManager = new LuceneIndexManager(directory);
        queryEngine = new QueryEngine(indexManager);
    }

    [TestMethod]
    public void Should_IndexAndSearch_CodeExamples()
    {
        // Arrange
        XDocument doc = XDocument.Parse("""
            <?xml version="1.0"?>
            <doc>
                <assembly><name>TestAssembly</name></assembly>
                <members>
                    <member name="M:TestNamespace.Calculator.Add(System.Int32,System.Int32)">
                        <summary>Adds two numbers together.</summary>
                        <example>
                            Basic addition:
                            <code>
                            var result = Calculator.Add(5, 3);
                            Console.WriteLine(result); // Output: 8
                            </code>
                        </example>
                        <param name="a">First number.</param>
                        <param name="b">Second number.</param>
                        <returns>The sum of a and b.</returns>
                    </member>
                </members>
            </doc>
            """);

        ImmutableArray<MemberInfo> members = parser.ParseMembers(doc, "TestAssembly");

        // Act
        foreach (MemberInfo member in members)
        {
            Document luceneDoc = documentBuilder.BuildDocument(member);
            indexManager.AddDocument(luceneDoc);
        }
        indexManager.Commit();

        // Assert - Search by code example content
        List<MemberInfo> results = queryEngine.SearchByContent("Calculator.Add", 10);
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("Add");
        results[0].CodeExamples.Length.ShouldBe(1);
        results[0].CodeExamples[0].Code.ShouldContain("Calculator.Add(5, 3)");
        results[0].CodeExamples[0].Description.ShouldContain("Basic addition:");
    }

    [TestMethod]
    public void Should_IndexAndSearch_Exceptions()
    {
        // Arrange
        XDocument doc = XDocument.Parse("""
            <?xml version="1.0"?>
            <doc>
                <assembly><name>TestAssembly</name></assembly>
                <members>
                    <member name="M:TestNamespace.Validator.ValidateInput(System.String)">
                        <summary>Validates user input.</summary>
                        <param name="input">The input to validate.</param>
                        <exception cref="T:System.ArgumentNullException">Thrown when input is null.</exception>
                        <exception cref="T:System.ArgumentException">Thrown when input is empty or contains only whitespace.</exception>
                    </member>
                </members>
            </doc>
            """);

        ImmutableArray<MemberInfo> members = parser.ParseMembers(doc, "TestAssembly");

        // Act
        foreach (MemberInfo member in members)
        {
            Document luceneDoc = documentBuilder.BuildDocument(member);
            indexManager.AddDocument(luceneDoc);
        }
        indexManager.Commit();

        // Assert - Search by exception type
        List<MemberInfo> results = queryEngine.SearchByContent("ArgumentNullException", 10);
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("ValidateInput");
        results[0].Exceptions.Length.ShouldBe(2);
        results[0].Exceptions[0].Type.ShouldBe("System.ArgumentNullException");
        results[0].Exceptions[0].Condition.ShouldBe("Thrown when input is null.");
    }

    [TestMethod]
    public void Should_IndexAndSearch_Parameters()
    {
        // Arrange
        XDocument doc = XDocument.Parse("""
            <?xml version="1.0"?>
            <doc>
                <assembly><name>TestAssembly</name></assembly>
                <members>
                    <member name="M:TestNamespace.StringUtils.Format(System.String,System.Object[])">
                        <summary>Formats a string with arguments.</summary>
                        <param name="format">The format string.</param>
                        <param name="args">The arguments to insert into the format string.</param>
                        <returns>The formatted string.</returns>
                    </member>
                </members>
            </doc>
            """);

        ImmutableArray<MemberInfo> members = parser.ParseMembers(doc, "TestAssembly");

        // Act
        foreach (MemberInfo member in members)
        {
            Document luceneDoc = documentBuilder.BuildDocument(member);
            indexManager.AddDocument(luceneDoc);
        }
        indexManager.Commit();

        // Assert - Search by parameter description
        List<MemberInfo> results = queryEngine.SearchByContent("format string", 10);
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("Format");
        results[0].Parameters.Length.ShouldBe(2);
        results[0].Parameters[0].Name.ShouldBe("format");
        results[0].Parameters[0].Description.ShouldBe("The format string.");
    }

    [TestMethod]
    public void Should_IndexAndSearch_ReturnsDocumentation()
    {
        // Arrange
        XDocument doc = XDocument.Parse("""
            <?xml version="1.0"?>
            <doc>
                <assembly><name>TestAssembly</name></assembly>
                <members>
                    <member name="M:TestNamespace.Calculator.CalculateAverage(System.Double[])">
                        <summary>Calculates the average of numbers.</summary>
                        <param name="numbers">Array of numbers.</param>
                        <returns>The arithmetic mean of the numbers, or NaN if the array is empty.</returns>
                    </member>
                </members>
            </doc>
            """);

        ImmutableArray<MemberInfo> members = parser.ParseMembers(doc, "TestAssembly");

        // Act
        foreach (MemberInfo member in members)
        {
            Document luceneDoc = documentBuilder.BuildDocument(member);
            indexManager.AddDocument(luceneDoc);
        }
        indexManager.Commit();

        // Assert - Search by returns documentation
        List<MemberInfo> results = queryEngine.SearchByContent("arithmetic mean", 10);
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("CalculateAverage");
        results[0].Returns.ShouldBe("The arithmetic mean of the numbers, or NaN if the array is empty.");
    }

    [TestMethod]
    public void Should_CalculateAndIndex_ComplexityMetrics()
    {
        // Arrange
        XDocument doc = XDocument.Parse("""
            <?xml version="1.0"?>
            <doc>
                <assembly><name>TestAssembly</name></assembly>
                <members>
                    <member name="M:TestNamespace.ComplexMethod(System.String,System.Int32,System.Boolean)">
                        <summary>
                        A complex method that processes data.
                        It handles various conditions and loops through data.
                        </summary>
                        <param name="input">The input string.</param>
                        <param name="count">The count value.</param>
                        <param name="flag">The flag to control behavior.</param>
                        <remarks>
                        This method uses if statements to check conditions.
                        It also has a while loop for processing.
                        Error handling with try-catch is included.
                        </remarks>
                    </member>
                </members>
            </doc>
            """);

        ImmutableArray<MemberInfo> members = parser.ParseMembers(doc, "TestAssembly");

        // Act
        foreach (MemberInfo member in members)
        {
            Document luceneDoc = documentBuilder.BuildDocument(member);
            indexManager.AddDocument(luceneDoc);
        }
        indexManager.Commit();

        // Assert
        List<MemberInfo> results = queryEngine.SearchByName("ComplexMethod", 10);
        results.Count.ShouldBe(1);

        ComplexityMetrics? complexity = results[0].Complexity;
        complexity.ShouldNotBeNull();
        complexity!.ParameterCount.ShouldBe(3);
        complexity.CyclomaticComplexity.ShouldBeGreaterThan(1); // Has if, while, catch
        complexity.DocumentationLineCount.ShouldBeGreaterThan(0);
    }

    public void Dispose()
    {
        queryEngine.Dispose();
        indexManager.Dispose();
        directory.Dispose();
    }
}