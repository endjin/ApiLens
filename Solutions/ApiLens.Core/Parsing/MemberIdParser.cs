using ApiLens.Core.Models;

namespace ApiLens.Core.Parsing;

public class MemberIdParser
{
    public static ParsedMemberId Parse(string memberId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);

        if (memberId.Length < 2 || memberId[1] != ':')
        {
            throw new ArgumentException($"Invalid member ID format: {memberId}", nameof(memberId));
        }

        MemberType memberType = memberId[0] switch
        {
            'T' => MemberType.Type,
            'M' => MemberType.Method,
            'P' => MemberType.Property,
            'F' => MemberType.Field,
            'E' => MemberType.Event,
            _ => throw new ArgumentException($"Unknown member type prefix: {memberId[0]}", nameof(memberId))
        };

        string id = memberId[2..];

        if (memberType == MemberType.Type)
        {
            return ParseType(id);
        }

        return ParseMember(id, memberType);
    }

    private static ParsedMemberId ParseType(string id)
    {
        int lastDot = id.LastIndexOf('.');
        string namespaceName = lastDot >= 0 ? id[..lastDot] : string.Empty;
        string typeName = lastDot >= 0 ? id[(lastDot + 1)..] : id;

        // Check for nested types (indicated by +)
        bool isNested = typeName.Contains('+');
        string? parentType = null;
        string? nestedTypeName = null;

        if (isNested)
        {
            int plusIndex = typeName.IndexOf('+');
            parentType = typeName[..plusIndex];
            nestedTypeName = typeName[(plusIndex + 1)..];
        }

        // Extract generic arity from type name (e.g., List`1)
        int genericArity = 0;
        int backtickIndex = typeName.IndexOf('`');
        if (backtickIndex >= 0 && int.TryParse(typeName[(backtickIndex + 1)..], out int arity))
        {
            genericArity = arity;
        }

        return new ParsedMemberId
        {
            MemberType = MemberType.Type,
            Namespace = namespaceName,
            TypeName = typeName,
            MemberName = typeName,
            FullName = id,
            Parameters = [],
            GenericArity = genericArity,
            IsNested = isNested,
            ParentType = parentType,
            NestedTypeName = nestedTypeName
        };
    }

    private static ParsedMemberId ParseMember(string id, MemberType memberType)
    {
        // For methods, properties, fields, events - extract type and member info
        int parenIndex = id.IndexOf('(');
        string idWithoutParams = parenIndex >= 0 ? id[..parenIndex] : id;

        int lastDotIndex = idWithoutParams.LastIndexOf('.');

        if (lastDotIndex < 0)
        {
            throw new ArgumentException($"Invalid member ID format: {id}");
        }

        string fullTypeName = idWithoutParams[..lastDotIndex];
        string memberName = idWithoutParams[(lastDotIndex + 1)..];

        // Extract namespace and type name
        int typeLastDot = fullTypeName.LastIndexOf('.');
        string namespaceName = typeLastDot >= 0 ? fullTypeName[..typeLastDot] : string.Empty;
        string typeName = typeLastDot >= 0 ? fullTypeName[(typeLastDot + 1)..] : fullTypeName;

        // Extract parameters for methods
        ImmutableArray<string> parameters = [];
        if (memberType == MemberType.Method && parenIndex >= 0)
        {
            string paramString = id[(parenIndex + 1)..^1]; // Remove parentheses
            if (!string.IsNullOrEmpty(paramString))
            {
                parameters = SplitParameters(paramString);
            }
        }

        return new ParsedMemberId
        {
            MemberType = memberType,
            Namespace = namespaceName,
            TypeName = typeName,
            MemberName = memberName,
            FullName = id,
            Parameters = parameters,
            GenericArity = 0,
            IsNested = false,
            ParentType = null,
            NestedTypeName = null
        };
    }

    private static ImmutableArray<string> SplitParameters(string paramString)
    {
        ImmutableArray<string>.Builder parameters = ImmutableArray.CreateBuilder<string>();
        int braceDepth = 0;
        int start = 0;

        for (int i = 0; i < paramString.Length; i++)
        {
            char c = paramString[i];

            if (c == '{')
            {
                braceDepth++;
            }
            else if (c == '}')
            {
                braceDepth--;
            }
            else if (c == ',' && braceDepth == 0)
            {
                // Found a parameter separator at the top level
                parameters.Add(paramString[start..i].Trim());
                start = i + 1;
            }
        }

        // Add the last parameter
        if (start < paramString.Length)
        {
            parameters.Add(paramString[start..].Trim());
        }

        return parameters.ToImmutable();
    }
}