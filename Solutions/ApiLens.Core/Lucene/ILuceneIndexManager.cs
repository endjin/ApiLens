using ApiLens.Core.Models;
using Lucene.Net.Documents;
using Lucene.Net.Index;

namespace ApiLens.Core.Lucene;

public interface ILuceneIndexManager : IDisposable
{
    void AddDocument(Document document);
    void UpdateDocument(Term term, Document document);
    void DeleteDocument(Term term);
    void DeleteAll();
    void Commit();
    List<Document> SearchByField(string fieldName, string queryText, int maxResults);
    List<Document> SearchByIntRange(string fieldName, int min, int max, int maxResults);
    List<Document> SearchByFieldExists(string fieldName, int maxResults);
    IndexStatistics? GetIndexStatistics();
}