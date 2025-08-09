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