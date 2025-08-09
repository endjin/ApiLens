using ApiLens.Core.Models;
using NuGet.ProjectModel;

namespace ApiLens.Core.Services;

/// <summary>
/// Service for parsing project.assets.json files using NuGet.ProjectModel.
/// </summary>
public class AssetFileParserService : IAssetFileParserService
{
    private readonly IFileSystemService fileSystem;

    public AssetFileParserService(IFileSystemService fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        this.fileSystem = fileSystem;
    }

    public Task<ProjectAssets> ParseProjectAssetsAsync(string projectPath)
    {
        string? assetsPath = FindAssetsFile(projectPath);

        if (string.IsNullOrEmpty(assetsPath) || !fileSystem.FileExists(assetsPath))
        {
            return Task.FromResult(new ProjectAssets { HasAssets = false });
        }

        try
        {
            // Use NuGet.ProjectModel to parse the assets file
            LockFileFormat lockFileFormat = new();
            LockFile lockFile;

            using (Stream stream = fileSystem.OpenRead(assetsPath))
            {
                lockFile = lockFileFormat.Read(stream, assetsPath);
            }

            if (lockFile == null)
            {
                return Task.FromResult(new ProjectAssets { HasAssets = false });
            }

            List<ResolvedPackageInfo> packages = [];

            foreach (LockFileLibrary? library in lockFile.Libraries)
            {
                ResolvedPackageInfo packageInfo = new()
                {
                    Id = library.Name,
                    Version = library.Version?.ToString() ?? string.Empty,
                    Type = library.Type,
                    Hash = library.Sha512,
                    Files = library.Files?.ToList() ?? []
                };

                // Get dependencies from the first target that has this library
                LockFileTargetLibrary? targetLibrary = lockFile.Targets?
                    .SelectMany(t => t.Libraries)
                    .FirstOrDefault(l => l.Name == library.Name && l.Version == library.Version);

                if (targetLibrary?.Dependencies != null)
                {
                    packageInfo.Dependencies = [.. targetLibrary.Dependencies
                        .Select(d => new PackageDependency
                        {
                            Id = d.Id,
                            VersionRange = d.VersionRange?.ToString()
                        })];
                }

                packages.Add(packageInfo);
            }

            List<string> frameworks = lockFile.Targets?
                .Select(t => t.TargetFramework?.GetShortFolderName() ?? string.Empty)
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct()
                .ToList() ?? [];

            return Task.FromResult(new ProjectAssets
            {
                HasAssets = true,
                Packages = packages,
                TargetFrameworks = frameworks
            });
        }
        catch (Exception)
        {
            // If we can't parse the assets file, return empty result
            return Task.FromResult(new ProjectAssets { HasAssets = false });
        }
    }

    private string? FindAssetsFile(string projectPath)
    {
        string? projectDir = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrEmpty(projectDir))
        {
            return null;
        }

        // Try standard obj folder location
        string standardPath = Path.Combine(projectDir, "obj", "project.assets.json");
        if (fileSystem.FileExists(standardPath))
        {
            return standardPath;
        }

        // Try to find in any obj subfolder
        string objDir = Path.Combine(projectDir, "obj");
        if (fileSystem.DirectoryExists(objDir))
        {
            IEnumerable<string> files = fileSystem.GetFiles(objDir, "project.assets.json", false);
            return files.FirstOrDefault();
        }

        return null;
    }
}