namespace ApiLens.Core.Models;

/// <summary>
/// Represents complexity metrics for a member.
/// </summary>
public record ComplexityMetrics
{
    /// <summary>
    /// Gets the number of parameters for a method.
    /// </summary>
    public required int ParameterCount { get; init; }

    /// <summary>
    /// Gets the estimated cyclomatic complexity.
    /// </summary>
    public required int CyclomaticComplexity { get; init; }

    /// <summary>
    /// Gets the number of lines in the documentation.
    /// </summary>
    public required int DocumentationLineCount { get; init; }
}