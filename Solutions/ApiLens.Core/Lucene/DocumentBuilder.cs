using System.Text;
using ApiLens.Core.Models;
using Lucene.Net.Documents;

namespace ApiLens.Core.Lucene;

public class DocumentBuilder : IDocumentBuilder
{
    private static string SanitizeForStorage(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? string.Empty;

        // Replace control characters that can break JSON and other formats
        return input.Replace("\n", " ")
                   .Replace("\r", " ")
                   .Replace("\t", " ")
                   .Replace("\b", " ")
                   .Replace("\f", " ");
    }

    public Document BuildDocument(MemberInfo memberInfo)
    {
        ArgumentNullException.ThrowIfNull(memberInfo);

        Document doc =
        [
            new StringField(LuceneFields.Id, SanitizeForStorage(memberInfo.Id), Field.Store.YES),
            new StringField(LuceneFields.MemberType, memberInfo.MemberType.ToString(), Field.Store.YES),
            new StringField(LuceneFields.Name, memberInfo.Name, Field.Store.YES),
            new StringField(LuceneFields.FullName, memberInfo.FullName, Field.Store.YES),
            new StringField(LuceneFields.Assembly, memberInfo.Assembly, Field.Store.YES),
            new StringField(LuceneFields.Namespace, memberInfo.Namespace, Field.Store.YES),

            // Indexed fields for searching
            new TextField(LuceneFields.NameText, memberInfo.Name, Field.Store.NO),
            new TextField(LuceneFields.FullNameText, memberInfo.FullName, Field.Store.NO),
            new TextField(LuceneFields.NamespaceText, memberInfo.Namespace, Field.Store.NO),

            // Normalized fields for case-insensitive search
            new StringField(LuceneFields.NameNormalized, memberInfo.Name.ToLowerInvariant(), Field.Store.NO),
            new StringField(LuceneFields.FullNameNormalized, memberInfo.FullName.ToLowerInvariant(), Field.Store.NO),
            new StringField(LuceneFields.NamespaceNormalized, memberInfo.Namespace.ToLowerInvariant(), Field.Store.NO),

            // Facet field for filtering by member type
            new StringField(LuceneFields.MemberTypeFacet, memberInfo.MemberType.ToString(), Field.Store.YES)
        ];

        // Add declaring type for non-type members
        if (memberInfo.MemberType != MemberType.Type)
        {
            string declaringType = ExtractDeclaringType(memberInfo.FullName);
            if (!string.IsNullOrEmpty(declaringType))
            {
                doc.Add(new StringField(LuceneFields.DeclaringType, declaringType, Field.Store.YES));
            }
        }

        // Add type-specific search fields
        switch (memberInfo.MemberType)
        {
            case MemberType.Type:
                doc.Add(new TextField(LuceneFields.TypeSearch, memberInfo.Name, Field.Store.YES));
                break;
            case MemberType.Method:
                doc.Add(new TextField(LuceneFields.MethodSearch, memberInfo.Name, Field.Store.YES));
                break;
            case MemberType.Property:
                doc.Add(new TextField(LuceneFields.PropertySearch, memberInfo.Name, Field.Store.YES));
                break;
            case MemberType.Field:
                doc.Add(new TextField(LuceneFields.FieldSearch, memberInfo.Name, Field.Store.YES));
                break;
            case MemberType.Event:
                doc.Add(new TextField(LuceneFields.EventSearch, memberInfo.Name, Field.Store.YES));
                break;
        }

        // Add optional fields
        if (!string.IsNullOrWhiteSpace(memberInfo.Summary))
        {
            doc.Add(new TextField(LuceneFields.Summary, SanitizeForStorage(memberInfo.Summary), Field.Store.YES));
        }

        if (!string.IsNullOrWhiteSpace(memberInfo.Remarks))
        {
            doc.Add(new TextField(LuceneFields.Remarks, SanitizeForStorage(memberInfo.Remarks), Field.Store.YES));
        }

        // Add cross-references
        foreach (CrossReference crossRef in memberInfo.CrossReferences)
        {
            doc.Add(new StringField(LuceneFields.CrossRef, crossRef.TargetId, Field.Store.YES));
            doc.Add(new StringField(LuceneFields.CrossRefType(crossRef.Type.ToString()), crossRef.TargetId, Field.Store.YES));
        }

        // Add related types
        foreach (string relatedType in memberInfo.RelatedTypes)
        {
            doc.Add(new TextField(LuceneFields.RelatedType, relatedType, Field.Store.YES));
        }

        // Add code examples
        foreach (CodeExample example in memberInfo.CodeExamples)
        {
            doc.Add(new TextField(LuceneFields.CodeExample, example.Code, Field.Store.YES));
            if (!string.IsNullOrWhiteSpace(example.Description))
            {
                doc.Add(new TextField(LuceneFields.CodeExampleDescription, example.Description, Field.Store.YES));
            }
        }

        // Add exceptions
        foreach (ExceptionInfo exception in memberInfo.Exceptions)
        {
            // Store the full exception type as keyword field for exact matching
            doc.Add(new StringField(LuceneFields.ExceptionType, exception.Type, Field.Store.YES));

            // Also add as text field for partial matching
            doc.Add(new TextField(LuceneFields.ExceptionTypeText, exception.Type, Field.Store.NO));

            // Extract just the class name without namespace for easier searching
            string simpleName = exception.Type.Contains('.')
                ? exception.Type.Substring(exception.Type.LastIndexOf('.') + 1)
                : exception.Type;
            doc.Add(new TextField(LuceneFields.ExceptionSimpleName, simpleName, Field.Store.NO));

            if (!string.IsNullOrWhiteSpace(exception.Condition))
            {
                doc.Add(new TextField(LuceneFields.ExceptionCondition, exception.Condition, Field.Store.YES));
            }
        }

        // Add attributes
        foreach (AttributeInfo attribute in memberInfo.Attributes)
        {
            doc.Add(new StringField(LuceneFields.Attribute, attribute.Type, Field.Store.YES));
        }

        // Add parameters
        foreach (ParameterInfo parameter in memberInfo.Parameters)
        {
            doc.Add(new TextField(LuceneFields.Parameter, $"{parameter.Type} {parameter.Name}", Field.Store.YES));
            if (!string.IsNullOrWhiteSpace(parameter.Description))
            {
                doc.Add(new TextField(LuceneFields.ParameterDescription, parameter.Description, Field.Store.YES));
            }
        }

        // Add combined content field for general content searches
        StringBuilder contentBuilder = new();
        contentBuilder.Append(memberInfo.Name).Append(' ');
        contentBuilder.Append(memberInfo.FullName).Append(' ');

        if (!string.IsNullOrWhiteSpace(memberInfo.Summary))
        {
            contentBuilder.Append(memberInfo.Summary).Append(' ');
        }

        if (!string.IsNullOrWhiteSpace(memberInfo.Remarks))
        {
            contentBuilder.Append(memberInfo.Remarks).Append(' ');
        }

        // Include code examples in content
        foreach (CodeExample example in memberInfo.CodeExamples)
        {
            contentBuilder.Append(example.Code).Append(' ');
            if (!string.IsNullOrWhiteSpace(example.Description))
            {
                contentBuilder.Append(example.Description).Append(' ');
            }
        }

        // Include exception types in content
        foreach (ExceptionInfo exception in memberInfo.Exceptions)
        {
            contentBuilder.Append(exception.Type).Append(' ');
            if (!string.IsNullOrWhiteSpace(exception.Condition))
            {
                contentBuilder.Append(exception.Condition).Append(' ');
            }
        }

        // Include parameter descriptions
        foreach (ParameterInfo parameter in memberInfo.Parameters)
        {
            contentBuilder.Append(parameter.Type).Append(' ');
            contentBuilder.Append(parameter.Name).Append(' ');
            if (!string.IsNullOrWhiteSpace(parameter.Description))
            {
                contentBuilder.Append(parameter.Description).Append(' ');
            }
        }

        // Note: Content field is built later via BuildSearchableContent() for consistency
        // The contentBuilder here was used for inline building but caused duplicate field issues

        // Add returns documentation
        if (!string.IsNullOrWhiteSpace(memberInfo.Returns))
        {
            doc.Add(new TextField(LuceneFields.Returns, SanitizeForStorage(memberInfo.Returns), Field.Store.YES));
        }

        // Add return type for methods
        if (!string.IsNullOrWhiteSpace(memberInfo.ReturnType))
        {
            doc.Add(new StringField(LuceneFields.ReturnType, memberInfo.ReturnType, Field.Store.YES));
        }

        // Add see also references
        if (!string.IsNullOrWhiteSpace(memberInfo.SeeAlso))
        {
            doc.Add(new TextField(LuceneFields.SeeAlso, SanitizeForStorage(memberInfo.SeeAlso), Field.Store.YES));
        }

        // Add method modifiers
        if (memberInfo.MemberType == MemberType.Method)
        {
            doc.Add(new StringField(LuceneFields.IsStatic, memberInfo.IsStatic.ToString().ToLowerInvariant(), Field.Store.YES));
            doc.Add(new StringField(LuceneFields.IsAsync, memberInfo.IsAsync.ToString().ToLowerInvariant(), Field.Store.YES));
            doc.Add(new StringField(LuceneFields.IsExtension, memberInfo.IsExtension.ToString().ToLowerInvariant(), Field.Store.YES));
        }

        // Add complexity metrics
        if (memberInfo.Complexity != null)
        {
            doc.Add(new Int32Field(LuceneFields.ParameterCount, memberInfo.Complexity.ParameterCount, Field.Store.YES));
            doc.Add(new Int32Field(LuceneFields.CyclomaticComplexity, memberInfo.Complexity.CyclomaticComplexity,
                Field.Store.YES));
            doc.Add(new Int32Field(LuceneFields.DocumentationLineCount, memberInfo.Complexity.DocumentationLineCount,
                Field.Store.YES));
        }

        // Add version tracking fields
        if (!string.IsNullOrWhiteSpace(memberInfo.PackageId))
        {
            doc.Add(new StringField(LuceneFields.PackageId, memberInfo.PackageId, Field.Store.YES));
            // Add normalized package ID for case-insensitive search
            doc.Add(new StringField(LuceneFields.PackageIdNormalized, memberInfo.PackageId.ToLowerInvariant(), Field.Store.NO));
        }

        if (!string.IsNullOrWhiteSpace(memberInfo.PackageVersion))
        {
            doc.Add(new StringField(LuceneFields.PackageVersion, memberInfo.PackageVersion, Field.Store.YES));
        }

        if (!string.IsNullOrWhiteSpace(memberInfo.TargetFramework))
        {
            doc.Add(new StringField(LuceneFields.TargetFramework, memberInfo.TargetFramework, Field.Store.YES));
        }

        doc.Add(new StringField(LuceneFields.IsFromNuGetCache, memberInfo.IsFromNuGetCache.ToString().ToLowerInvariant(),
            Field.Store.YES));

        // Always add sourceFilePath for proper change detection tracking
        doc.Add(new StringField(LuceneFields.SourceFilePath, SanitizeForStorage(memberInfo.SourceFilePath ?? string.Empty), Field.Store.YES));

        // Add searchable version field
        if (!string.IsNullOrWhiteSpace(memberInfo.PackageVersion))
        {
            doc.Add(new TextField(LuceneFields.VersionSearch, memberInfo.PackageVersion, Field.Store.YES));
        }

        // Add content hash for deduplication (not stored)
        if (!string.IsNullOrWhiteSpace(memberInfo.ContentHash))
        {
            doc.Add(new StringField(LuceneFields.ContentHash, memberInfo.ContentHash, Field.Store.NO));
        }

        // Method-specific fields would be handled by overloaded methods

        // Create a combined searchable content field
        string content = BuildSearchableContent(memberInfo);
        doc.Add(new TextField(LuceneFields.Content, content, Field.Store.YES));

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
        {
            parts.Add(memberInfo.SeeAlso);
        }

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
            doc.Add(new StringField(LuceneFields.BaseType, typeInfo.BaseType, Field.Store.YES));
        }

