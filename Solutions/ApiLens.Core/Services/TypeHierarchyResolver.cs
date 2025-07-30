using ApiLens.Core.Models;
using ApiLens.Core.Querying;

namespace ApiLens.Core.Services;

public class TypeHierarchyResolver
{
    private readonly IQueryEngine queryEngine;

    public TypeHierarchyResolver(IQueryEngine queryEngine)
    {
        ArgumentNullException.ThrowIfNull(queryEngine);
        this.queryEngine = queryEngine;
    }

    public List<MemberInfo> GetBaseTypeChain(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        List<MemberInfo> baseTypes = [];
        HashSet<string> visitedTypes = [];

        // Start from the type name
        string currentTypeName = typeName;

        while (!string.IsNullOrWhiteSpace(currentTypeName) && !visitedTypes.Contains(currentTypeName))
        {
            visitedTypes.Add(currentTypeName);

            // Look for base types referenced in cross-references or related types
            List<MemberInfo> typeMembers = queryEngine.SearchByName(GetSimpleName(currentTypeName), 10);

            foreach (MemberInfo member in typeMembers)
            {
                if (member.MemberType == MemberType.Type && member.FullName == currentTypeName)
                {
                    // Check if any related types might be base types
                    if (member.RelatedTypes.Length > 0)
                    {
                        // Add the first related type as potential base
                        string potentialBase = member.RelatedTypes[0];
                        if (!visitedTypes.Contains(potentialBase))
                        {
                            baseTypes.Add(new MemberInfo
                            {
                                Id = $"T:{potentialBase}",
                                MemberType = MemberType.Type,
                                Name = GetSimpleName(potentialBase),
                                FullName = potentialBase,
                                Assembly = "Unknown",
                                Namespace = GetNamespace(potentialBase)
                            });
                            currentTypeName = potentialBase;
                            break;
                        }
                    }
                }
            }

            // If we didn't find a base type, stop
            if (baseTypes.Count == visitedTypes.Count - 1)
                break;
        }

        return baseTypes;
    }

    public List<MemberInfo> GetDerivedTypes(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        List<MemberInfo> derivedTypes = [];
        List<MemberInfo> allTypes = queryEngine.GetByType(MemberType.Type, 1000);

        foreach (MemberInfo type in allTypes)
        {
            // Check if this type has the target type in its related types (potential base)
            if (type.RelatedTypes.Contains(typeName))
            {
                derivedTypes.Add(type);
            }
        }

        return derivedTypes;
    }

    public List<MemberInfo> GetRelatedTypes(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        MemberInfo? member = queryEngine.GetById($"T:{typeName}");
        if (member == null)
        {
            // Try searching by name
            List<MemberInfo> results = queryEngine.SearchByName(GetSimpleName(typeName), 10);
            member = results.FirstOrDefault(m => m.FullName == typeName);
        }

        if (member == null)
            return [];

        List<MemberInfo> relatedTypes = [];

        foreach (string relatedTypeName in member.RelatedTypes)
        {
            MemberInfo? relatedMember = queryEngine.GetById($"T:{relatedTypeName}");
            if (relatedMember != null)
            {
                relatedTypes.Add(relatedMember);
            }
            else
            {
                // Create a placeholder
                relatedTypes.Add(new MemberInfo
                {
                    Id = $"T:{relatedTypeName}",
                    MemberType = MemberType.Type,
                    Name = GetSimpleName(relatedTypeName),
                    FullName = relatedTypeName,
                    Assembly = "Unknown",
                    Namespace = GetNamespace(relatedTypeName)
                });
            }
        }

        return relatedTypes;
    }

    private static string GetSimpleName(string fullName)
    {
        int lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
    }

    private static string GetNamespace(string fullName)
    {
        int lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName[..lastDot] : string.Empty;
    }
}