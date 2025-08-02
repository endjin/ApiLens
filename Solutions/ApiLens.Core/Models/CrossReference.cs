namespace ApiLens.Core.Models;

public record CrossReference
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public required ReferenceType Type { get; init; }
    public required string Context { get; init; }
}