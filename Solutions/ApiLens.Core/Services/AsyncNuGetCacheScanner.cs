using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ApiLens.Core.Models;

namespace ApiLens.Core.Services;

/// <summary>
/// High-performance async scanner for discovering and processing NuGet packages in the local cache.
/// </summary>
public partial class AsyncNuGetCacheScanner : INuGetCacheScanner
{
    private readonly IFileSystemService fileSystem;
    private readonly IAsyncFileEnumerator asyncFileEnumerator;
    private readonly ConcurrentDictionary<string, Version> versionCache;

    // Regex to parse NuGet cache paths
    // Pattern: .../packageid/version/lib|ref/framework/*.xml
    [GeneratedRegex(@"[\\/](?<packageId>[^\\/]+)[\\/](?<version>[^\\/]+)[\\/](?:lib|ref)[\\/](?<framework>[^\\/]+)[\\/][^\\/]+\.xml$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex NuGetPathRegex();

    public AsyncNuGetCacheScanner(IFileSystemService fileSystem, IAsyncFileEnumerator asyncFileEnumerator)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(asyncFileEnumerator);
        this.fileSystem = fileSystem;
        this.asyncFileEnumerator = asyncFileEnumerator;
        this.versionCache = new ConcurrentDictionary<string, Version>(StringComparer.OrdinalIgnoreCase);
    }

    // Synchronous methods (for backward compatibility)
    public ImmutableArray<NuGetPackageInfo> ScanNuGetCache()
    {
        return ScanNuGetCacheAsync().GetAwaiter().GetResult();
    }

    public ImmutableArray<NuGetPackageInfo> ScanDirectory(string cachePath)
    {
        return ScanDirectoryAsync(cachePath).GetAwaiter().GetResult();
    }

    // Async methods (optimized)
    public async Task<ImmutableArray<NuGetPackageInfo>> ScanNuGetCacheAsync(CancellationToken cancellationToken = default)
    {
        string cachePath = fileSystem.GetUserNuGetCachePath();
        return await ScanDirectoryAsync(cachePath, cancellationToken);
    }

    public async Task<ImmutableArray<NuGetPackageInfo>> ScanDirectoryAsync(
        string cachePath, 
        CancellationToken cancellationToken = default,
        IProgress<int>? progress = null)
    {
        if (!fileSystem.DirectoryExists(cachePath))
        {
            return ImmutableArray<NuGetPackageInfo>.Empty;
        }

        ConcurrentBag<NuGetPackageInfo> packages = [];
        int filesProcessed = 0;

        // Process files in batches for better performance
        await foreach (IReadOnlyList<FileInfo> batch in asyncFileEnumerator.EnumerateFilesBatchedAsync(
            cachePath, "*.xml", batchSize: 100, recursive: true, maxConcurrency: null, cancellationToken))
        {
            // Process batch in parallel
            await Parallel.ForEachAsync(batch, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, async (xmlFile, ct) =>
            {
                NuGetPackageInfo? packageInfo = ParsePackageInfo(xmlFile.FullName);
                if (packageInfo != null)
                {
                    packages.Add(packageInfo);
                }

                int currentCount = Interlocked.Increment(ref filesProcessed);
                if (currentCount % 100 == 0)
                {
                    progress?.Report(currentCount);
                }

                await Task.CompletedTask; // Ensure async
            });
        }

        return [.. packages.OrderBy(p => p.PackageId).ThenBy(p => p.Version)];
    }

    public ImmutableArray<NuGetPackageInfo> GetLatestVersions(ImmutableArray<NuGetPackageInfo> packages)
    {
        // Group by package ID and target framework, then select the latest version
        IOrderedEnumerable<NuGetPackageInfo> latestVersions = packages
            .GroupBy(p => new { p.PackageId, p.TargetFramework })
            .Select(g => g.OrderByDescending(p => GetCachedVersion(p.Version)).First())
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

    private Version GetCachedVersion(string versionString)
    {
        return versionCache.GetOrAdd(versionString, ParseVersion);
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

        // Use stack allocation for better performance
        Span<int> parts = stackalloc int[4];
        int partCount = 0;
        
        // Parse version parts
        ReadOnlySpan<char> versionSpan = versionString.AsSpan();
        int start = 0;
        
        for (int i = 0; i <= versionSpan.Length && partCount < 4; i++)
        {
            if (i == versionSpan.Length || versionSpan[i] == '.')
            {
                if (i > start)
                {
                    ReadOnlySpan<char> partSpan = versionSpan.Slice(start, i - start);
                    if (int.TryParse(partSpan, out int partValue))
                    {
                        parts[partCount++] = partValue;
                    }
                    else
                    {
                        parts[partCount++] = 0;
                    }
                }
                start = i + 1;
            }
        }

        // Fill remaining parts with 0
        while (partCount < 4)
        {
            parts[partCount++] = 0;
        }

        return new Version(parts[0], parts[1], parts[2], parts[3]);
    }
}