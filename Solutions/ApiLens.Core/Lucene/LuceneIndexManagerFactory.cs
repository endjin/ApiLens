using ApiLens.Core.Parsing;

namespace ApiLens.Core.Lucene;

/// <summary>
/// Factory implementation for creating LuceneIndexManager instances.
/// </summary>
public class LuceneIndexManagerFactory : ILuceneIndexManagerFactory
{
    private readonly IXmlDocumentParser parser;
    private readonly IDocumentBuilder documentBuilder;

    public LuceneIndexManagerFactory(IXmlDocumentParser parser, IDocumentBuilder documentBuilder)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(documentBuilder);

        this.parser = parser;
        this.documentBuilder = documentBuilder;
    }

    /// <inheritdoc/>
    public ILuceneIndexManager Create(string indexPath)
    {
        if (indexPath == null)
        {
            throw new ArgumentNullException(nameof(indexPath));
        }

        return new LuceneIndexManager(indexPath, parser, documentBuilder);
    }
}