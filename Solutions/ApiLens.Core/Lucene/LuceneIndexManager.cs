using ApiLens.Core.Models;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace ApiLens.Core.Lucene;

public class LuceneIndexManager : ILuceneIndexManager
{
    private readonly global::Lucene.Net.Store.Directory directory;
    private readonly Analyzer analyzer;
    private readonly IndexWriter writer;
    private readonly LuceneVersion version = LuceneVersion.LUCENE_48;
    private readonly string? indexPath;
    private bool disposed;

    public LuceneIndexManager(global::Lucene.Net.Store.Directory directory, string? indexPath = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        this.directory = directory;
        analyzer = new DotNetCodeAnalyzer(version);
        this.indexPath = indexPath;

        IndexWriterConfig config = new(version, analyzer)
        {
            OpenMode = OpenMode.CREATE_OR_APPEND
        };

        writer = new IndexWriter(directory, config);
    }

    public LuceneIndexManager(string indexPath) : this(FSDirectory.Open(indexPath), indexPath)
    {
    }

    public void AddDocument(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        writer.AddDocument(document);
    }

    public void UpdateDocument(Term term, Document document)
    {
        ArgumentNullException.ThrowIfNull(term);
        ArgumentNullException.ThrowIfNull(document);
        writer.UpdateDocument(term, document);
    }

    public void DeleteDocument(Term term)
    {
        ArgumentNullException.ThrowIfNull(term);
        writer.DeleteDocuments(term);
    }

    public void DeleteAll()
    {
        writer.DeleteAll();
    }

    public void Commit()
    {
        writer.Commit();
    }

    public DirectoryReader OpenReader()
    {
        return DirectoryReader.Open(directory);
    }

    public List<Document> SearchByField(string fieldName, string queryText, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        using DirectoryReader reader = OpenReader();
        IndexSearcher searcher = new(reader);

        Query query;

        // For string fields (not analyzed), use TermQuery
        if (fieldName is "id" or "memberType" or "assembly" or "name" or "fullName" or "namespace"
            or "exceptionType" or "attribute" or "crossref" or "memberTypeFacet")
        {
            query = new TermQuery(new Term(fieldName, queryText));
        }
        else
        {
            // For text fields (analyzed), use QueryParser
            QueryParser parser = new(version, fieldName, analyzer);
            query = parser.Parse(queryText);
        }

        TopDocs topDocs = searcher.Search(query, maxResults);
        List<Document> results = [];

        foreach (ScoreDoc? scoreDoc in topDocs.ScoreDocs)
        {
            results.Add(searcher.Doc(scoreDoc.Doc));
        }

        return results;
    }

    public List<Document> SearchByIntRange(string fieldName, int min, int max, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        List<Document> results = [];

        try
        {
            using DirectoryReader reader = writer.GetReader(true);
            IndexSearcher searcher = new(reader);

            // Create a numeric range query
            Query query = NumericRangeQuery.NewInt32Range(fieldName, min, max, true, true);

            TopDocs topDocs = searcher.Search(query, maxResults);
            foreach (ScoreDoc scoreDoc in topDocs.ScoreDocs)
            {
                Document doc = searcher.Doc(scoreDoc.Doc);
                results.Add(doc);
            }
        }
        catch (IOException)
        {
            // Return empty results on search error
        }

        return results;
    }

    public List<Document> SearchByFieldExists(string fieldName, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        List<Document> results = [];

        try
        {
            using DirectoryReader reader = writer.GetReader(true);
            IndexSearcher searcher = new(reader);

            // Create a wildcard query to find documents where the field exists
            Query query = new WildcardQuery(new Term(fieldName, "*"));

            TopDocs topDocs = searcher.Search(query, maxResults);
            foreach (ScoreDoc scoreDoc in topDocs.ScoreDocs)
            {
                Document doc = searcher.Doc(scoreDoc.Doc);
                results.Add(doc);
            }
        }
        catch (IOException)
        {
            // Return empty results on search error
        }

        return results;
    }

    public IndexStatistics? GetIndexStatistics()
    {
        try
        {
            // Get the directory path
            string resolvedIndexPath = indexPath ?? "Unknown";
            long totalSize = 0;
            int fileCount = 0;
            DateTime? lastModified = null;

            // If we have the index path, try to calculate file statistics
            // This is optional and will gracefully fail if file system access is not available
            if (!string.IsNullOrEmpty(indexPath))
            {
                try
                {
                    if (System.IO.Directory.Exists(indexPath))
                    {
                        DirectoryInfo dirInfo = new(indexPath);
                        FileInfo[] files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                        fileCount = files.Length;
                        totalSize = files.Sum(f => f.Length);
                        lastModified = files.Length > 0 ? files.Max(f => f.LastWriteTime) : null;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // File system access might not be available in all contexts
                    // Continue without file statistics
                }
                catch (IOException)
                {
                    // File system access might fail due to I/O issues
                    // Continue without file statistics
                }
            }

            // Get document and field counts
            int documentCount = 0;
            int fieldCount = 0;

            try
            {
                using DirectoryReader reader = OpenReader();
                documentCount = reader.NumDocs;

                // Count unique fields across all documents
                HashSet<string> uniqueFields = [];
                for (int i = 0; i < Math.Min(100, documentCount); i++) // Sample first 100 docs for performance
                {
                    Document doc = reader.Document(i);
                    foreach (IIndexableField field in doc.Fields)
                    {
                        uniqueFields.Add(field.Name);
                    }
                }
                fieldCount = uniqueFields.Count;
            }
            catch (global::Lucene.Net.Index.CorruptIndexException)
            {
                // Index might be corrupted
            }
            catch (IOException)
            {
                // Index might be empty or not yet created
            }

            return new IndexStatistics
            {
                IndexPath = resolvedIndexPath,
                TotalSizeInBytes = totalSize,
                DocumentCount = documentCount,
                FieldCount = fieldCount,
                FileCount = fileCount,
                LastModified = lastModified
            };
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }


    public void Dispose()
    {
        if (disposed) return;

        writer?.Dispose();
        analyzer?.Dispose();
        directory?.Dispose();

        disposed = true;
    }
}