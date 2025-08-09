using ApiLens.Core.Models;

namespace ApiLens.Core.Services;

/// <summary>
/// Service for parsing project.assets.json files.
/// </summary>
public interface IAssetFileParserService
{
    /// <summary>
    /// Parses the project.assets.json file for a project.
    /// </summary>
    /// <param name="projectPath">Path to the project file.</param>
    /// <returns>Parsed asset information.</returns>
    Task<ProjectAssets> ParseProjectAssetsAsync(string projectPath);
}