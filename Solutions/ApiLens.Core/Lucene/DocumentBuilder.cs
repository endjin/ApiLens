using System.Text;
using ApiLens.Core.Models;
using Lucene.Net.Documents;

namespace ApiLens.Core.Lucene;

public class DocumentBuilder : IDocumentBuilder
{
    public Document BuildDocument(MemberInfo memberInfo)
    {
        ArgumentNullException.ThrowIfNull(memberInfo);

        Document doc =
        [
            new StringField("id", memberInfo.Id, Field.Store.YES),
            new StringField("memberType", memberInfo.MemberType.ToString(), Field.Store.YES),
            new StringField("name", memberInfo.Name, Field.Store.YES),
            new StringField("fullName", memberInfo.FullName, Field.Store.YES),
            new StringField("assembly", memberInfo.Assembly, Field.Store.YES),
            new StringField("namespace", memberInfo.Namespace, Field.Store.YES),

            // Indexed fields for searching
            new TextField("nameText", memberInfo.Name, Field.Store.NO),
            new TextField("fullNameText", memberInfo.FullName, Field.Store.NO),
            new TextField("namespaceText", memberInfo.Namespace, Field.Store.NO),
            
            // Facet field for filtering by member type
            new StringField("memberTypeFacet", memberInfo.MemberType.ToString(), Field.Store.YES)
        ];

        // Add type-specific search fields
        switch (memberInfo.MemberType)
        {
            case MemberType.Type:
                doc.Add(new TextField("typeSearch", memberInfo.Name, Field.Store.YES));
                break;
            case MemberType.Method:
                doc.Add(new TextField("methodSearch", memberInfo.Name, Field.Store.YES));
                break;
            case MemberType.Property:
                doc.Add(new TextField("propertySearch", memberInfo.Name, Field.Store.YES));
                break;
            case MemberType.Field:
                doc.Add(new TextField("fieldSearch", memberInfo.Name, Field.Store.YES));
                break;
            case MemberType.Event:
                doc.Add(new TextField("eventSearch", memberInfo.Name, Field.Store.YES));
                break;
        }

        // Add optional fields
        if (!string.IsNullOrWhiteSpace(memberInfo.Summary))
        {
            doc.Add(new TextField("summary", memberInfo.Summary, Field.Store.YES));
        }

        if (!string.IsNullOrWhiteSpace(memberInfo.Remarks))
        {
            doc.Add(new TextField("remarks", memberInfo.Remarks, Field.Store.YES));
        }

        // Add cross-references
        foreach (CrossReference crossRef in memberInfo.CrossReferences)
        {
            doc.Add(new StringField("crossref", crossRef.TargetId, Field.Store.YES));
            doc.Add(new StringField($"crossref_{crossRef.Type}", crossRef.TargetId, Field.Store.YES));
        }

        // Add related types
        foreach (string relatedType in memberInfo.RelatedTypes)
        {
            doc.Add(new TextField("relatedType", relatedType, Field.Store.YES));
        }

        // Add code examples
        foreach (CodeExample example in memberInfo.CodeExamples)
        {
            doc.Add(new TextField("codeExample", example.Code, Field.Store.YES));
            if (!string.IsNullOrWhiteSpace(example.Description))
            {
                doc.Add(new TextField("codeExampleDescription", example.Description, Field.Store.YES));
            }
        }

        // Add exceptions
        foreach (ExceptionInfo exception in memberInfo.Exceptions)
        {
            doc.Add(new StringField("exceptionType", exception.Type, Field.Store.YES));
            if (!string.IsNullOrWhiteSpace(exception.Condition))
            {
                doc.Add(new TextField("exceptionCondition", exception.Condition, Field.Store.YES));
            }
        }

        // Add attributes
        foreach (AttributeInfo attribute in memberInfo.Attributes)
        {
            doc.Add(new StringField("attribute", attribute.Type, Field.Store.YES));
        }

        // Add parameters
        foreach (ParameterInfo parameter in memberInfo.Parameters)
        {
            doc.Add(new TextField("parameter", $"{parameter.Type} {parameter.Name}", Field.Store.YES));
            if (!string.IsNullOrWhiteSpace(parameter.Description))
            {
                doc.Add(new TextField("parameterDescription", parameter.Description, Field.Store.YES));
            }
        }

        // Add combined content field for general content searches
        StringBuilder contentBuilder = new();
        contentBuilder.Append(memberInfo.Name).Append(' ');
        contentBuilder.Append(memberInfo.FullName).Append(' ');

        if (!string.IsNullOrWhiteSpace(memberInfo.Summary))
            contentBuilder.Append(memberInfo.Summary).Append(' ');

        if (!string.IsNullOrWhiteSpace(memberInfo.Remarks))
            contentBuilder.Append(memberInfo.Remarks).Append(' ');

        // Include code examples in content
        foreach (CodeExample example in memberInfo.CodeExamples)
        {
            contentBuilder.Append(example.Code).Append(' ');
            if (!string.IsNullOrWhiteSpace(example.Description))
                contentBuilder.Append(example.Description).Append(' ');
        }

        // Include exception types in content
        foreach (ExceptionInfo exception in memberInfo.Exceptions)
        {
            contentBuilder.Append(exception.Type).Append(' ');
            if (!string.IsNullOrWhiteSpace(exception.Condition))
                contentBuilder.Append(exception.Condition).Append(' ');
        }

        // Include parameter descriptions
        foreach (ParameterInfo parameter in memberInfo.Parameters)
        {
            contentBuilder.Append(parameter.Type).Append(' ');
            contentBuilder.Append(parameter.Name).Append(' ');
            if (!string.IsNullOrWhiteSpace(parameter.Description))
                contentBuilder.Append(parameter.Description).Append(' ');
        }

        doc.Add(new TextField("content", contentBuilder.ToString(), Field.Store.NO));

        // Add returns documentation
        if (!string.IsNullOrWhiteSpace(memberInfo.Returns))
        {
            doc.Add(new TextField("returns", memberInfo.Returns, Field.Store.YES));
        }

        // Add see also references
        if (!string.IsNullOrWhiteSpace(memberInfo.SeeAlso))
        {
            doc.Add(new TextField("seeAlso", memberInfo.SeeAlso, Field.Store.YES));
        }

        // Add complexity metrics
        if (memberInfo.Complexity != null)
        {
            doc.Add(new Int32Field("parameterCount", memberInfo.Complexity.ParameterCount, Field.Store.YES));
            doc.Add(new Int32Field("cyclomaticComplexity", memberInfo.Complexity.CyclomaticComplexity, Field.Store.YES));
            doc.Add(new Int32Field("documentationLineCount", memberInfo.Complexity.DocumentationLineCount, Field.Store.YES));
        }

        // Add version tracking fields
        if (!string.IsNullOrWhiteSpace(memberInfo.PackageId))
        {
            doc.Add(new StringField("packageId", memberInfo.PackageId, Field.Store.YES));
        }

        if (!string.IsNullOrWhiteSpace(memberInfo.PackageVersion))
        {
            doc.Add(new StringField("packageVersion", memberInfo.PackageVersion, Field.Store.YES));
        }

        if (!string.IsNullOrWhiteSpace(memberInfo.TargetFramework))
        {
            doc.Add(new StringField("targetFramework", memberInfo.TargetFramework, Field.Store.YES));
        }

        doc.Add(new StringField("isFromNuGetCache", memberInfo.IsFromNuGetCache.ToString().ToLowerInvariant(), Field.Store.YES));

        // Always add sourceFilePath for proper change detection tracking
        doc.Add(new StringField("sourceFilePath", memberInfo.SourceFilePath ?? string.Empty, Field.Store.YES));

        // Add searchable version field
        if (!string.IsNullOrWhiteSpace(memberInfo.PackageVersion))
        {
            doc.Add(new TextField("versionSearch", memberInfo.PackageVersion, Field.Store.YES));
        }

        // Add content hash for deduplication (not stored)
        if (!string.IsNullOrWhiteSpace(memberInfo.ContentHash))
        {
            doc.Add(new StringField("contentHash", memberInfo.ContentHash, Field.Store.NO));
        }

        // Method-specific fields would be handled by overloaded methods

        // Create a combined searchable content field
        string content = BuildSearchableContent(memberInfo);
        doc.Add(new TextField("content", content, Field.Store.YES));

        return doc;
    }