        foreach (string interfaceName in typeInfo.Interfaces)
        {
            doc.Add(new StringField(LuceneFields.Interface, interfaceName, Field.Store.YES));
        }

        if (typeInfo.IsGeneric)
        {
            doc.Add(new StringField(LuceneFields.IsGeneric, "true", Field.Store.YES));
            doc.Add(new Int32Field(LuceneFields.GenericArity, typeInfo.GenericArity, Field.Store.YES));
        }

        doc.Add(new StringField(LuceneFields.TypeKind, typeInfo.Kind.ToString(), Field.Store.YES));

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
            Remarks = methodInfo.Remarks,
            IsStatic = methodInfo.IsStatic,
            IsAsync = methodInfo.IsAsync,
            IsExtension = methodInfo.IsExtension,
            ReturnType = methodInfo.ReturnType
        };

        Document doc = BuildDocument(memberInfo);

        // Add method-specific fields (parameters only, since other fields are already added)
        foreach (ParameterInfo parameter in methodInfo.Parameters)
        {
            doc.Add(new TextField(LuceneFields.Parameter, $"{parameter.Type} {parameter.Name}", Field.Store.YES));
        }

        return doc;
    }

    private static string ExtractNamespaceFromDeclaringType(string declaringType)
    {
        int lastDot = declaringType.LastIndexOf('.');
        return lastDot >= 0 ? declaringType[..lastDot] : string.Empty;
    }

    private static string ExtractDeclaringType(string fullName)
    {
        // Handle method signatures with parameters
        // E.g., "Namespace.Type.Method(Param1,Param2)" -> "Namespace.Type"

        // First, remove parameter list if present
        int parenIndex = fullName.IndexOf('(');
        string nameWithoutParams = parenIndex > 0 ? fullName[..parenIndex] : fullName;

        // For generic methods, remove generic parameters
        // E.g., "Namespace.Type.Method`2" -> "Namespace.Type.Method"
        int backtickIndex = nameWithoutParams.LastIndexOf('`');
        if (backtickIndex > 0)
        {
            nameWithoutParams = nameWithoutParams[..backtickIndex];
        }

        // Now extract the declaring type (everything before last dot)
        int lastDot = nameWithoutParams.LastIndexOf('.');
        return lastDot > 0 ? nameWithoutParams[..lastDot] : string.Empty;
    }
}