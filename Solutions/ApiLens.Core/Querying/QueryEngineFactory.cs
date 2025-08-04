using ApiLens.Core.Lucene;

namespace ApiLens.Core.Querying;

/// <summary>
/// Factory implementation for creating QueryEngine instances.
/// </summary>
public class QueryEngineFactory : IQueryEngineFactory
{
    /// <inheritdoc/>
    public IQueryEngine Create(ILuceneIndexManager indexManager)
    {
        return new QueryEngine(indexManager);
    }
}