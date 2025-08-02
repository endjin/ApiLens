using ApiLens.Core.Lucene;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;

namespace ApiLens.Core.Tests.Lucene;

[TestClass]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed in TestCleanup")]
public class DotNetCodeAnalyzerTests
{
    private DotNetCodeAnalyzer analyzer = null!;

    [TestInitialize]
    public void Setup()
    {
        analyzer = new DotNetCodeAnalyzer(global::Lucene.Net.Util.LuceneVersion.LUCENE_48);
    }

    [TestCleanup]
    public void Cleanup()
    {
        analyzer?.Dispose();
    }

    [TestMethod]
    public void Analyze_WithClassName_TokenizesCorrectly()
    {
        // Arrange
        const string text = "System.Collections.Generic.List";

        // Act
        List<string> tokens = GetTokens(text);

        // Assert
        tokens.ShouldContain("system");
        tokens.ShouldContain("collections");
        tokens.ShouldContain("generic");
        tokens.ShouldContain("list");
        tokens.ShouldContain("system.collections");
        tokens.ShouldContain("collections.generic");
        tokens.ShouldContain("generic.list");
        tokens.ShouldContain("system.collections.generic.list");
    }

    [TestMethod]
    public void Analyze_WithGenericType_HandlesBacktick()
    {
        // Arrange
        const string text = "List`1";

        // Act
        List<string> tokens = GetTokens(text);

        // Assert
        // For now, just check that we get the lowercased token
        // We can enhance the analyzer later to split on backticks
        tokens.ShouldContain("list`1");
    }

    [TestMethod]
    public void Analyze_WithMethodName_TokenizesCorrectly()
    {
        // Arrange
        const string text = "System.String.Split";

        // Act
        List<string> tokens = GetTokens(text);

        // Assert
        tokens.ShouldContain("system");
        tokens.ShouldContain("string");
        tokens.ShouldContain("split");
        tokens.ShouldContain("system.string");
        tokens.ShouldContain("string.split");
        tokens.ShouldContain("system.string.split");
    }

    [TestMethod]
    public void Analyze_WithShortName_ReturnsLowercaseToken()
    {
        // Arrange
        const string text = "Int32";

        // Act
        List<string> tokens = GetTokens(text);

        // Assert
        tokens.ShouldContain("int32");
    }

    [TestMethod]
    public void Analyze_WithCamelCase_PreservesAsOneToken()
    {
        // Arrange
        const string text = "GetHashCode";

        // Act
        List<string> tokens = GetTokens(text);

        // Assert
        tokens.ShouldContain("gethashcode");
    }

    [TestMethod]
    public void Analyze_WithNestedType_HandlesPlus()
    {
        // Arrange
        const string text = "Dictionary`2+Enumerator";

        // Act
        List<string> tokens = GetTokens(text);

        // Assert
        tokens.ShouldContain("dictionary`2+enumerator");
        // Additional tokens based on implementation
    }

    private List<string> GetTokens(string text)
    {
        List<string> tokens = [];
        using TokenStream? tokenStream = analyzer.GetTokenStream("field", text);
        ICharTermAttribute? termAttribute = tokenStream.GetAttribute<ICharTermAttribute>();
        tokenStream.Reset();

        while (tokenStream.IncrementToken())
        {
            tokens.Add(termAttribute.ToString());
        }

        tokenStream.End();
        return tokens;
    }
}