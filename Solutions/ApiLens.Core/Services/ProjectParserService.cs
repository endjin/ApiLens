using System.Xml.Linq;
using ApiLens.Core.Models;

namespace ApiLens.Core.Services;

/// <summary>
/// Service for parsing .NET project files.
/// </summary>
public class ProjectParserService : IProjectParserService
{
    private readonly IFileSystemService fileSystem;

    public ProjectParserService(IFileSystemService fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        this.fileSystem = fileSystem;
    }

    public async Task<IEnumerable<PackageReference>> GetPackageReferencesAsync(string projectPath)
    {
        if (!fileSystem.FileExists(projectPath))
        {
            throw new FileNotFoundException($"Project file not found: {projectPath}");
        }

        List<PackageReference> packageRefs = [];

        // Parse the project file
        string content = await fileSystem.ReadAllTextAsync(projectPath);
        XDocument doc = XDocument.Parse(content);

        // Find PackageReference elements (SDK-style projects)
        foreach (XElement element in doc.Descendants("PackageReference"))
        {
            string? packageId = element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value;
            if (string.IsNullOrEmpty(packageId))
            {
                continue;
            }

            PackageReference package = new()
            {
                Id = packageId,
                Version = element.Attribute("Version")?.Value ?? element.Element("Version")?.Value,
                PrivateAssets = element.Element("PrivateAssets")?.Value ?? element.Attribute("PrivateAssets")?.Value,
                IncludeAssets = element.Element("IncludeAssets")?.Value ?? element.Attribute("IncludeAssets")?.Value,
                ExcludeAssets = element.Element("ExcludeAssets")?.Value ?? element.Attribute("ExcludeAssets")?.Value
            };

            packageRefs.Add(package);
        }

        // Check for packages.config (legacy format)
        string projectDir = fileSystem.GetDirectoryName(projectPath) ?? string.Empty;
        string packagesConfigPath = fileSystem.CombinePath(projectDir, "packages.config");

        if (fileSystem.FileExists(packagesConfigPath))
        {
            packageRefs.AddRange(await ParsePackagesConfigAsync(packagesConfigPath));
        }

        return packageRefs;
    }

    public async Task<IEnumerable<string>> GetTargetFrameworksAsync(string projectPath)
    {
        if (!fileSystem.FileExists(projectPath))
        {
            throw new FileNotFoundException($"Project file not found: {projectPath}");
        }

        List<string> frameworks = [];
        string content = await fileSystem.ReadAllTextAsync(projectPath);
        XDocument doc = XDocument.Parse(content);

        // Check for TargetFramework (single)
        string? targetFramework = doc.Descendants("TargetFramework").FirstOrDefault()?.Value;
        if (!string.IsNullOrEmpty(targetFramework))
        {
            frameworks.Add(targetFramework);
        }

        // Check for TargetFrameworks (multiple)
        string? targetFrameworks = doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value;
        if (!string.IsNullOrEmpty(targetFrameworks))
        {
            frameworks.AddRange(targetFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries));
        }

        // For legacy projects, check TargetFrameworkVersion
        string? targetFrameworkVersion = doc.Descendants("TargetFrameworkVersion").FirstOrDefault()?.Value;
        if (!string.IsNullOrEmpty(targetFrameworkVersion))
        {
            // Convert v4.7.2 to net472
            string version = targetFrameworkVersion.TrimStart('v').Replace(".", "");
            frameworks.Add($"net{version}");
        }

        return frameworks.Distinct();
    }

    private async Task<IEnumerable<PackageReference>> ParsePackagesConfigAsync(string path)
    {
        string content = await fileSystem.ReadAllTextAsync(path);
        XDocument doc = XDocument.Parse(content);

        return doc.Descendants("package")
            .Select(p => new PackageReference
            {
                Id = p.Attribute("id")?.Value ?? string.Empty,
                Version = p.Attribute("version")?.Value,
                TargetFramework = p.Attribute("targetFramework")?.Value
            })
            .Where(p => !string.IsNullOrEmpty(p.Id));
    }
}