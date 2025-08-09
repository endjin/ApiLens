using ApiLens.Core.Models;

namespace ApiLens.Core.Services;

/// <summary>
/// Service for parsing .NET project files.
/// </summary>
public interface IProjectParserService
{
    /// <summary>
    /// Parses a project file and extracts package references.
    /// </summary>
    /// <param name="projectPath">Path to the project file.</param>
    /// <returns>Collection of package references.</returns>
    Task<IEnumerable<PackageReference>> GetPackageReferencesAsync(string projectPath);

    /// <summary>
    /// Gets the target frameworks from a project file.
    /// </summary>
    /// <param name="projectPath">Path to the project file.</param>
    /// <returns>Collection of target framework monikers.</returns>
    Task<IEnumerable<string>> GetTargetFrameworksAsync(string projectPath);
}