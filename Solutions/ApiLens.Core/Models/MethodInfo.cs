namespace ApiLens.Core.Models;

public record MethodInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string DeclaringType { get; init; }
    public required string ReturnType { get; init; }
    public required bool IsStatic { get; init; }
    public required bool IsExtension { get; init; }
    public required bool IsAsync { get; init; }
    public required bool IsGeneric { get; init; }
    public required int GenericArity { get; init; }
    public ImmutableArray<ParameterInfo> Parameters { get; init; } = [];
    public ImmutableArray<string> GenericParameters { get; init; } = [];
    public ImmutableArray<string> Exceptions { get; init; } = [];
    public string? Summary { get; init; }
    public string? Returns { get; init; }
    public string? Remarks { get; init; }
}