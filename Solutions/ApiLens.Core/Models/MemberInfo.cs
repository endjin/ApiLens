namespace ApiLens.Core.Models;

public record MemberInfo
{
    public required string Id { get; init; }
    public required MemberType MemberType { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string Assembly { get; init; }
    public required string Namespace { get; init; }
    public string? Summary { get; init; }
    public string? Remarks { get; init; }
    public ImmutableArray<CrossReference> CrossReferences { get; init; } = [];
    public ImmutableArray<string> RelatedTypes { get; init; } = [];

    // New metadata fields
    public ImmutableArray<CodeExample> CodeExamples { get; init; } = [];
    public ImmutableArray<ExceptionInfo> Exceptions { get; init; } = [];
    public ImmutableArray<AttributeInfo> Attributes { get; init; } = [];
    public ComplexityMetrics? Complexity { get; init; }
    public ImmutableArray<ParameterInfo> Parameters { get; init; } = [];
    public string? Returns { get; init; }
    public string? ReturnType { get; init; }
    public string? SeeAlso { get; init; }
    
    // Method modifiers (for methods only)
    public bool IsStatic { get; init; }
    public bool IsAsync { get; init; }
    public bool IsExtension { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsOverride { get; init; }
    public bool IsSealed { get; init; }

    // Version tracking fields (all nullable for backward compatibility)
    public string? PackageId { get; init; }
    public string? PackageVersion { get; init; }
    public string? TargetFramework { get; init; }
    public string? ContentHash { get; init; }
    public DateTime? IndexedAt { get; init; }
    public bool IsFromNuGetCache { get; init; }
    public string? SourceFilePath { get; init; }

    /// <summary>
    /// Calculates a documentation quality score from 0-100 based on completeness.
    /// </summary>
    public int DocumentationScore
    {
        get
        {
            int score = 0;
            int maxScore = 0;

            // Summary is worth 40 points
            maxScore += 40;
            if (!string.IsNullOrWhiteSpace(Summary))
            {
                // Basic summary gets 20 points, detailed (>50 chars) gets full 40
                score += Summary.Length > 50 ? 40 : 20;
            }

            // Remarks worth 20 points
            maxScore += 20;
            if (!string.IsNullOrWhiteSpace(Remarks))
            {
                score += 20;
            }

            // Code examples worth 20 points
            maxScore += 20;
            if (CodeExamples.Any())
            {
                score += 20;
            }

            // For methods, parameter documentation worth 10 points
            if (MemberType == MemberType.Method && Parameters.Any())
            {
                maxScore += 10;
                bool allParamsDocumented = Parameters.All(p => !string.IsNullOrWhiteSpace(p.Description));
                if (allParamsDocumented)
                {
                    score += 10;
                }
            }

            // Returns documentation worth 10 points for methods
            if (MemberType == MemberType.Method)
            {
                maxScore += 10;
                if (!string.IsNullOrWhiteSpace(Returns))
                {
                    score += 10;
                }
            }

            // Calculate percentage
            return maxScore > 0 ? (score * 100) / maxScore : 0;
        }
    }

    /// <summary>
    /// Indicates whether this member has at least basic documentation.
    /// </summary>
    public bool IsDocumented => !string.IsNullOrWhiteSpace(Summary);

    /// <summary>
    /// Indicates whether this member has comprehensive documentation.
    /// </summary>
    public bool IsWellDocumented => DocumentationScore >= 70;
}