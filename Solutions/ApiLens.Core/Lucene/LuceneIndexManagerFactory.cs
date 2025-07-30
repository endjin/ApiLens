namespace ApiLens.Core.Lucene;

/// <summary>
/// Factory implementation for creating LuceneIndexManager instances.
/// </summary>
public class LuceneIndexManagerFactory : ILuceneIndexManagerFactory
{
    /// <inheritdoc/>
    public ILuceneIndexManager Create(string indexPath)
    {
        return new LuceneIndexManager(indexPath);
    }
}