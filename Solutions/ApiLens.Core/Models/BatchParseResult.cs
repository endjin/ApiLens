namespace ApiLens.Core.Models;

public record BatchParseResult
{
    public required int TotalFiles { get; init; }
    public required int SuccessfulFiles { get; init; }
    public required int FailedFiles { get; init; }
    public required int TotalMembers { get; init; }
    public required TimeSpan ElapsedTime { get; init; }
    public required long BytesProcessed { get; init; }
    public ImmutableArray<MemberInfo> Members { get; init; } = [];
    public ImmutableArray<string> Errors { get; init; } = [];
}