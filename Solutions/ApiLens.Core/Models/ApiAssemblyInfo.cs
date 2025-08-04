namespace ApiLens.Core.Models;

public record ApiAssemblyInfo
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Culture { get; init; }
    public string? PublicKeyToken { get; init; }
    public string? Description { get; init; }
    public ImmutableArray<string> Types { get; init; } = [];
    public ImmutableArray<string> Namespaces { get; init; } = [];
}