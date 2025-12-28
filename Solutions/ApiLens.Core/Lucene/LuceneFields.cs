namespace ApiLens.Core.Lucene;

/// <summary>
/// Constants for Lucene index field names used throughout the ApiLens indexing and querying system.
/// Using constants prevents typos and enables refactoring support.
/// </summary>
public static class LuceneFields
{
    // Core identity fields
    public const string Id = "id";
    public const string MemberType = "memberType";
    public const string Name = "name";
    public const string FullName = "fullName";
    public const string Assembly = "assembly";
    public const string Namespace = "namespace";

    // Text search fields (analyzed)
    public const string NameText = "nameText";
    public const string FullNameText = "fullNameText";
    public const string NamespaceText = "namespaceText";

    // Normalized fields (case-insensitive search)
    public const string NameNormalized = "nameNormalized";
    public const string FullNameNormalized = "fullNameNormalized";
    public const string NamespaceNormalized = "namespaceNormalized";

    // Facet and filter fields
    public const string MemberTypeFacet = "memberTypeFacet";
    public const string DeclaringType = "declaringType";

    // Type-specific search fields
    public const string TypeSearch = "typeSearch";
    public const string MethodSearch = "methodSearch";
    public const string PropertySearch = "propertySearch";
    public const string FieldSearch = "fieldSearch";
    public const string EventSearch = "eventSearch";

    // Documentation fields
    public const string Summary = "summary";
    public const string Remarks = "remarks";
    public const string Returns = "returns";
    public const string ReturnType = "returnType";
    public const string SeeAlso = "seeAlso";
    public const string Content = "content";

    // Cross-reference fields
    public const string CrossRef = "crossref";
    public const string RelatedType = "relatedType";

    // Code example fields
    public const string CodeExample = "codeExample";
    public const string CodeExampleDescription = "codeExampleDescription";

    // Exception fields
    public const string ExceptionType = "exceptionType";
    public const string ExceptionTypeText = "exceptionTypeText";
    public const string ExceptionSimpleName = "exceptionSimpleName";
    public const string ExceptionCondition = "exceptionCondition";

    // Attribute fields
    public const string Attribute = "attribute";

    // Parameter fields
    public const string Parameter = "parameter";
    public const string ParameterDescription = "parameterDescription";

    // Method modifier fields
    public const string IsStatic = "isStatic";
    public const string IsAsync = "isAsync";
    public const string IsExtension = "isExtension";

    // Complexity metric fields
    public const string ParameterCount = "parameterCount";
    public const string CyclomaticComplexity = "cyclomaticComplexity";
    public const string DocumentationLineCount = "documentationLineCount";

    // Package/version tracking fields
    public const string PackageId = "packageId";
    public const string PackageIdNormalized = "packageIdNormalized";
    public const string PackageVersion = "packageVersion";
    public const string TargetFramework = "targetFramework";
    public const string IsFromNuGetCache = "isFromNuGetCache";
    public const string SourceFilePath = "sourceFilePath";
    public const string VersionSearch = "versionSearch";
    public const string ContentHash = "contentHash";

    // Type-specific fields
    public const string BaseType = "baseType";
    public const string Interface = "interface";
    public const string IsGeneric = "isGeneric";
    public const string GenericArity = "genericArity";
    public const string TypeKind = "typeKind";

    // Document tracking fields
    public const string DocumentType = "documentType";

    /// <summary>
    /// Generates a cross-reference field name for a specific reference type.
    /// </summary>
    /// <param name="referenceType">The type of cross-reference.</param>
    /// <returns>The field name for the cross-reference type.</returns>
    public static string CrossRefType(string referenceType) => $"crossref_{referenceType}";
}
