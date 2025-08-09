namespace ApiLens.Core.Models;

/// <summary>
/// Represents a NuGet package reference from a project file.
/// </summary>
public class PackageReference
{
    /// <summary>
    /// Gets or sets the package ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the package version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the target framework.
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets whether this is a transitive dependency.
    /// </summary>
    public bool IsTransitive { get; set; }

    /// <summary>
    /// Gets or sets the private assets value.
    /// </summary>
    public string? PrivateAssets { get; set; }

    /// <summary>
    /// Gets or sets the include assets value.
    /// </summary>
    public string? IncludeAssets { get; set; }

    /// <summary>
    /// Gets or sets the exclude assets value.
    /// </summary>
    public string? ExcludeAssets { get; set; }
}