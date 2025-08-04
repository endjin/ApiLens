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
    public string? SeeAlso { get; init; }

    // Version tracking fields (all nullable for backward compatibility)
    public string? PackageId { get; init; }
    public string? PackageVersion { get; init; }
    public string? TargetFramework { get; init; }
    public string? ContentHash { get; init; }
    public DateTime? IndexedAt { get; init; }
    public bool IsFromNuGetCache { get; init; }
    public string? SourceFilePath { get; init; }
}