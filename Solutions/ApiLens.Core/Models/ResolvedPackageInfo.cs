namespace ApiLens.Core.Models;

/// <summary>
/// Represents resolved package information from project.assets.json.
/// </summary>
public class ResolvedPackageInfo
{
    /// <summary>
    /// Gets or sets the package ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the resolved version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the package type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the SHA512 hash.
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Gets or sets the list of files in the package.
    /// </summary>
    public List<string> Files { get; set; } = [];

    /// <summary>
    /// Gets or sets the package dependencies.
    /// </summary>
    public List<PackageDependency> Dependencies { get; set; } = [];
}