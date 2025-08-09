using System.Text.RegularExpressions;
using ApiLens.Core.Models;

namespace ApiLens.Core.Services;

/// <summary>
/// Service for parsing .NET solution files.
/// </summary>
public class SolutionParserService : ISolutionParserService
{
    private readonly IFileSystemService fileSystem;
    private static readonly Regex ProjectRegex = new(
        @"Project\(""\{[^}]*\}""\)\s*=\s*""([^""]*)"",\s*""([^""]*\.(cs|fs|vb)proj)""",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public SolutionParserService(IFileSystemService fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        this.fileSystem = fileSystem;
    }

    public async Task<SolutionInfo> ParseSolutionAsync(string solutionPath)
    {
        if (!fileSystem.FileExists(solutionPath))
        {
            throw new FileNotFoundException($"Solution file not found: {solutionPath}");
        }

        string content = await fileSystem.ReadAllTextAsync(solutionPath);
        int projectCount = ProjectRegex.Matches(content).Count;
        List<ProjectReference> projects = new(projectCount);
        string solutionDir = fileSystem.GetDirectoryName(solutionPath) ?? string.Empty;

        foreach (Match match in ProjectRegex.Matches(content))
        {
            string name = match.Groups[1].Value;
            string relativePath = match.Groups[2].Value;
            
            // Combine the solution directory with the relative project path
            string projectPath = fileSystem.CombinePath(solutionDir, relativePath);

            if (fileSystem.FileExists(projectPath))
            {
                projects.Add(new ProjectReference
                {
                    Name = name,
                    Path = projectPath
                });
            }
        }

        return new SolutionInfo
        {
            Path = solutionPath,
            Projects = projects
        };
    }

    public async Task<IEnumerable<string>> GetProjectPathsAsync(string solutionPath)
    {
        SolutionInfo solutionInfo = await ParseSolutionAsync(solutionPath);
        return solutionInfo.Projects.Select(p => p.Path);
    }
}