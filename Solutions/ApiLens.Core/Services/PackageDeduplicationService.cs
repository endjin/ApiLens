using System.Collections.Concurrent;
using ApiLens.Core.Models;

namespace ApiLens.Core.Services;

/// <summary>
/// High-performance package deduplication service with single-pass algorithm.
/// </summary>
public class PackageDeduplicationService : IPackageDeduplicationService
{
    private readonly ConcurrentDictionary<string, string> pathNormalizationCache;

    public PackageDeduplicationService()
    {
        pathNormalizationCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public PackageDeduplicationResult DeduplicatePackages(
        IReadOnlyList<NuGetPackageInfo> scannedPackages,
        IReadOnlyDictionary<string, HashSet<(string Version, string Framework)>> existingPackages,
        IReadOnlySet<string> indexedXmlPaths,
        IReadOnlySet<string> emptyXmlPaths,
        bool latestOnly)
    {
        // Pre-normalize all paths for efficient lookup
        HashSet<string> normalizedIndexedPaths = new(indexedXmlPaths.Count, StringComparer.OrdinalIgnoreCase);
        foreach (string path in indexedXmlPaths)
        {
            normalizedIndexedPaths.Add(NormalizePath(path));
        }

        HashSet<string> normalizedEmptyPaths = new(emptyXmlPaths.Count, StringComparer.OrdinalIgnoreCase);
        foreach (string path in emptyXmlPaths)
        {
            normalizedEmptyPaths.Add(NormalizePath(path));
        }

        // Single-pass processing with efficient data structures
        Dictionary<string, List<PackageData>> packagesByXmlPath = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> packagesToIndex = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> packageIdsToDelete = new(StringComparer.OrdinalIgnoreCase);

        // Stats tracking
        int emptyFilesSkipped = 0;
        int alreadyIndexedSkipped = 0;
        int newPackages = 0;
        int updatedPackages = 0;

        // Single pass through all packages
        foreach (NuGetPackageInfo package in scannedPackages)
        {
            string normalizedPath = NormalizePath(package.XmlDocumentationPath);

            // Skip empty files
            if (normalizedEmptyPaths.Contains(normalizedPath))
            {
                emptyFilesSkipped++;
                continue;
            }

            // Skip already indexed files
            if (normalizedIndexedPaths.Contains(normalizedPath))
            {
                alreadyIndexedSkipped++;
                continue;
            }

            // Track package data for deduplication
            if (!packagesByXmlPath.TryGetValue(normalizedPath, out List<PackageData>? packages))
            {
                packages = [];
                packagesByXmlPath[normalizedPath] = packages;
            }

            PackageData packageData = new(package, normalizedPath);
            packages.Add(packageData);

            // Check if this is a new or updated package
            bool isNew = !existingPackages.TryGetValue(package.PackageId, out HashSet<(string Version, string Framework)>? existingVersions);
            bool needsIndexing = isNew || (existingVersions != null && !existingVersions.Any(v =>
                v.Version == package.Version &&
                (v.Framework == package.TargetFramework ||
                 (v.Framework == "unknown" && !existingVersions.Any(iv =>
                    iv.Version == package.Version &&
                    iv.Framework != "unknown")))));

            if (needsIndexing)
            {
                packagesToIndex.Add(normalizedPath);
                if (isNew)
                    newPackages++;
                else
                    updatedPackages++;

                // Track for deletion if using latest-only mode
                if (latestOnly && existingVersions != null)
                {
                    // Check if this is newer than all existing versions
                    if (IsNewerThanAll(package.Version, existingVersions.Select(v => v.Version)))
                    {
                        packageIdsToDelete.Add(package.PackageId);
                    }
                }
            }
        }

        // Select packages to index (one per unique XML file)
        List<NuGetPackageInfo> finalPackagesToIndex = new(packagesToIndex.Count);
        foreach (string xmlPath in packagesToIndex)
        {
            if (packagesByXmlPath.TryGetValue(xmlPath, out List<PackageData>? packages) && packages.Count > 0)
            {
                // Use the first package for each unique XML file
                finalPackagesToIndex.Add(packages[0].Package);
            }
        }

        int skippedPackages = scannedPackages.Count - finalPackagesToIndex.Count;

        return new PackageDeduplicationResult
        {
            PackagesToIndex = finalPackagesToIndex,
            PackageIdsToDelete = packageIdsToDelete,
            SkippedPackages = skippedPackages,
            Stats = new DeduplicationStats
            {
                TotalScannedPackages = scannedPackages.Count,
                UniqueXmlFiles = packagesByXmlPath.Count,
                EmptyXmlFilesSkipped = emptyFilesSkipped,
                AlreadyIndexedSkipped = alreadyIndexedSkipped,
                NewPackages = newPackages,
                UpdatedPackages = updatedPackages
            }
        };
    }

    private string NormalizePath(string path)
    {
        return pathNormalizationCache.GetOrAdd(path, p =>
        {
            try
            {
                return Path.GetFullPath(p).Replace('\\', '/');
            }
            catch (ArgumentException)
            {
                return p.Replace('\\', '/');
            }
            catch (NotSupportedException)
            {
                return p.Replace('\\', '/');
            }
        });
    }

    private static bool IsNewerThanAll(string newVersion, IEnumerable<string> existingVersions)
    {
        if (!Version.TryParse(newVersion, out Version? newVer))
            return false;

        foreach (string existingVersion in existingVersions)
        {
            if (Version.TryParse(existingVersion, out Version? existVer) && newVer <= existVer)
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct PackageData(NuGetPackageInfo Package, string NormalizedPath);
}