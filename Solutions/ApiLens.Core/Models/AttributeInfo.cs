namespace ApiLens.Core.Models;

/// <summary>
/// Represents attribute information for a member.
/// </summary>
public record AttributeInfo
{
    /// <summary>
    /// Gets the fully qualified type name of the attribute.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the attribute properties and their values.
    /// </summary>
    public ImmutableDictionary<string, string> Properties { get; init; } = ImmutableDictionary<string, string>.Empty;
}