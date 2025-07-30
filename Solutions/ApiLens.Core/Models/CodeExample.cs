namespace ApiLens.Core.Models;

/// <summary>
/// Represents a code example from XML documentation.
/// </summary>
public record CodeExample
{
    /// <summary>
    /// Gets the description or context for the code example.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the actual code content.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the programming language of the example.
    /// </summary>
    public string Language { get; init; } = "csharp";
}