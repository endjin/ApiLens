namespace ApiLens.Core.Models;

/// <summary>
/// Represents information about a NuGet package found in the local cache.
/// </summary>
public record NuGetPackageInfo
{
    /// <summary>
    /// Gets the package ID (e.g., "Newtonsoft.Json").
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// Gets the package version (e.g., "13.0.1").
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the target framework (e.g., "net6.0").
    /// </summary>
    public required string TargetFramework { get; init; }

    /// <summary>
    /// Gets the full path to the XML documentation file.
    /// </summary>
    public required string XmlDocumentationPath { get; init; }

    /// <summary>
    /// Gets the content hash for deduplication (computed separately).
    /// </summary>
    public string? ContentHash { get; init; }

    /// <summary>
    /// Gets the timestamp when this package was indexed.
    /// </summary>
    public DateTime IndexedAt { get; init; } = DateTime.UtcNow;
}