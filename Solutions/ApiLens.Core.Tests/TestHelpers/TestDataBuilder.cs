using System.Collections.Immutable;
using ApiLens.Core.Models;

namespace ApiLens.Core.Tests.Helpers;

public static class TestDataBuilder
{
    public static ParameterInfo CreateParameter(string name, string type, string? description = null, int position = 0)
    {
        return new ParameterInfo
        {
            Name = name,
            Type = type,
            Description = description,
            Position = position,
            IsOptional = false,
            IsParams = false,
            IsOut = false,
            IsRef = false
        };
    }

    public static CrossReference CreateCrossReference(ReferenceType type, string targetId, string sourceId = "", string context = "")
    {
        return new CrossReference
        {
            Type = type,
            TargetId = targetId,
            SourceId = sourceId,
            Context = context
        };
    }

    public static AttributeInfo CreateAttribute(string type, Dictionary<string, string>? properties = null)
    {
        return new AttributeInfo
        {
            Type = type,
            Properties = properties?.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty
        };
    }
}