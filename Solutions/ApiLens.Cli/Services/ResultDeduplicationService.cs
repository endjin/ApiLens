using ApiLens.Core.Models;

namespace ApiLens.Cli.Services;

/// <summary>
/// Service for deduplicating search results by combining multiple framework versions into a single result.
/// </summary>
public class ResultDeduplicationService
{
    private readonly FrameworkVersionComparer comparer = new();
    
    /// <summary>
    /// Deduplicates results by combining multiple framework versions of the same member into a single result.
    /// </summary>
    public List<MemberInfo> DeduplicateResults(List<MemberInfo> results, bool showDistinct)
    {
        if (!showDistinct || results.Count == 0)
            return results;
        
        // Group by unique identifier (without framework-specific parts)
        var groups = results.GroupBy(m => GetBaseIdentifier(m));
        
        var deduplicated = new List<MemberInfo>();
        
        foreach (var group in groups)
        {
            // Select the best version based on framework priority
            var selected = SelectBestVersion(group);
            
            // Aggregate all framework versions into a single display string
            var frameworks = group
                .Select(m => m.TargetFramework)
                .Where(tf => !string.IsNullOrEmpty(tf))
                .Distinct()
                .OrderBy(tf => tf, comparer)
                .ToList();
            
            // Create a combined framework string
            string combinedFramework = FormatFrameworkList(frameworks);
            
            // Update the selected item with the combined framework information
            selected = selected with 
            { 
                TargetFramework = combinedFramework
            };
            
            deduplicated.Add(selected);
        }
        
        return deduplicated;
    }
    
    /// <summary>
    /// Gets a unique identifier for a member that doesn't include framework-specific information.
    /// </summary>
    private string GetBaseIdentifier(MemberInfo member)
    {
        // Create a composite key from the essential properties
        // This ensures we group the same member across different frameworks
        return $"{member.MemberType}|{member.FullName}|{member.Assembly}|{member.PackageId}";
    }
    
    /// <summary>
    /// Selects the best version from a group of duplicate members based on framework priority.
    /// </summary>
    private MemberInfo SelectBestVersion(IGrouping<string, MemberInfo> group)
    {
        // Sort by framework version using our intelligent comparer
        // The comparer returns items in descending order of preference
        var sorted = group
            .Where(m => !string.IsNullOrEmpty(m.TargetFramework))
            .OrderBy(m => m.TargetFramework, comparer)
            .ToList();
        
        // If we have sorted results, return the best one
        if (sorted.Count > 0)
            return sorted[0];
        
        // Fallback to the first item if no framework info is available
        return group.First();
    }
    
    /// <summary>
    /// Formats a list of framework versions for display.
    /// </summary>
    private string FormatFrameworkList(List<string?> frameworks)
    {
        // Filter out null values
        var nonNullFrameworks = frameworks.Where(f => f != null).ToList();
        
        if (nonNullFrameworks.Count == 0)
            return string.Empty;
        
        if (nonNullFrameworks.Count == 1)
            return nonNullFrameworks[0] ?? string.Empty;
        
        // For multiple frameworks, show the best one first with others in brackets
        // Example: "net8.0 [+4 others]" or "net8.0 [net6.0, netstandard2.1]"
        if (nonNullFrameworks.Count <= 3)
        {
            // Show all frameworks if there are 3 or fewer
            return string.Join(", ", nonNullFrameworks);
        }
        else
        {
            // Show the best framework and count of others
            return $"{nonNullFrameworks[0]} [+{nonNullFrameworks.Count - 1} others]";
        }
    }
    
    /// <summary>
    /// Gets statistics about deduplication effectiveness.
    /// </summary>
    public DeduplicationStats GetStatistics(List<MemberInfo> original, List<MemberInfo> deduplicated)
    {
        return new DeduplicationStats
        {
            OriginalCount = original.Count,
            DeduplicatedCount = deduplicated.Count,
            ReductionPercentage = original.Count > 0 
                ? (1.0 - (double)deduplicated.Count / original.Count) * 100 
                : 0,
            AverageFrameworksPerMember = original.Count > 0 && deduplicated.Count > 0
                ? (double)original.Count / deduplicated.Count
                : 1
        };
    }
}

/// <summary>
/// Statistics about the deduplication process.
/// </summary>
public class DeduplicationStats
{
    public int OriginalCount { get; init; }
    public int DeduplicatedCount { get; init; }
    public double ReductionPercentage { get; init; }
    public double AverageFrameworksPerMember { get; init; }
}