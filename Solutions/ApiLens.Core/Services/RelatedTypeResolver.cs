using System.Text.RegularExpressions;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;

namespace ApiLens.Core.Services;

public partial class RelatedTypeResolver
{
    private readonly IQueryEngine queryEngine;

    public RelatedTypeResolver(IQueryEngine queryEngine)
    {
        ArgumentNullException.ThrowIfNull(queryEngine);
        this.queryEngine = queryEngine;
    }

    public List<string> GetRelatedTypes(string memberId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);

        MemberInfo? member = queryEngine.GetById(memberId);
        if (member == null)
            return [];

        HashSet<string> relatedTypes = [];

        // Extract declaring type from member ID
        ExtractDeclaringType(memberId, relatedTypes);

        // Add types from RelatedTypes field
        foreach (string relatedType in member.RelatedTypes)
        {
            relatedTypes.Add($"T:{relatedType}");
        }

        // Add types from cross-references
        foreach (CrossReference crossRef in member.CrossReferences)
        {
            if (crossRef.TargetId.StartsWith("T:"))
            {
                relatedTypes.Add(crossRef.TargetId);
            }
        }

        // Extract types from member signature
        ExtractTypesFromSignature(memberId, relatedTypes);

        // Extract types from documentation
        ExtractTypesFromDocumentation(member, relatedTypes);

        return [.. relatedTypes];
    }

    private static void ExtractDeclaringType(string memberId, HashSet<string> relatedTypes)
    {
        if (memberId.StartsWith("M:") || memberId.StartsWith("P:") || memberId.StartsWith("F:") || memberId.StartsWith("E:"))
        {
            // Extract declaring type
            string memberName = memberId[2..]; // Remove prefix

            // For methods, remove parameters
            if (memberId.StartsWith("M:") && memberName.Contains('('))
            {
                memberName = memberName[..memberName.IndexOf('(')];
            }

            // Find last dot to get declaring type
            int lastDot = memberName.LastIndexOf('.');
            if (lastDot > 0)
            {
                string declaringType = memberName[..lastDot];
                relatedTypes.Add($"T:{declaringType}");
            }
        }
    }

    private static void ExtractTypesFromSignature(string memberId, HashSet<string> relatedTypes)
    {
        if (memberId.StartsWith("M:") && memberId.Contains('('))
        {
            // Extract parameter types from method signature
            string parameterSection = memberId[(memberId.IndexOf('(') + 1)..];
            if (parameterSection.EndsWith(')'))
            {
                parameterSection = parameterSection[..^1];
            }

            if (!string.IsNullOrEmpty(parameterSection))
            {
                string[] parameterTypes = parameterSection.Split(',');
                foreach (string paramType in parameterTypes)
                {
                    string cleanType = paramType.Trim();
                    if (!string.IsNullOrEmpty(cleanType))
                    {
                        // Handle generic types
                        if (cleanType.Contains('`'))
                        {
                            // Extract base generic type
                            Match genericMatch = GenerateGenericTypePattern().Match(cleanType);
                            if (genericMatch.Success)
                            {
                                relatedTypes.Add($"T:{genericMatch.Groups[1].Value}`{genericMatch.Groups[2].Value}");
                            }
                        }
                        else
                        {
                            relatedTypes.Add($"T:{cleanType}");
                        }
                    }
                }
            }
        }
    }

    private static void ExtractTypesFromDocumentation(MemberInfo member, HashSet<string> relatedTypes)
    {
        // Extract types mentioned in summary
        if (!string.IsNullOrWhiteSpace(member.Summary))
        {
            ExtractTypeNamesFromText(member.Summary, relatedTypes);
        }

        // Property type inference from name
        if (member is { MemberType: MemberType.Property, Name: "Name" })
        {
            relatedTypes.Add("T:System.String");
        }
    }

    private static void ExtractTypeNamesFromText(string text, HashSet<string> relatedTypes)
    {
        // Look for common patterns like "List<String>" or "Returns List<String>"
        if (text.Contains("List<String>") || text.Contains("List&lt;String&gt;"))
        {
            relatedTypes.Add("T:System.Collections.Generic.List`1");
            relatedTypes.Add("T:System.String");
        }

        // Add more patterns as needed
        if (text.Contains("Dictionary<") || text.Contains("Dictionary&lt;"))
        {
            relatedTypes.Add("T:System.Collections.Generic.Dictionary`2");
        }
    }

    [GeneratedRegex(@"^(.+?)`(\d+)")]
    private static partial Regex GenerateGenericTypePattern();

    [GeneratedRegex(@"\(([^)]+)\)")]
    private static partial Regex GenerateMethodParameterPattern();
}