    private static string BuildSearchableContent(MemberInfo memberInfo)
    {
        List<string> parts =
        [
            memberInfo.Name,
            memberInfo.FullName,
            memberInfo.Namespace
        ];

        if (!string.IsNullOrWhiteSpace(memberInfo.Summary))
        {
            parts.Add(memberInfo.Summary);
        }

        if (!string.IsNullOrWhiteSpace(memberInfo.Remarks))
        {
            parts.Add(memberInfo.Remarks);
        }

        // Add code examples to searchable content
        foreach (CodeExample example in memberInfo.CodeExamples)
        {
            if (!string.IsNullOrWhiteSpace(example.Description))
            {
                parts.Add(example.Description);
            }

            parts.Add(example.Code);
        }

        // Add exception conditions
        foreach (ExceptionInfo exception in memberInfo.Exceptions)
        {
            parts.Add(exception.Type);

            if (!string.IsNullOrWhiteSpace(exception.Condition))
            {
                parts.Add(exception.Condition);
            }
        }

        // Add parameter descriptions
        foreach (ParameterInfo parameter in memberInfo.Parameters)
        {
            parts.Add(parameter.Name);
            parts.Add(parameter.Type);

            if (!string.IsNullOrWhiteSpace(parameter.Description))
            {
                parts.Add(parameter.Description);
            }
        }

        // Add returns documentation
        if (!string.IsNullOrWhiteSpace(memberInfo.Returns))
        {
            parts.Add(memberInfo.Returns);
        }

        // Add see also references
        if (!string.IsNullOrWhiteSpace(memberInfo.SeeAlso))
            parts.Add(memberInfo.SeeAlso);

        return string.Join(" ", parts);
    }

