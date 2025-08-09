namespace ApiLens.Core.Models;

/// <summary>
/// Represents information about a .NET solution file.
/// </summary>
public class SolutionInfo
{
    /// <summary>
    /// Gets or sets the path to the solution file.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of projects in the solution.
    /// </summary>
    public List<ProjectReference> Projects { get; set; } = [];
}