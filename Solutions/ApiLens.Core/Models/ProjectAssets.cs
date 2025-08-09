namespace ApiLens.Core.Models;

/// <summary>
/// Represents parsed project.assets.json information.
/// </summary>
public class ProjectAssets
{
    /// <summary>
    /// Gets or sets whether the assets file exists and was parsed.
    /// </summary>
    public bool HasAssets { get; set; }

    /// <summary>
    /// Gets or sets the list of resolved packages.
    /// </summary>
    public List<ResolvedPackageInfo> Packages { get; set; } = [];

    /// <summary>
    /// Gets or sets the target frameworks.
    /// </summary>
    public List<string> TargetFrameworks { get; set; } = [];
}