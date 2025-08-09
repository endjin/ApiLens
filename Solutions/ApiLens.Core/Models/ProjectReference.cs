namespace ApiLens.Core.Models;

/// <summary>
/// Represents a reference to a project within a solution.
/// </summary>
public class ProjectReference
{
    /// <summary>
    /// Gets or sets the name of the project.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the project file.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project type GUID.
    /// </summary>
    public string? ProjectTypeGuid { get; set; }
}