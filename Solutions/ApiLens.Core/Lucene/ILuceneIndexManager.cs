using ApiLens.Core.Models;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace ApiLens.Core.Lucene;

public interface ILuceneIndexManager : IDisposable
{
    // High-performance batch operations
    Task<IndexingResult> IndexBatchAsync(IEnumerable<MemberInfo> members, CancellationToken cancellationToken = default);
    Task<IndexingResult> IndexXmlFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default);

    // Document management
    void DeleteDocument(Term term);
    void DeleteAll();
    Task CommitAsync();

    // Search operations
    TopDocs SearchByField(string fieldName, string searchTerm, int maxResults = 100);
    TopDocs SearchWithQuery(Query query, int maxResults = 100);
    Document? GetDocument(int docId);
    List<Document> SearchByIntRange(string fieldName, int min, int max, int maxResults);
    List<Document> SearchByFieldExists(string fieldName, int maxResults);

    // Index statistics
    int GetTotalDocuments();
    long GetIndexSizeInBytes();
    IndexStatistics? GetIndexStatistics();

    // Performance metrics
    PerformanceMetrics GetPerformanceMetrics();
}