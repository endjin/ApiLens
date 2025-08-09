using ApiLens.Core.Models;

namespace ApiLens.Core.Services;

/// <summary>
/// Service for parsing .NET solution files.
/// </summary>
public interface ISolutionParserService
{
    /// <summary>
    /// Parses a solution file and extracts project information.
    /// </summary>
    /// <param name="solutionPath">Path to the solution file.</param>
    /// <returns>Information about the solution and its projects.</returns>
    Task<SolutionInfo> ParseSolutionAsync(string solutionPath);

    /// <summary>
    /// Gets the paths to all project files in a solution.
    /// </summary>
    /// <param name="solutionPath">Path to the solution file.</param>
    /// <returns>Collection of project file paths.</returns>
    Task<IEnumerable<string>> GetProjectPathsAsync(string solutionPath);
}