namespace ApiLens.Core.Lucene;

/// <summary>
/// Factory for creating ILuceneIndexManager instances with specific index paths.
/// </summary>
public interface ILuceneIndexManagerFactory
{
    /// <summary>
    /// Creates a new ILuceneIndexManager instance with the specified index path.
    /// </summary>
    /// <param name="indexPath">The path to the Lucene index directory.</param>
    /// <returns>A new ILuceneIndexManager instance.</returns>
    ILuceneIndexManager Create(string indexPath);
}