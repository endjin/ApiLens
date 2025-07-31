using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using ApiLens.Core.Infrastructure;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace ApiLens.Core.Lucene;

public sealed class LuceneIndexManager : ILuceneIndexManager
{
    private readonly global::Lucene.Net.Store.Directory directory;
    private readonly IndexWriter writer;
    private readonly IXmlDocumentParser parser;
    private readonly IDocumentBuilder documentBuilder;
    private readonly string? indexPath;

    // Performance settings
    private const int BatchSize = 50_000;
    private const int ChannelCapacity = 100_000;
    private const double RamBufferSizeMB = 512.0;

    // Object pools
    private readonly ObjectPool<Document> documentPool;
    private readonly ObjectPool<StringBuilder> stringBuilderPool;
    private readonly StringInternCache stringCache;

    // Channels for parallel processing
    private readonly Channel<ParseTask> parseChannel;
    private readonly Channel<Document> documentChannel;

    // Pre-created analyzers
    private readonly KeywordAnalyzer keywordAnalyzer;
    private readonly WhitespaceAnalyzer whitespaceAnalyzer;
    private readonly PerFieldAnalyzerWrapper analyzer;

    // Performance tracking
    private readonly PerformanceTracker performanceTracker;
    private bool disposed;

    public LuceneIndexManager(
        string indexPath,
        IXmlDocumentParser parser,
        IDocumentBuilder documentBuilder)
    {
        ArgumentNullException.ThrowIfNull(indexPath);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(documentBuilder);

        this.indexPath = indexPath;
        this.parser = parser;
        this.documentBuilder = documentBuilder;

        // Initialize directory
        directory = FSDirectory.Open(indexPath);

        // Initialize analyzers
        keywordAnalyzer = new KeywordAnalyzer();
        whitespaceAnalyzer = new WhitespaceAnalyzer(LuceneVersion.LUCENE_48);

        // Configure per-field analyzer
        Dictionary<string, Analyzer> fieldAnalyzers = new()
        {
            { "id", keywordAnalyzer },
            { "memberType", keywordAnalyzer },
            { "name", keywordAnalyzer },
            { "fullName", keywordAnalyzer },
            { "assembly", keywordAnalyzer },
            { "namespace", keywordAnalyzer },
            { "memberTypeFacet", keywordAnalyzer },
            { "crossref", keywordAnalyzer },
            { "exceptionType", keywordAnalyzer },
            { "attribute", keywordAnalyzer },
            { "packageId", keywordAnalyzer },
            { "packageVersion", keywordAnalyzer },
            { "targetFramework", keywordAnalyzer },
            { "contentHash", keywordAnalyzer }
        };

        analyzer = new PerFieldAnalyzerWrapper(whitespaceAnalyzer, fieldAnalyzers);

        // Configure IndexWriter for maximum performance
        IndexWriterConfig config = new(LuceneVersion.LUCENE_48, analyzer)
        {
            OpenMode = OpenMode.CREATE_OR_APPEND,
            RAMBufferSizeMB = RamBufferSizeMB,
            MaxBufferedDocs = BatchSize,
            MergePolicy = new TieredMergePolicy
            {
                MaxMergeAtOnce = 10,
                SegmentsPerTier = 10
            },
            UseCompoundFile = false
        };

        writer = new IndexWriter(directory, config);

        // Initialize object pools
        documentPool = new ObjectPool<Document>(
            () => [],
            doc => { }, // Documents can't be cleared, we'll create new ones
            maxSize: 4096);

        stringBuilderPool = new ObjectPool<StringBuilder>(
            () => new StringBuilder(4096),
            sb => sb.Clear(),
            maxSize: 1024);

        stringCache = new StringInternCache(maxSize: 10000);

        // Initialize channels
        parseChannel = Channel.CreateBounded<ParseTask>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = false
        });

        documentChannel = Channel.CreateBounded<Document>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = true
        });

        performanceTracker = new PerformanceTracker();
    }

    public async Task<IndexingResult> IndexBatchAsync(
        IEnumerable<MemberInfo> members,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield(); // Ensure async
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<string> errors = [];
        int successCount = 0;
        int failCount = 0;

        try
        {
            List<Document> batch = new(BatchSize);

            foreach (MemberInfo member in members)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    Document doc = documentBuilder.BuildDocument(member);
                    batch.Add(doc);
                    successCount++;

                    if (batch.Count >= BatchSize)
                    {
                        foreach (Document batchDoc in batch)
                        {
                            Term idTerm = new("id", batchDoc.Get("id"));
                            writer.UpdateDocument(idTerm, batchDoc);
                        }
                        writer.Commit();
                        batch.Clear();
                        performanceTracker.RecordBatchCommit(BatchSize);
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    failCount++;
                    errors.Add($"Failed to index member {member.Id}: {ex.Message}");
                }
            }

            // Commit remaining documents
            if (batch.Count > 0)
            {
                foreach (Document batchDoc in batch)
                {
                    Term idTerm = new("id", batchDoc.Get("id"));
                    writer.UpdateDocument(idTerm, batchDoc);
                }
                writer.Commit();
                performanceTracker.RecordBatchCommit(batch.Count);
            }

            return new IndexingResult
            {
                TotalDocuments = successCount + failCount,
                SuccessfulDocuments = successCount,
                FailedDocuments = failCount,
                ElapsedTime = stopwatch.Elapsed,
                BytesProcessed = 0, // Would need to track this
                Metrics = performanceTracker.GetMetrics(),
                Errors = [.. errors]
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            errors.Add($"Indexing failed: {ex.Message}");
            return new IndexingResult
            {
                TotalDocuments = successCount + failCount,
                SuccessfulDocuments = successCount,
                FailedDocuments = failCount,
                ElapsedTime = stopwatch.Elapsed,
                BytesProcessed = 0,
                Metrics = performanceTracker.GetMetrics(),
                Errors = [.. errors]
            };
        }
    }

    public async Task<IndexingResult> IndexXmlFilesAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        return await IndexXmlFilesAsync(filePaths, null, cancellationToken);
    }

    public async Task<IndexingResult> IndexXmlFilesAsync(
        IEnumerable<string> filePaths,
        Action<int>? progressCallback,
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<string> errors = [];
        int totalDocuments = 0;
        int successCount = 0;
        long bytesProcessed = 0;

        // Start indexing pipeline
        Task indexingTask = StartIndexingPipelineAsync(cancellationToken);

        // Parse files in parallel
        ParallelOptions parseOptions = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        // Track progress
        List<string> filePathList = filePaths.ToList();
        int filesProcessed = 0;

        await Parallel.ForEachAsync(filePathList, parseOptions, async (filePath, ct) =>
        {
            try
            {
                long fileSize = 0;
                try
                {
                    FileInfo fileInfo = new(filePath);
                    if (fileInfo.Exists)
                    {
                        fileSize = fileInfo.Length;
                    }
                }
                catch (IOException)
                {
                    // File might not exist in unit tests, continue anyway
                }
                catch (UnauthorizedAccessException)
                {
                    // File access denied, continue anyway
                }
                Interlocked.Add(ref bytesProcessed, fileSize);

                int membersInFile = 0;
                await foreach (MemberInfo member in parser.ParseXmlFileStreamAsync(filePath, ct))
                {
                    Document doc = documentBuilder.BuildDocument(member);
                    await documentChannel.Writer.WriteAsync(doc, ct);
                    Interlocked.Increment(ref totalDocuments);
                    Interlocked.Increment(ref successCount);
                    membersInFile++;
                }

                // Track files that produce 0 members
                if (membersInFile == 0)
                {
                    // Add a special document to track empty XML files
                    Document emptyFileDoc = new Document();
                    emptyFileDoc.Add(new StringField("id", $"EMPTY_FILE|{NormalizePath(filePath)}", Field.Store.YES));
                    emptyFileDoc.Add(new StringField("documentType", "EmptyXmlFile", Field.Store.YES));
                    emptyFileDoc.Add(new StringField("sourceFilePath", NormalizePath(filePath), Field.Store.YES));
                    await documentChannel.Writer.WriteAsync(emptyFileDoc, ct);
                    Interlocked.Increment(ref totalDocuments);
                    Interlocked.Increment(ref successCount);
                }

                // Report progress after each file
                int currentProgress = Interlocked.Increment(ref filesProcessed);
                progressCallback?.Invoke(currentProgress);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                lock (errors)
                {
                    errors.Add($"Failed to parse {filePath}: {ex.Message}");
                }
            }
        });

        // Signal completion by sending a sentinel document
        Document sentinelDoc = new Document();
        sentinelDoc.Add(new StringField("id", "SENTINEL_END_OF_STREAM", Field.Store.NO));
        await documentChannel.Writer.WriteAsync(sentinelDoc, cancellationToken);

        // Wait for indexing to complete
        await indexingTask;

        return new IndexingResult
        {
            TotalDocuments = totalDocuments,
            SuccessfulDocuments = successCount,
            FailedDocuments = totalDocuments - successCount,
            ElapsedTime = stopwatch.Elapsed,
            BytesProcessed = bytesProcessed,
            Metrics = performanceTracker.GetMetrics(),
            Errors = [.. errors]
        };
    }


    private async Task StartIndexingPipelineAsync(CancellationToken cancellationToken)
    {
        List<Document> batch = new(BatchSize);

        await foreach (Document doc in documentChannel.Reader.ReadAllAsync(cancellationToken))
        {
            // Check for sentinel document
            string? id = doc.Get("id");
            if (id == "SENTINEL_END_OF_STREAM")
            {
                // Process any remaining documents in the batch before exiting
                if (batch.Count > 0)
                {
                    Stopwatch batchStopwatch = Stopwatch.StartNew();
                    foreach (Document batchDoc in batch)
                    {
                        string? docId = batchDoc.Get("id");
                        if (!string.IsNullOrEmpty(docId))
                        {
                            Term idTerm = new("id", docId);
                            writer.UpdateDocument(idTerm, batchDoc);
                        }
                    }
                    writer.Commit();
                    performanceTracker.RecordBatchCommit(batch.Count, batchStopwatch.Elapsed);
                }
                return; // Exit the pipeline
            }

            batch.Add(doc);

            if (batch.Count >= BatchSize)
            {
                Stopwatch batchStopwatch = Stopwatch.StartNew();
                foreach (Document batchDoc in batch)
                {
                    string? docId = batchDoc.Get("id");
                    if (!string.IsNullOrEmpty(docId))
                    {
                        Term idTerm = new("id", docId);
                        writer.UpdateDocument(idTerm, batchDoc);
                    }
                }
                writer.Commit();
                performanceTracker.RecordBatchCommit(batch.Count, batchStopwatch.Elapsed);
                batch.Clear();
            }
        }

        // This should not be reached if sentinel is properly sent
        // But keeping it for safety
        if (batch.Count > 0)
        {
            Stopwatch batchStopwatch = Stopwatch.StartNew();
            foreach (Document batchDoc in batch)
            {
                string? docId = batchDoc.Get("id");
                if (!string.IsNullOrEmpty(docId))
                {
                    Term idTerm = new("id", docId);
                    writer.UpdateDocument(idTerm, batchDoc);
                }
            }
            writer.Commit();
            performanceTracker.RecordBatchCommit(batch.Count, batchStopwatch.Elapsed);
        }
    }

    public void DeleteDocument(Term term)
    {
        ArgumentNullException.ThrowIfNull(term);
        writer.DeleteDocuments(term);
    }

    public void DeleteDocumentsByPackageId(string packageId)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        Term packageTerm = new("packageId", packageId);
        writer.DeleteDocuments(packageTerm);
    }

    public void DeleteDocumentsByPackageIds(IEnumerable<string> packageIds)
    {
        ArgumentNullException.ThrowIfNull(packageIds);
        foreach (string packageId in packageIds)
        {
            Term packageTerm = new("packageId", packageId);
            writer.DeleteDocuments(packageTerm);
        }
    }

    public void DeleteAll()
    {
        writer.DeleteAll();
    }

    public async Task CommitAsync()
    {
        await Task.Run(() => writer.Commit());
    }

    public TopDocs SearchByField(string fieldName, string searchTerm, int maxResults = 100)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        ArgumentNullException.ThrowIfNull(searchTerm);

        using DirectoryReader? reader = writer.GetReader(applyAllDeletes: true);
        IndexSearcher searcher = new(reader);

        // For keyword fields, use a TermQuery for exact matching
        Query query;
        if (fieldName is "id" or "memberType" or "name" or "fullName" or "assembly" or "namespace")
        {
            query = new TermQuery(new Term(fieldName, searchTerm));
        }
        else
        {
            QueryParser parser = new(LuceneVersion.LUCENE_48, fieldName, analyzer);
            query = parser.Parse(searchTerm);
        }

        return searcher.Search(query, maxResults);
    }

    public TopDocs SearchWithQuery(Query query, int maxResults = 100)
    {
        ArgumentNullException.ThrowIfNull(query);

        using DirectoryReader? reader = writer.GetReader(applyAllDeletes: true);
        IndexSearcher searcher = new(reader);

        return searcher.Search(query, maxResults);
    }

    public Document? GetDocument(int docId)
    {
        using DirectoryReader? reader = writer.GetReader(applyAllDeletes: true);
        IndexSearcher searcher = new(reader);

        try
        {
            return searcher.Doc(docId);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch
#pragma warning restore CA1031 // Do not catch general exception types
        {
            return null;
        }
    }

    public List<Document> SearchByIntRange(string fieldName, int min, int max, int maxResults)
    {
        using DirectoryReader? reader = writer.GetReader(applyAllDeletes: true);
        IndexSearcher searcher = new(reader);

        NumericRangeQuery<int>? query = NumericRangeQuery.NewInt32Range(fieldName, min, max, true, true);
        TopDocs topDocs = searcher.Search(query, maxResults);

        List<Document> results = new(topDocs.ScoreDocs.Length);
        foreach (ScoreDoc? scoreDoc in topDocs.ScoreDocs)
        {
            results.Add(searcher.Doc(scoreDoc.Doc));
        }

        return results;
    }

    public List<Document> SearchByFieldExists(string fieldName, int maxResults)
    {
        using DirectoryReader? reader = writer.GetReader(applyAllDeletes: true);
        IndexSearcher searcher = new(reader);

        TermRangeQuery query = new(fieldName, null, null, true, true);
        TopDocs topDocs = searcher.Search(query, maxResults);

        List<Document> results = new(topDocs.ScoreDocs.Length);
        foreach (ScoreDoc? scoreDoc in topDocs.ScoreDocs)
        {
            results.Add(searcher.Doc(scoreDoc.Doc));
        }

        return results;
    }

    public int GetTotalDocuments()
    {
        // Force a commit to ensure all documents are visible
        writer.Commit();
        using DirectoryReader? reader = writer.GetReader(applyAllDeletes: true);
        return reader.NumDocs;
    }

    public long GetIndexSizeInBytes()
    {
        long totalSize = 0;

        if (!string.IsNullOrEmpty(indexPath) && System.IO.Directory.Exists(indexPath))
        {
            DirectoryInfo dirInfo = new(indexPath);
            foreach (FileInfo file in dirInfo.GetFiles())
            {
                totalSize += file.Length;
            }
        }

        return totalSize;
    }

    public IndexStatistics? GetIndexStatistics()
    {
        if (string.IsNullOrEmpty(indexPath))
            return null;

        return new IndexStatistics
        {
            DocumentCount = GetTotalDocuments(),
            IndexPath = indexPath,
            TotalSizeInBytes = GetIndexSizeInBytes(),
            FieldCount = 0, // Would need to count unique fields
            FileCount = System.IO.Directory.Exists(indexPath)
                ? System.IO.Directory.GetFiles(indexPath).Length
                : 0
        };
    }

    public PerformanceMetrics GetPerformanceMetrics()
    {
        return performanceTracker.GetMetrics();
    }

    public Dictionary<string, HashSet<string>> GetIndexedPackageVersions()
    {
        Dictionary<string, HashSet<string>> packageVersions = new(StringComparer.OrdinalIgnoreCase);

        using DirectoryReader? reader = writer.GetReader(applyAllDeletes: true);

        // Only load the fields we need for efficiency
        HashSet<string> fieldsToLoad = ["packageId", "packageVersion"];

        // Iterate through all documents efficiently
        for (int i = 0; i < reader.MaxDoc; i++)
        {
            // Skip deleted documents
            var liveDocs = MultiFields.GetLiveDocs(reader);
            if (liveDocs != null && !liveDocs.Get(i))
                continue;

            Document? doc = reader.Document(i, fieldsToLoad);

            string? packageId = doc?.Get("packageId");
            string? version = doc?.Get("packageVersion");

            if (!string.IsNullOrWhiteSpace(packageId) && !string.IsNullOrWhiteSpace(version))
            {
                if (!packageVersions.TryGetValue(packageId, out HashSet<string>? versions))
                {
                    versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    packageVersions[packageId] = versions;
                }
                versions.Add(version);
            }
        }

        return packageVersions;
    }

    public Dictionary<string, HashSet<(string Version, string Framework)>> GetIndexedPackageVersionsWithFramework()
    {
        Dictionary<string, HashSet<(string, string)>> packageVersions = new(StringComparer.OrdinalIgnoreCase);

        using DirectoryReader? reader = writer.GetReader(applyAllDeletes: true);

        // Only load the fields we need for efficiency
        HashSet<string> fieldsToLoad = ["packageId", "packageVersion", "targetFramework"];

        // Iterate through all documents efficiently
        for (int i = 0; i < reader.MaxDoc; i++)
        {
            // Skip deleted documents
            var liveDocs = MultiFields.GetLiveDocs(reader);
            if (liveDocs != null && !liveDocs.Get(i))
                continue;

            Document? doc = reader.Document(i, fieldsToLoad);

            string? packageId = doc?.Get("packageId");
            string? version = doc?.Get("packageVersion");
            string? framework = doc?.Get("targetFramework") ?? "unknown";

            if (!string.IsNullOrWhiteSpace(packageId) && !string.IsNullOrWhiteSpace(version))
            {
                if (!packageVersions.TryGetValue(packageId, out HashSet<(string, string)>? versions))
                {
                    versions = new HashSet<(string, string)>();
                    packageVersions[packageId] = versions;
                }
                versions.Add((version, framework));
            }
        }

        return packageVersions;
    }

    public HashSet<string> GetIndexedXmlPaths()
    {
        HashSet<string> xmlPaths = new(StringComparer.OrdinalIgnoreCase);

        using DirectoryReader? reader = writer.GetReader(applyAllDeletes: true);

        // Only load the sourceFilePath field for efficiency
        HashSet<string> fieldsToLoad = ["sourceFilePath"];

        // Iterate through all documents efficiently
        HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < reader.MaxDoc; i++)
        {
            Document? doc = reader.Document(i, fieldsToLoad);
            string? sourcePath = doc?.Get("sourceFilePath");

            if (!string.IsNullOrWhiteSpace(sourcePath) && seenPaths.Add(sourcePath))
            {
                xmlPaths.Add(sourcePath);
            }
        }

        return xmlPaths;
    }

    public HashSet<string> GetEmptyXmlPaths()
    {
        HashSet<string> emptyPaths = new(StringComparer.OrdinalIgnoreCase);

        using DirectoryReader? reader = writer.GetReader(applyAllDeletes: true);

        // Search for empty file documents
        TermQuery query = new(new Term("documentType", "EmptyXmlFile"));
        IndexSearcher searcher = new(reader);
        TopDocs topDocs = searcher.Search(query, int.MaxValue);

        HashSet<string> fieldsToLoad = ["sourceFilePath"];

        foreach (ScoreDoc scoreDoc in topDocs.ScoreDocs)
        {
            Document? doc = searcher.Doc(scoreDoc.Doc, fieldsToLoad);
            string? sourcePath = doc?.Get("sourceFilePath");

            if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                emptyPaths.Add(sourcePath);
            }
        }

        return emptyPaths;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        writer?.Dispose();
        directory?.Dispose();
        analyzer?.Dispose();
        keywordAnalyzer?.Dispose();
        whitespaceAnalyzer?.Dispose();
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private record ParseTask(string FilePath, CancellationToken CancellationToken);

    private sealed class PerformanceTracker
    {
        private readonly ConcurrentBag<TimeSpan> batchCommitTimes = [];
        private readonly Stopwatch totalStopwatch = Stopwatch.StartNew();
        private int documentsIndexed;

        public void RecordBatchCommit(int batchSize, TimeSpan? elapsed = null)
        {
            Interlocked.Add(ref documentsIndexed, batchSize);
            if (elapsed.HasValue)
            {
                batchCommitTimes.Add(elapsed.Value);
            }
        }

        public PerformanceMetrics GetMetrics()
        {
            long gcInfo = GC.GetTotalMemory(false);

            return new PerformanceMetrics
            {
                TotalAllocatedBytes = gcInfo,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                AverageParseTimeMs = 0, // Would need to track separately
                AverageIndexTimeMs = 0, // Would need to track separately
                AverageBatchCommitTimeMs = batchCommitTimes.Count > 0
                    ? batchCommitTimes.Average(t => t.TotalMilliseconds)
                    : 0,
                PeakThreadCount = ThreadPool.ThreadCount,
                CpuUsagePercent = 0, // Would need platform-specific code
                PeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64,
                DocumentsPooled = 0, // Would need to expose from pools
                StringsInterned = 0 // Would need to expose from cache
            };
        }
    }
}