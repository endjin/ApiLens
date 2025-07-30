using ApiLens.Core.Lucene;

namespace ApiLens.Core.Querying;

/// <summary>
/// Factory for creating IQueryEngine instances.
/// </summary>
public interface IQueryEngineFactory
{
    /// <summary>
    /// Creates a new IQueryEngine instance with the specified index manager.
    /// </summary>
    /// <param name="indexManager">The Lucene index manager to use.</param>
    /// <returns>A new IQueryEngine instance.</returns>
    IQueryEngine Create(ILuceneIndexManager indexManager);
}