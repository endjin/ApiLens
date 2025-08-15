using System.Text.RegularExpressions;

namespace ApiLens.Cli.Services;

/// <summary>
/// Intelligent comparer for .NET framework versions that handles current and future versions.
/// </summary>
public class FrameworkVersionComparer : IComparer<string?>
{
    private static readonly Regex FrameworkPattern = new(
        @"^(net(?:standard|coreapp)?|net)(\d+)(?:\.(\d+))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x == null) return 1;
        if (y == null) return -1;
        
        var xInfo = ParseFramework(x);
        var yInfo = ParseFramework(y);
        
        // First compare by framework type priority (higher priority = better)
        var priorityComparison = yInfo.Priority.CompareTo(xInfo.Priority);
        if (priorityComparison != 0) return priorityComparison;
        
        // Then by version (higher version = better)
        var versionComparison = yInfo.Version.CompareTo(xInfo.Version);
        if (versionComparison != 0) return versionComparison;
        
        // Finally by string comparison as fallback
        return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }
    
    private (int Priority, Version Version) ParseFramework(string framework)
    {
        if (string.IsNullOrEmpty(framework))
            return (0, new Version(0, 0));
        
        var match = FrameworkPattern.Match(framework);
        if (!match.Success)
            return (0, new Version(0, 0));
        
        var frameworkBase = match.Groups[1].Value.ToLowerInvariant();
        var majorStr = match.Groups[2].Value;
        var minorStr = match.Groups[3].Value;
        
        // Determine priority based on framework type
        int priority = GetFrameworkPriority(frameworkBase, majorStr);
        
        // Parse version
        if (!int.TryParse(majorStr, out int major)) major = 0;
        if (!int.TryParse(minorStr, out int minor)) minor = 0;
        
        // Special handling for .NET Framework 4.x versions like net462
        if (frameworkBase == "net" && majorStr.Length == 3)
        {
            // net462 -> 4.6.2
            major = int.Parse(majorStr.Substring(0, 1));
            minor = int.Parse(majorStr.Substring(1, 1));
            // Patch version could be added from 3rd digit if needed
        }
        
        return (priority, new Version(major, minor));
    }
    
    private static int GetFrameworkPriority(string frameworkBase, string majorStr)
    {
        return frameworkBase switch
        {
            "netstandard" => 1000, // Lowest priority - most compatible but least features
            "netcoreapp" => 3000,  // .NET Core
            "net" when IsModernDotNet(majorStr) => 4000, // Modern .NET (5+)
            "net" => 2000, // .NET Framework
            _ => 0
        };
    }
    
    private static bool IsModernDotNet(string majorStr)
    {
        // Modern .NET uses single or double digit versions (5, 6, 7, 8, 9, 10, 11...)
        // .NET Framework uses 3-digit versions (462, 472, 481...) or explicit 4.x
        if (majorStr.Length >= 3) return false;
        
        if (int.TryParse(majorStr, out int major))
        {
            // .NET 5 and above are modern .NET
            return major >= 5;
        }
        
        return false;
    }
    
    /// <summary>
    /// Gets a display-friendly framework name.
    /// </summary>
    public static string GetDisplayName(string framework)
    {
        if (string.IsNullOrEmpty(framework)) return "Unknown";
        
        var match = FrameworkPattern.Match(framework);
        if (!match.Success) return framework;
        
        var frameworkBase = match.Groups[1].Value.ToLowerInvariant();
        var majorStr = match.Groups[2].Value;
        var minorStr = match.Groups[3].Value;
        
        return frameworkBase switch
        {
            "netstandard" => $".NET Standard {majorStr}.{minorStr}",
            "netcoreapp" => $".NET Core {majorStr}.{minorStr}",
            "net" when IsModernDotNet(majorStr) => $".NET {majorStr}" + (string.IsNullOrEmpty(minorStr) ? "" : $".{minorStr}"),
            "net" when majorStr.Length == 3 => $".NET Framework {majorStr[0]}.{majorStr[1]}.{majorStr[2]}",
            _ => framework
        };
    }
}