    public Document BuildDocument(TypeInfo typeInfo, string? summary = null, string? remarks = null)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        // Create base member info to reuse common logic
        MemberInfo memberInfo = new()
        {
            Id = typeInfo.Id,
            MemberType = MemberType.Type,
            Name = typeInfo.Name,
            FullName = typeInfo.FullName,
            Assembly = typeInfo.Assembly,
            Namespace = typeInfo.Namespace,
            Summary = summary,
            Remarks = remarks
        };

        Document doc = BuildDocument(memberInfo);

        // Add type-specific fields
        if (!string.IsNullOrWhiteSpace(typeInfo.BaseType))
        {
            doc.Add(new StringField("baseType", typeInfo.BaseType, Field.Store.YES));
        }

        foreach (string interfaceName in typeInfo.Interfaces)
        {
            doc.Add(new StringField("interface", interfaceName, Field.Store.YES));
        }

        if (typeInfo.IsGeneric)
        {
            doc.Add(new StringField("isGeneric", "true", Field.Store.YES));
            doc.Add(new Int32Field("genericArity", typeInfo.GenericArity, Field.Store.YES));
        }

        doc.Add(new StringField("typeKind", typeInfo.Kind.ToString(), Field.Store.YES));

        return doc;
    }

    public Document BuildDocument(MethodInfo methodInfo)
    {
        ArgumentNullException.ThrowIfNull(methodInfo);

        // Create base member info to reuse common logic
        MemberInfo memberInfo = new()
        {
            Id = methodInfo.Id,
            MemberType = MemberType.Method,
            Name = methodInfo.Name,
            FullName = methodInfo.FullName,
            Assembly = "Unknown", // Would need to be provided separately
            Namespace = ExtractNamespaceFromDeclaringType(methodInfo.DeclaringType),
            Summary = methodInfo.Summary,
            Remarks = methodInfo.Remarks
        };

        Document doc = BuildDocument(memberInfo);

        // Add method-specific fields
        doc.Add(new StringField("returnType", methodInfo.ReturnType, Field.Store.YES));

        foreach (ParameterInfo parameter in methodInfo.Parameters)
        {
            doc.Add(new TextField("parameter", $"{parameter.Type} {parameter.Name}", Field.Store.YES));
        }

        if (methodInfo.IsStatic)
        {
            doc.Add(new StringField("isStatic", "true", Field.Store.YES));
        }

        if (methodInfo.IsExtension)
        {
            doc.Add(new StringField("isExtension", "true", Field.Store.YES));
        }

        if (methodInfo.IsAsync)
        {
            doc.Add(new StringField("isAsync", "true", Field.Store.YES));
        }

        return doc;
    }

    private static string ExtractNamespaceFromDeclaringType(string declaringType)
    {
        int lastDot = declaringType.LastIndexOf('.');
        return lastDot >= 0 ? declaringType[..lastDot] : string.Empty;
    }
}