using ApiLens.Core.Lucene;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Util;

namespace ApiLens.Core.Tests.Lucene;

[TestClass]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed in TestCleanup")]
public class GenericTypeAnalyzerTests
{
    private LuceneVersion version;
    private DotNetCodeAnalyzer analyzer = null!;

    [TestInitialize]
    public void Setup()
    {
        version = LuceneVersion.LUCENE_48;
        analyzer = new DotNetCodeAnalyzer(version);
    }

    [TestCleanup]
    public void Cleanup()
    {
        analyzer?.Dispose();
    }

    [TestMethod]
    public void Analyze_GenericTypeWithOneParameter_CreatesMultipleTokens()
    {
        // Arrange
        string input = "List<T>";

        // Act
        List<string> tokens = GetTokens(input);

        // Assert
        tokens.ShouldContain("list<t>");
        tokens.ShouldContain("list");
    }

    [TestMethod]
    public void Analyze_GenericTypeWithMultipleParameters_CreatesMultipleTokens()
    {
        // Arrange
        string input = "Dictionary<TKey,TValue>";

        // Act
        List<string> tokens = GetTokens(input);

        // Assert
        tokens.ShouldContain("dictionary<tkey,tvalue>");
        tokens.ShouldContain("dictionary");
    }

    [TestMethod]
    public void Analyze_NestedGenericType_CreatesAppropriateTokens()
    {
        // Arrange
        string input = "List<Dictionary<string,int>>";

        // Act
        List<string> tokens = GetTokens(input);

        // Assert
        tokens.ShouldContain("list<dictionary<string,int>>");
        tokens.ShouldContain("list");
    }

    [TestMethod]
    public void Analyze_GenericTypeInNamespace_HandlesCorrectly()
    {
        // Arrange
        string input = "System.Collections.Generic.List<T>";

        // Act
        List<string> tokens = GetTokens(input);

        // Assert
        tokens.ShouldContain("system.collections.generic.list<t>");
        tokens.ShouldContain("list<t>");
        tokens.ShouldContain("list");
        tokens.ShouldContain("system");
        tokens.ShouldContain("collections");
        tokens.ShouldContain("generic");
    }

    [TestMethod]
    public void Analyze_ContentWithGenericTypes_TokenizesCorrectly()
    {
        // Arrange
        string input = "Provides extension methods for List<T> operations";

        // Act
        List<string> tokens = GetTokens(input);

        // Assert
        // WhitespaceTokenizer processes each word separately
        tokens.ShouldContain("provides");
        tokens.ShouldContain("extension");
        tokens.ShouldContain("methods");
        tokens.ShouldContain("for");
        tokens.ShouldContain("list<t>");
        tokens.ShouldContain("list");
        tokens.ShouldContain("operations");
    }

    [TestMethod]
    public void Analyze_GenericTypeWithConcreteType_CreatesTokens()
    {
        // Arrange
        string input = "List<Person>";

        // Act
        List<string> tokens = GetTokens(input);

        // Assert
        tokens.ShouldContain("list<person>");
        tokens.ShouldContain("list");
    }

    [TestMethod]
    public void Analyze_TypeWithSingleBacktick_CreatesMultipleTokens()
    {
        // Arrange
        string input = "List`1";

        // Act
        List<string> tokens = GetTokens(input);

        // Assert
        tokens.ShouldContain("list`1");
        tokens.ShouldContain("list");
    }

    [TestMethod]
    public void Analyze_TypeWithDoubleBacktick_CreatesMultipleTokens()
    {
        // Arrange
        string input = "ToIReadOnlyListUnsafe``1";

        // Act
        List<string> tokens = GetTokens(input);

        // Assert
        tokens.ShouldContain("toireadonlylistunsafe``1");
        tokens.ShouldContain("toireadonlylistunsafe");
    }

    [TestMethod]
    public void Analyze_TypeWithMultipleGenericParameters_BacktickNotation()
    {
        // Arrange
        string input = "Dictionary`2";

        // Act
        List<string> tokens = GetTokens(input);

        // Assert
        tokens.ShouldContain("dictionary`2");
        tokens.ShouldContain("dictionary");
    }

    [TestMethod]
    public void Analyze_FullyQualifiedGenericTypeWithBacktick_HandlesCorrectly()
    {
        // Arrange
        string input = "System.Collections.Generic.List`1";

        // Act
        List<string> tokens = GetTokens(input);

        // Assert
        tokens.ShouldContain("system.collections.generic.list`1");
        tokens.ShouldContain("list`1");
        tokens.ShouldContain("list");
        tokens.ShouldContain("system");
        tokens.ShouldContain("collections");
        tokens.ShouldContain("generic");
    }

    private List<string> GetTokens(string input)
    {
        List<string> tokens = [];

        using TokenStream tokenStream = analyzer.GetTokenStream("test", new StringReader(input));
        ICharTermAttribute termAttr = tokenStream.AddAttribute<ICharTermAttribute>();

        tokenStream.Reset();
        while (tokenStream.IncrementToken())
        {
            tokens.Add(termAttr.ToString());
        }
        tokenStream.End();

        return tokens;
    }
}