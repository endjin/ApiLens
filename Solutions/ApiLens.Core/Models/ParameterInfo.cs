namespace ApiLens.Core.Models;

public record ParameterInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required int Position { get; init; }
    public required bool IsOptional { get; init; }
    public required bool IsParams { get; init; }
    public required bool IsOut { get; init; }
    public required bool IsRef { get; init; }
    public string? DefaultValue { get; init; }
    public string? Description { get; init; }
}