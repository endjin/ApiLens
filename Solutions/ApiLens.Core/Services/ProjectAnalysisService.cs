using ApiLens.Core.Models;

namespace ApiLens.Core.Services;

/// <summary>
/// Service for analyzing .NET projects and solutions.
/// </summary>
public class ProjectAnalysisService : IProjectAnalysisService
{
    private readonly ISolutionParserService solutionParser;
    private readonly IProjectParserService projectParser;
    private readonly IAssetFileParserService assetParser;
    private readonly IFileSystemService fileSystem;

    public ProjectAnalysisService(
        ISolutionParserService solutionParser,
        IProjectParserService projectParser,
        IAssetFileParserService assetParser,
        IFileSystemService fileSystem)
    {
        ArgumentNullException.ThrowIfNull(solutionParser);
        ArgumentNullException.ThrowIfNull(projectParser);
        ArgumentNullException.ThrowIfNull(assetParser);
        ArgumentNullException.ThrowIfNull(fileSystem);

        this.solutionParser = solutionParser;
        this.projectParser = projectParser;
        this.assetParser = assetParser;
        this.fileSystem = fileSystem;
    }

    public async Task<ProjectAnalysisResult> AnalyzeAsync(string path, bool includeTransitive = false, bool useAssetsFile = false)
    {
        if (!fileSystem.FileExists(path))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        ProjectAnalysisResult result = new()
        {
            Path = path,
            Type = GetProjectType(path)
        };

        List<string> projectPaths = [];

        // Determine if it's a solution or project
        if (result.Type == ProjectType.Solution)
        {
            SolutionInfo solutionInfo = await solutionParser.ParseSolutionAsync(path);
            projectPaths.AddRange(solutionInfo.Projects.Select(p => p.Path));
            result.ProjectPaths = projectPaths;
        }
        else
        {
            projectPaths.Add(path);
            result.ProjectPaths = projectPaths;
        }

        // Collect all package references
        Dictionary<string, PackageReference> allPackages = [];
        HashSet<string> allFrameworks = [];

        foreach (string projectPath in projectPaths)
        {
            try
            {
                // Get package references from project file
                IEnumerable<PackageReference> packageRefs = await projectParser.GetPackageReferencesAsync(projectPath);

                // Get target frameworks
                IEnumerable<string> frameworks = await projectParser.GetTargetFrameworksAsync(projectPath);
                foreach (string framework in frameworks)
                {
                    allFrameworks.Add(framework);
                }

                // Optionally merge with assets file information
                if (useAssetsFile)
                {
                    ProjectAssets assets = await assetParser.ParseProjectAssetsAsync(projectPath);
                    if (assets.HasAssets)
                    {
                        packageRefs = MergeWithAssetInfo(packageRefs, assets, includeTransitive);

                        // Add frameworks from assets file
                        foreach (string framework in assets.TargetFrameworks)
                        {
                            allFrameworks.Add(framework);
                        }
                    }
                    else
                    {
                        result.Warnings.Add($"No project.assets.json found for {Path.GetFileName(projectPath)}");
                    }
                }

                // Add to collection
                foreach (PackageReference pkgRef in packageRefs)
                {
                    string key = $"{pkgRef.Id}:{pkgRef.Version}";
                    if (!allPackages.ContainsKey(key))
                    {
                        allPackages[key] = pkgRef;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Error analyzing {Path.GetFileName(projectPath)}: {ex.Message}");
            }
        }

        result.Packages = [.. allPackages.Values];
        result.Frameworks = [.. allFrameworks];

        // Add statistics
        result.Statistics["TotalProjects"] = projectPaths.Count;
        result.Statistics["TotalPackages"] = allPackages.Count;
        result.Statistics["DirectPackages"] = allPackages.Values.Count(p => !p.IsTransitive);
        result.Statistics["TransitivePackages"] = allPackages.Values.Count(p => p.IsTransitive);
        result.Statistics["TotalFrameworks"] = allFrameworks.Count;

        return result;
    }

    public bool IsProjectOrSolution(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".sln" => true,
            ".csproj" => true,
            ".fsproj" => true,
            ".vbproj" => true,
            _ => false
        };
    }

    public ProjectType GetProjectType(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".sln" => ProjectType.Solution,
            ".csproj" => ProjectType.CsProj,
            ".fsproj" => ProjectType.FsProj,
            ".vbproj" => ProjectType.VbProj,
            _ => throw new ArgumentException($"Unsupported file type: {extension}")
        };
    }

    private IEnumerable<PackageReference> MergeWithAssetInfo(IEnumerable<PackageReference> packageRefs, ProjectAssets assets, bool includeTransitive)
    {
        List<PackageReference> refsList = [.. packageRefs];

        // Update versions from assets file
        foreach (PackageReference? pkgRef in refsList)
        {
            ResolvedPackageInfo? assetPackage = assets.Packages.FirstOrDefault(p =>
                string.Equals(p.Id, pkgRef.Id, StringComparison.OrdinalIgnoreCase));

            if (assetPackage != null)
            {
                // Use resolved version from assets file if available
                if (string.IsNullOrEmpty(pkgRef.Version))
                {
                    pkgRef.Version = assetPackage.Version;
                }
            }
        }

        // Add transitive dependencies if requested
        if (includeTransitive)
        {
            HashSet<string> directPackageIds = new(
                refsList.Select(p => p.Id),
                StringComparer.OrdinalIgnoreCase);

            foreach (ResolvedPackageInfo assetPackage in assets.Packages)
            {
                if (!directPackageIds.Contains(assetPackage.Id))
                {
                    refsList.Add(new PackageReference
                    {
                        Id = assetPackage.Id,
                        Version = assetPackage.Version,
                        IsTransitive = true
                    });
                }
            }
        }

        return refsList;
    }
}