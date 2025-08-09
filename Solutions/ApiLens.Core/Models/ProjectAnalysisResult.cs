namespace ApiLens.Core.Models;

/// <summary>
/// Represents the result of analyzing a project or solution.
/// </summary>
public class ProjectAnalysisResult
{
    /// <summary>
    /// Gets or sets the path to the project or solution.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of file analyzed.
    /// </summary>
    public ProjectType Type { get; set; }

    /// <summary>
    /// Gets or sets the list of package references found.
    /// </summary>
    public List<PackageReference> Packages { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of frameworks.
    /// </summary>
    public List<string> Frameworks { get; set; } = [];

    /// <summary>
    /// Gets or sets statistics about the analysis.
    /// </summary>
    public Dictionary<string, int> Statistics { get; set; } = [];

    /// <summary>
    /// Gets or sets any warnings generated during analysis.
    /// </summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of projects analyzed (for solutions).
    /// </summary>
    public List<string> ProjectPaths { get; set; } = [];
}