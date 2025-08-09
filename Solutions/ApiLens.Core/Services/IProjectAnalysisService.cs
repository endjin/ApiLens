using ApiLens.Core.Models;

namespace ApiLens.Core.Services;

/// <summary>
/// Service for analyzing .NET projects and solutions.
/// </summary>
public interface IProjectAnalysisService
{
    /// <summary>
    /// Analyzes a project or solution file.
    /// </summary>
    /// <param name="path">Path to the project or solution file.</param>
    /// <param name="includeTransitive">Whether to include transitive dependencies.</param>
    /// <param name="useAssetsFile">Whether to use project.assets.json for resolved versions.</param>
    /// <returns>Analysis result containing package information.</returns>
    Task<ProjectAnalysisResult> AnalyzeAsync(string path, bool includeTransitive = false, bool useAssetsFile = false);

    /// <summary>
    /// Determines if a file is a project or solution file.
    /// </summary>
    /// <param name="path">Path to check.</param>
    /// <returns>True if the file is a project or solution file.</returns>
    bool IsProjectOrSolution(string path);

    /// <summary>
    /// Gets the project type from a file path.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <returns>The project type.</returns>
    ProjectType GetProjectType(string path);
}