using System.Text.RegularExpressions;
using ApiLens.Core.Models;

namespace ApiLens.Core.Services;

/// <summary>
/// Scanner for discovering and processing NuGet packages in the local cache.
/// </summary>
public partial class NuGetCacheScanner : INuGetCacheScanner
{
    private readonly IFileSystemService fileSystem;

    // Regex to parse NuGet cache paths
    // Pattern: .../packageid/version/lib|ref/framework/*.xml
    [GeneratedRegex(@"[\\/](?<packageId>[^\\/]+)[\\/](?<version>[^\\/]+)[\\/](?:lib|ref)[\\/](?<framework>[^\\/]+)[\\/][^\\/]+\.xml$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex NuGetPathRegex();

    public NuGetCacheScanner(IFileSystemService fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        this.fileSystem = fileSystem;
    }

    public ImmutableArray<NuGetPackageInfo> ScanNuGetCache()
    {
        string cachePath = fileSystem.GetUserNuGetCachePath();
        return ScanDirectory(cachePath);
    }

    public ImmutableArray<NuGetPackageInfo> ScanDirectory(string cachePath)
    {
        if (!fileSystem.DirectoryExists(cachePath))
        {
            return ImmutableArray<NuGetPackageInfo>.Empty;
        }

        List<NuGetPackageInfo> packages = [];

        foreach (FileInfo xmlFile in fileSystem.EnumerateFiles(cachePath, "*.xml", recursive: true))
        {
            NuGetPackageInfo? packageInfo = ParsePackageInfo(xmlFile.FullName);
            if (packageInfo != null)
            {
                packages.Add(packageInfo);
            }
        }

        return [.. packages];
    }

    public ImmutableArray<NuGetPackageInfo> GetLatestVersions(ImmutableArray<NuGetPackageInfo> packages)
    {
        // Group by package ID and target framework, then select the latest version
        IOrderedEnumerable<NuGetPackageInfo> latestVersions = packages
            .GroupBy(p => new { p.PackageId, p.TargetFramework })
            .Select(g => g.OrderByDescending(p => ParseVersion(p.Version)).First())
            .OrderBy(p => p.PackageId)
            .ThenBy(p => p.TargetFramework);

        return [.. latestVersions];
    }

    private static NuGetPackageInfo? ParsePackageInfo(string xmlPath)
    {
        Match match = NuGetPathRegex().Match(xmlPath);
        if (!match.Success)
        {
            return null;
        }

        return new NuGetPackageInfo
        {
            PackageId = match.Groups["packageId"].Value.ToLowerInvariant(),
            Version = match.Groups["version"].Value,
            TargetFramework = match.Groups["framework"].Value.ToLowerInvariant(),
            XmlDocumentationPath = xmlPath
        };
    }

    private static Version ParseVersion(string versionString)
    {
        // Handle semantic versioning by removing pre-release and build metadata
        int dashIndex = versionString.IndexOf('-');
        if (dashIndex > 0)
        {
            versionString = versionString[..dashIndex];
        }

        // Try to parse as System.Version
        if (Version.TryParse(versionString, out Version? version))
        {
            return version;
        }

        // FIXED: Avoid Array.Resize allocations by pre-allocating array
        string[] parts = versionString.Split('.');

        // Create a properly sized array and copy parts
        string[] paddedParts = new string[4];
        for (int i = 0; i < 4; i++)
        {
            paddedParts[i] = i < parts.Length ? parts[i] : "0";
        }

        string paddedVersion = string.Join(".", paddedParts);
        return Version.TryParse(paddedVersion, out version) ? version : new Version(0, 0, 0, 0);
    }

    // Async methods (delegate to sync versions for backward compatibility)
    public Task<ImmutableArray<NuGetPackageInfo>> ScanNuGetCacheAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ScanNuGetCache());
    }

    public Task<ImmutableArray<NuGetPackageInfo>> ScanDirectoryAsync(
        string cachePath,
        CancellationToken cancellationToken = default,
        IProgress<int>? progress = null)
    {
        return Task.FromResult(ScanDirectory(cachePath));
    }
}