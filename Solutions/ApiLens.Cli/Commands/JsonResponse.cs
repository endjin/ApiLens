using System.Text.Json.Serialization;

namespace ApiLens.Cli.Commands;

/// <summary>
/// Standardized JSON response wrapper for all ApiLens commands.
/// Ensures consistent JSON output with comprehensive metadata.
/// </summary>
/// <typeparam name="T">The type of results being returned</typeparam>
public class JsonResponse<T>
{
    /// <summary>
    /// The actual query results or command output
    /// </summary>
    [JsonPropertyName("results")]
    public required T Results { get; set; }

    /// <summary>
    /// Metadata about the query execution and index
    /// </summary>
    [JsonPropertyName("metadata")]
    public required ResponseMetadata Metadata { get; set; }
}

/// <summary>
/// Comprehensive metadata for JSON responses
/// </summary>
public class ResponseMetadata
{
    /// <summary>
    /// Total count of results returned
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    /// <summary>
    /// Unique assemblies found in the results
    /// </summary>
    [JsonPropertyName("assemblies")]
    public List<string> Assemblies { get; set; } = [];

    /// <summary>
    /// Unique packages found in the results
    /// </summary>
    [JsonPropertyName("packages")]
    public List<string> Packages { get; set; } = [];

    /// <summary>
    /// Time taken to execute the search in milliseconds
    /// </summary>
    [JsonPropertyName("searchTime")]
    public string SearchTime { get; set; } = "0ms";

    /// <summary>
    /// Version of the ApiLens tool
    /// </summary>
    [JsonPropertyName("apiLensVersion")]
    public string ApiLensVersion { get; set; } = GetApiLensVersion();

    /// <summary>
    /// Size of the index in bytes
    /// </summary>
    [JsonPropertyName("indexSize")]
    public long IndexSize { get; set; }

    /// <summary>
    /// Total number of documents in the index
    /// </summary>
    [JsonPropertyName("indexDocumentCount")]
    public int IndexDocumentCount { get; set; }

    /// <summary>
    /// Last modification time of the index
    /// </summary>
    [JsonPropertyName("indexLastModified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? IndexLastModified { get; set; }

    /// <summary>
    /// The original query string (if applicable)
    /// </summary>
    [JsonPropertyName("query")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Query { get; set; }

    /// <summary>
    /// The type of query performed (if applicable)
    /// </summary>
    [JsonPropertyName("queryType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? QueryType { get; set; }

    /// <summary>
    /// Additional command-specific metadata
    /// </summary>
    [JsonPropertyName("commandMetadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? CommandMetadata { get; set; }

    private static string GetApiLensVersion()
    {
        var assembly = typeof(ResponseMetadata).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "1.0.0";
    }
}