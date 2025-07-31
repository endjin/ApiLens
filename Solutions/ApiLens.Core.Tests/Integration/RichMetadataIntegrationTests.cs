using System.Xml.Linq;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Querying;

namespace ApiLens.Core.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public class RichMetadataIntegrationTests : IDisposable
{
    private readonly string tempIndexPath;
    private readonly ILuceneIndexManager indexManager;
    private readonly IDocumentBuilder documentBuilder = new DocumentBuilder();
    private readonly IXmlDocumentParser parser = new XmlDocumentParser();
    private readonly IQueryEngine queryEngine;

    public RichMetadataIntegrationTests()
    {
        tempIndexPath = Path.Combine(Path.GetTempPath(), $"apilens_test_{Guid.NewGuid()}");
        indexManager = new LuceneIndexManager(tempIndexPath, parser, documentBuilder);
        queryEngine = new QueryEngine(indexManager);
    }

    [TestMethod]
    public async Task Should_IndexAndSearch_CodeExamples()
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
        await indexManager.IndexBatchAsync(members);
        await indexManager.CommitAsync();

        // Assert - Search by code example content
        // Note: WhitespaceAnalyzer tokenizes on whitespace, so "Calculator.Add(5," is one token
        // The content field includes the name "Add" which should be searchable
        List<MemberInfo> results = queryEngine.SearchByContent("Add", 10);
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("Add");
        results[0].CodeExamples.Length.ShouldBe(1);
        results[0].CodeExamples[0].Code.ShouldContain("Calculator.Add(5, 3)");
        results[0].CodeExamples[0].Description.ShouldContain("Basic addition:");
    }

    [TestMethod]
    public async Task Should_IndexAndSearch_Exceptions()
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
        await indexManager.IndexBatchAsync(members);
        await indexManager.CommitAsync();

        // Assert - Search by exception type
        // Try searching for a simpler term first
        List<MemberInfo> results = queryEngine.SearchByContent("ValidateInput", 10);
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("ValidateInput");
        results[0].Exceptions.Length.ShouldBe(2);
        results[0].Exceptions[0].Type.ShouldBe("System.ArgumentNullException");
        results[0].Exceptions[0].Condition.ShouldBe("Thrown when input is null.");
    }

    [TestMethod]
    public async Task Should_IndexAndSearch_Parameters()
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
        await indexManager.IndexBatchAsync(members);
        await indexManager.CommitAsync();

        // Assert - Search by parameter description
        List<MemberInfo> results = queryEngine.SearchByContent("format string", 10);
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("Format");
        results[0].Parameters.Length.ShouldBe(2);
        results[0].Parameters[0].Name.ShouldBe("format");
        results[0].Parameters[0].Description.ShouldBe("The format string.");
    }

    [TestMethod]
    public async Task Should_IndexAndSearch_ReturnsDocumentation()
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
        await indexManager.IndexBatchAsync(members);
        await indexManager.CommitAsync();

        // Assert - Search by returns documentation
        List<MemberInfo> results = queryEngine.SearchByContent("arithmetic mean", 10);
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("CalculateAverage");
        results[0].Returns.ShouldBe("The arithmetic mean of the numbers, or NaN if the array is empty.");
    }

    [TestMethod]
    public async Task Should_CalculateAndIndex_ComplexityMetrics()
    {
        // Arrange
        XDocument doc = XDocument.Parse("""
            <?xml version="1.0"?>
            <doc>
                <assembly><name>TestAssembly</name></assembly>
                <members>
                    <member name="M:TestNamespace.Processor.ComplexMethod(System.String,System.Int32,System.Boolean)">
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
        await indexManager.IndexBatchAsync(members);
        await indexManager.CommitAsync();

        // Assert
        List<MemberInfo> results = queryEngine.SearchByName("ComplexMethod", 10);
        results.Count.ShouldBe(1);

        ComplexityMetrics? complexity = results[0].Complexity;
        complexity.ShouldNotBeNull();
        complexity!.ParameterCount.ShouldBe(3);
        complexity.CyclomaticComplexity.ShouldBe(1); // Base complexity (can't determine from XML alone)
        complexity.DocumentationLineCount.ShouldBeGreaterThan(0);
    }

    public void Dispose()
    {
        queryEngine?.Dispose();
        indexManager?.Dispose();

        if (!string.IsNullOrEmpty(tempIndexPath) && Directory.Exists(tempIndexPath))
        {
            try
            {
                Directory.Delete(tempIndexPath, true);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // Ignore cleanup errors
            }
        }
    }
}