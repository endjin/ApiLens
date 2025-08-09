namespace ApiLens.Core.Models;

/// <summary>
/// Represents a package dependency.
/// </summary>
public class PackageDependency
{
    /// <summary>
    /// Gets or sets the dependency ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version range.
    /// </summary>
    public string? VersionRange { get; set; }
}