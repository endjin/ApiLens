namespace ApiLens.Core.Models;

/// <summary>
/// Represents exception documentation from XML documentation.
/// </summary>
public record ExceptionInfo
{
    /// <summary>
    /// Gets the fully qualified type name of the exception.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the condition under which the exception is thrown.
    /// </summary>
    public string? Condition { get; init; }
}