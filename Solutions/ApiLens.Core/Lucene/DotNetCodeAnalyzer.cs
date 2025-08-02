using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Util;

namespace ApiLens.Core.Lucene;

public sealed class DotNetCodeAnalyzer : Analyzer
{
    private readonly LuceneVersion version;

    public DotNetCodeAnalyzer(LuceneVersion version)
    {
        this.version = version;
    }

    protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
    {
        // Use whitespace tokenizer as base (will handle the whole input as one token for now)
        WhitespaceTokenizer tokenizer = new(version, reader);

        // Use our custom filter to generate tokens
        TokenStream tokenStream = new DotNetTokenFilter(tokenizer);

        // Convert to lowercase
        tokenStream = new LowerCaseFilter(version, tokenStream);

        return new TokenStreamComponents(tokenizer, tokenStream);
    }
}