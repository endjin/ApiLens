using System.Text.RegularExpressions;

namespace ApiLens.Core.Helpers;

/// <summary>
/// Helper methods for working with NuGet package paths and metadata.
/// </summary>
public static partial class NuGetHelper
{
    [GeneratedRegex(
        @"[\\/]packages[\\/](?<packageId>[^\\/]+)[\\/](?<version>[^\\/]+)[\\/](?:lib|ref)[\\/](?<framework>[^\\/]+)[\\/][^\\/]+\.xml$",
        RegexOptions.IgnoreCase)]
    private static partial Regex NuGetPathRegex();

    /// <summary>
    /// Extracts NuGet package information from a file path if it matches the NuGet cache structure.
    /// </summary>
    /// <param name="filePath">The file path to analyze.</param>
    /// <returns>A tuple containing PackageId, Version, and Framework if the path is from a NuGet package; otherwise, null.</returns>
    public static (string PackageId, string Version, string Framework)? ExtractNuGetInfo(string filePath)
    {
        if (filePath is null)
        {
            return null;
        }

        Match match = NuGetPathRegex().Match(filePath);

        if (match.Success)
        {
            return (
                PackageId: match.Groups["packageId"].Value.ToLowerInvariant(),
                Version: match.Groups["version"].Value,
                Framework: match.Groups["framework"].Value
            );
        }

        return null;
    }

    /// <summary>
    /// Determines if a file path is from a NuGet package cache.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if the path appears to be from a NuGet cache; otherwise, false.</returns>
    public static bool IsNuGetPath(string filePath)
    {
        if (filePath is null)
        {
            return false;
        }

        return NuGetPathRegex().IsMatch(filePath);
    }
}