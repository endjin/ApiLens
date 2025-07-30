namespace ApiLens.Core.Models;

public record TypeInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string Assembly { get; init; }
    public required string Namespace { get; init; }
    public required TypeKind Kind { get; init; }
    public required bool IsGeneric { get; init; }
    public required int GenericArity { get; init; }
    public string? BaseType { get; init; }
    public ImmutableArray<string> Interfaces { get; init; } = [];
    public ImmutableArray<string> GenericParameters { get; init; } = [];
    public ImmutableArray<string> DerivedTypes { get; init; } = [];
}