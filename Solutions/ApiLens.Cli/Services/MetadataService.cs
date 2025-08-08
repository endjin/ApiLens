using System.Diagnostics;
using ApiLens.Cli.Commands;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;

namespace ApiLens.Cli.Services;

/// <summary>
/// Service for collecting metadata for JSON responses
/// </summary>
public class MetadataService
{
    private readonly Stopwatch _stopwatch;

    public MetadataService()
    {
        _stopwatch = new Stopwatch();
    }

    /// <summary>
    /// Start timing the operation
    /// </summary>
    public void StartTiming()
    {
        _stopwatch.Restart();
    }

    /// <summary>
    /// Stop timing and get the elapsed time in milliseconds format
    /// </summary>
    public string GetElapsedTime()
    {
        _stopwatch.Stop();
        return $"{_stopwatch.ElapsedMilliseconds}ms";
    }

    /// <summary>
    /// Build metadata from query results and index manager
    /// </summary>
    public ResponseMetadata BuildMetadata(
        IEnumerable<MemberInfo> results,
        ILuceneIndexManager indexManager,
        string? query = null,
        string? queryType = null,
        Dictionary<string, object>? commandMetadata = null)
    {
        List<MemberInfo> resultsList = results.ToList();
        IndexStatistics? indexStats = indexManager.GetIndexStatistics();

        ResponseMetadata metadata = new()
        {
            TotalCount = resultsList.Count,
            SearchTime = GetElapsedTime(),
            Query = query,
            QueryType = queryType,
            CommandMetadata = commandMetadata
        };

        // Extract unique assemblies
        metadata.Assemblies = resultsList
            .Select(r => r.Assembly)
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct()
            .OrderBy(a => a)
            .ToList();

        // Extract unique packages
        metadata.Packages = resultsList
            .Where(r => !string.IsNullOrEmpty(r.PackageId))
            .Select(r => r.PackageId!)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        // Add index statistics if available
        if (indexStats != null)
        {
            metadata.IndexSize = indexStats.TotalSizeInBytes;
            metadata.IndexDocumentCount = indexStats.DocumentCount;
            metadata.IndexLastModified = indexStats.LastModified;
        }
        else
        {
            // Fallback to basic stats
            metadata.IndexDocumentCount = indexManager.GetTotalDocuments();
            metadata.IndexSize = indexManager.GetIndexSizeInBytes();
        }

        return metadata;
    }

    /// <summary>
    /// Build metadata for non-MemberInfo results
    /// </summary>
    public ResponseMetadata BuildMetadata<T>(
        IEnumerable<T> results,
        ILuceneIndexManager indexManager,
        string? query = null,
        string? queryType = null,
        Dictionary<string, object>? commandMetadata = null)
    {
        List<T> resultsList = results.ToList();
        IndexStatistics? indexStats = indexManager.GetIndexStatistics();

        ResponseMetadata metadata = new()
        {
            TotalCount = resultsList.Count,
            SearchTime = GetElapsedTime(),
            Query = query,
            QueryType = queryType,
            CommandMetadata = commandMetadata
        };

        // Add index statistics if available
        if (indexStats != null)
        {
            metadata.IndexSize = indexStats.TotalSizeInBytes;
            metadata.IndexDocumentCount = indexStats.DocumentCount;
            metadata.IndexLastModified = indexStats.LastModified;
        }
        else
        {
            // Fallback to basic stats
            metadata.IndexDocumentCount = indexManager.GetTotalDocuments();
            metadata.IndexSize = indexManager.GetIndexSizeInBytes();
        }

        return metadata;
    }

    /// <summary>
    /// Build metadata for operations that don't have results
    /// </summary>
    public ResponseMetadata BuildMetadata(
        ILuceneIndexManager indexManager,
        string? query = null,
        string? queryType = null,
        Dictionary<string, object>? commandMetadata = null)
    {
        IndexStatistics? indexStats = indexManager.GetIndexStatistics();

        ResponseMetadata metadata = new()
        {
            TotalCount = 0,
            SearchTime = GetElapsedTime(),
            Query = query,
            QueryType = queryType,
            CommandMetadata = commandMetadata
        };

        // Add index statistics if available
        if (indexStats != null)
        {
            metadata.IndexSize = indexStats.TotalSizeInBytes;
            metadata.IndexDocumentCount = indexStats.DocumentCount;
            metadata.IndexLastModified = indexStats.LastModified;
        }
        else
        {
            // Fallback to basic stats
            metadata.IndexDocumentCount = indexManager.GetTotalDocuments();
            metadata.IndexSize = indexManager.GetIndexSizeInBytes();
        }

        return metadata;
    }
}