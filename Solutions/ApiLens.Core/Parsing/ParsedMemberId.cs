using ApiLens.Core.Models;

namespace ApiLens.Core.Parsing;

public record ParsedMemberId
{
    public required MemberType MemberType { get; init; }
    public required string Namespace { get; init; }
    public required string TypeName { get; init; }
    public required string MemberName { get; init; }
    public required string FullName { get; init; }
    public ImmutableArray<string> Parameters { get; init; } = [];
    public int GenericArity { get; init; }
    public bool IsNested { get; init; }
    public string? ParentType { get; init; }
    public string? NestedTypeName { get; init; }
}