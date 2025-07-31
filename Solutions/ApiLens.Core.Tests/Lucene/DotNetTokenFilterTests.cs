using System.Text;
using ApiLens.Core.Lucene;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;

namespace ApiLens.Core.Tests.Lucene;

[TestClass]
public class DotNetTokenFilterTests
{
    [TestMethod]
    public void IncrementToken_SimpleType_GeneratesExpectedTokens()
    {
        // Arrange
        string input = "System.String";
        // Based on GenerateTokens: full name first, then individual parts
        List<string> expectedTokens = ["System.String", "System", "String"];
        
        TokenStream tokenStream = CreateTokenStream(input);
        DotNetTokenFilter filter = new(tokenStream);
        
        // Act
        List<string> actualTokens = GetAllTokens(filter);
        
        // Assert
        actualTokens.ShouldBe(expectedTokens);
    }
    
    [TestMethod]
    public void IncrementToken_NestedNamespace_GeneratesHierarchicalTokens()
    {
        // Arrange
        string input = "System.Collections.Generic.List";
        // Based on GenerateTokens logic:
        // 1. Full name first
        // 2. Individual parts
        // 3. Hierarchical combinations (length > 1, not full name)
        List<string> expectedTokens = [
            "System.Collections.Generic.List",  // Full name
            "System",                          // Individual parts
            "Collections",
            "Generic", 
            "List",
            "System.Collections",               // Hierarchical combinations
            "System.Collections.Generic",
            "Collections.Generic",
            "Collections.Generic.List",
            "Generic.List"
        ];
        
        TokenStream tokenStream = CreateTokenStream(input);
        DotNetTokenFilter filter = new(tokenStream);
        
        // Act
        List<string> actualTokens = GetAllTokens(filter);
        
        // Assert
        actualTokens.ShouldBe(expectedTokens);
    }
    
    [TestMethod]
    public void IncrementToken_GenericTypeWithBacktick_GeneratesCorrectTokens()
    {
        // Arrange
        string input = "System.Collections.Generic.Dictionary`2";
        
        TokenStream tokenStream = CreateTokenStream(input);
        DotNetTokenFilter filter = new(tokenStream);
        
        // Act
        List<string> actualTokens = GetAllTokens(filter);
        
        // Assert
        // Based on GenerateBacktickGenericTokens:
        actualTokens.ShouldContain("System.Collections.Generic.Dictionary`2"); // Full name with backtick
        actualTokens.ShouldContain("Dictionary`2");  // Last part with backtick suffix
        actualTokens.ShouldContain("Dictionary");    // Base type without backtick
        actualTokens.ShouldContain("System.Collections.Generic"); // Namespace combination
    }
    
    [TestMethod]
    public void IncrementToken_GenericTypeWithAngleBrackets_GeneratesCorrectTokens()
    {
        // Arrange
        string input = "List<String>";
        
        TokenStream tokenStream = CreateTokenStream(input);
        DotNetTokenFilter filter = new(tokenStream);
        
        // Act
        List<string> actualTokens = GetAllTokens(filter);
        
        // Assert
        // Based on GenerateGenericTokens for simple generic (no namespace dots):
        actualTokens.ShouldContain("List<String>"); // Full generic type
        actualTokens.ShouldContain("List");         // Base type without generics
        // Note: The implementation doesn't extract type parameters from angle brackets
        actualTokens.Count.ShouldBe(2);
    }
    
    [TestMethod]
    public void IncrementToken_SinglePartName_ReturnsSingleToken()
    {
        // Arrange
        string input = "String";
        List<string> expectedTokens = ["String"];
        
        TokenStream tokenStream = CreateTokenStream(input);
        DotNetTokenFilter filter = new(tokenStream);
        
        // Act
        List<string> actualTokens = GetAllTokens(filter);
        
        // Assert
        actualTokens.ShouldBe(expectedTokens);
    }
    
    [TestMethod]
    public void IncrementToken_EmptyInput_ReturnsEmptyToken()
    {
        // Arrange
        TokenStream tokenStream = CreateTokenStream("");
        DotNetTokenFilter filter = new(tokenStream);
        
        // Act
        List<string> actualTokens = GetAllTokens(filter);
        
        // Assert
        // The DotNetTokenFilter processes empty string as a single empty token
        actualTokens.Count.ShouldBe(1);
        actualTokens[0].ShouldBe("");
    }
    
    [TestMethod]
    public void IncrementToken_MultipleInputTokens_ProcessesAll()
    {
        // Arrange
        string[] inputs = ["System.String", "System.Int32"];
        
        TokenStream tokenStream = CreateTokenStream(inputs);
        DotNetTokenFilter filter = new(tokenStream);
        
        // Act
        List<string> actualTokens = GetAllTokens(filter);
        
        // Assert
        actualTokens.ShouldContain("System.String");
        actualTokens.ShouldContain("System.Int32");
        actualTokens.ShouldContain("String");
        actualTokens.ShouldContain("Int32");
        actualTokens.Count(t => t == "System").ShouldBe(2); // System appears twice
    }
    
    [TestMethod]
    public void IncrementToken_NestedGenericType_HandlesComplexGenericStructure()
    {
        // Arrange
        string input = "Dictionary<string,List<int>>";
        
        TokenStream tokenStream = CreateTokenStream(input);
        DotNetTokenFilter filter = new(tokenStream);
        
        // Act
        List<string> actualTokens = GetAllTokens(filter);
        
        // Assert
        // Based on GenerateGenericTokens, for simple generic types (no namespace dots)
        // it only generates the full type and the base type without generics
        actualTokens.ShouldContain("Dictionary<string,List<int>>"); // Full generic type
        actualTokens.ShouldContain("Dictionary"); // Base type
        // The implementation doesn't parse the generic parameters within angle brackets
        actualTokens.Count.ShouldBe(2);
    }
    
    [TestMethod]
    public void PositionIncrement_FirstToken_HasIncrementOne()
    {
        // Arrange
        string input = "System.String";
        TokenStream tokenStream = CreateTokenStream(input);
        DotNetTokenFilter filter = new(tokenStream);
        IPositionIncrementAttribute posAttr = filter.GetAttribute<IPositionIncrementAttribute>();
        
        // Act
        filter.IncrementToken();
        
        // Assert
        posAttr.PositionIncrement.ShouldBe(1);
    }
    
    [TestMethod]
    public void PositionIncrement_SubsequentTokens_HaveIncrementZero()
    {
        // Arrange
        string input = "System.String";
        TokenStream tokenStream = CreateTokenStream(input);
        DotNetTokenFilter filter = new(tokenStream);
        IPositionIncrementAttribute posAttr = filter.GetAttribute<IPositionIncrementAttribute>();
        
        // Act
        filter.IncrementToken(); // First token
        filter.IncrementToken(); // Second token
        
        // Assert
        posAttr.PositionIncrement.ShouldBe(0);
    }
    
    [TestMethod]
    public void Reset_AfterProcessing_CanProcessAgain()
    {
        // Arrange
        string input = "System.String";
        TokenStream tokenStream = CreateTokenStream(input);
        DotNetTokenFilter filter = new(tokenStream);
        
        // Act - First pass
        List<string> firstPass = GetAllTokens(filter);
        
        // Reset
        filter.Reset();
        tokenStream.Reset();
        
        // Act - Second pass
        List<string> secondPass = GetAllTokens(filter);
        
        // Assert
        firstPass.ShouldBe(secondPass);
    }
    
    [TestMethod]
    public void IncrementToken_TypeWithUnderscores_PreservesUnderscores()
    {
        // Arrange
        string input = "System.Runtime.Intrinsics.X86.Aes_Gcm";
        
        TokenStream tokenStream = CreateTokenStream(input);
        DotNetTokenFilter filter = new(tokenStream);
        
        // Act
        List<string> actualTokens = GetAllTokens(filter);
        
        // Assert
        actualTokens.ShouldContain("Aes_Gcm");
        actualTokens.ShouldContain("X86");
    }
    
    [TestMethod]
    public void IncrementToken_ArrayType_HandlesArrayNotation()
    {
        // Arrange
        string input = "System.String[]";
        
        TokenStream tokenStream = CreateTokenStream(input);
        DotNetTokenFilter filter = new(tokenStream);
        
        // Act
        List<string> actualTokens = GetAllTokens(filter);
        
        // Assert
        actualTokens.ShouldContain("System.String[]");
        actualTokens.ShouldContain("String[]");
    }
    
    [TestMethod]
    public void Dispose_CalledOnFilter_DisposesUnderlyingStream()
    {
        // Arrange
        MockTokenStream mockStream = new();
        DotNetTokenFilter filter = new(mockStream);
        
        // Act
        filter.Dispose();
        
        // Assert
        mockStream.DisposedCount.ShouldBe(1);
    }
    
    // Helper methods
    
    private static TokenStream CreateTokenStream(string input)
    {
        return CreateTokenStream([input]);
    }
    
    private static TokenStream CreateTokenStream(string[] inputs)
    {
        return new ListTokenStream(inputs);
    }
    
    private static List<string> GetAllTokens(TokenFilter filter)
    {
        List<string> tokens = [];
        ICharTermAttribute termAttr = filter.GetAttribute<ICharTermAttribute>();
        
        filter.Reset();
        while (filter.IncrementToken())
        {
            tokens.Add(termAttr.ToString());
        }
        filter.End();
        
        return tokens;
    }
    
    // Mock classes for testing
    
    private sealed class ListTokenStream : TokenStream
    {
        private readonly string[] tokens;
        private int position = -1;
        private readonly ICharTermAttribute termAttr;
        
        public ListTokenStream(string[] tokens)
        {
            this.tokens = tokens;
            termAttr = AddAttribute<ICharTermAttribute>();
        }
        
        public sealed override bool IncrementToken()
        {
            ClearAttributes();
            position++;
            
            if (position < tokens.Length)
            {
                termAttr.SetEmpty().Append(tokens[position]);
                return true;
            }
            
            return false;
        }
        
        public override void Reset()
        {
            base.Reset();
            position = -1;
        }
    }
    
    private sealed class MockTokenStream : TokenStream
    {
        public int DisposedCount { get; private set; }
        
        public sealed override bool IncrementToken()
        {
            return false;
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposedCount++;
            }
            base.Dispose(disposing);
        }
    }
}