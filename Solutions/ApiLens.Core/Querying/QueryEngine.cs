using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using Lucene.Net.Documents;
using Lucene.Net.Search;

namespace ApiLens.Core.Querying;

public class QueryEngine : IQueryEngine
{
    private readonly ILuceneIndexManager indexManager;
    private bool disposed;

    public QueryEngine(ILuceneIndexManager indexManager)
    {
        ArgumentNullException.ThrowIfNull(indexManager);
        this.indexManager = indexManager;
    }

    public List<MemberInfo> SearchByName(string name, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        TopDocs topDocs = indexManager.SearchByField("nameText", name, maxResults);
        return ConvertTopDocsToMembers(topDocs);
    }

    public List<MemberInfo> SearchByContent(string searchText, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchText);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        TopDocs topDocs = indexManager.SearchByField("content", searchText, maxResults);
        return ConvertTopDocsToMembers(topDocs);
    }

    public List<MemberInfo> SearchByNamespace(string namespaceName, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        TopDocs topDocs = indexManager.SearchByField("namespaceText", namespaceName, maxResults);
        return ConvertTopDocsToMembers(topDocs);
    }

    public MemberInfo? GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        TopDocs topDocs = indexManager.SearchByField("id", id, 1);
        List<MemberInfo> members = ConvertTopDocsToMembers(topDocs);
        return members.Count > 0 ? members[0] : null;
    }

    public List<MemberInfo> SearchByAssembly(string assemblyName, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        TopDocs topDocs = indexManager.SearchByField("assembly", assemblyName, maxResults);
        return ConvertTopDocsToMembers(topDocs);
    }

    public List<MemberInfo> SearchByMemberType(MemberType memberType, int maxResults)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        TopDocs topDocs = indexManager.SearchByField("memberType", memberType.ToString(), maxResults);
        return ConvertTopDocsToMembers(topDocs);
    }

    public List<MemberInfo> GetByType(MemberType memberType, int maxResults)
    {
        return SearchByMemberType(memberType, maxResults);
    }

    public List<MemberInfo> GetTypeMembers(string typeName, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        // Search for the type's members by searching for members whose fullName starts with the type name
        TopDocs topDocs = indexManager.SearchByField("fullNameText", typeName + ".", maxResults);
        return ConvertTopDocsToMembers(topDocs);
    }

    public List<MemberInfo> SearchByCodeExample(string codePattern, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codePattern);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        TopDocs topDocs = indexManager.SearchByField("codeExample", codePattern, maxResults);
        return ConvertTopDocsToMembers(topDocs);
    }

    public List<MemberInfo> GetByExceptionType(string exceptionType, int maxResults)
    {
        return SearchByException(exceptionType, maxResults);
    }

    public List<MemberInfo> GetByParameterCount(int min, int max, int maxResults)
    {
        return SearchByParameterCount(min, max, maxResults);
    }

    public List<MemberInfo> GetMethodsWithExamples(int maxResults)
    {
        return SearchWithCodeExamples(maxResults);
    }

    public List<MemberInfo> GetComplexMethods(int minComplexity, int maxResults)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minComplexity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        return SearchByComplexity(minComplexity, int.MaxValue, maxResults);
    }

    public List<MemberInfo> SearchByField(string fieldName, string value, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        TopDocs topDocs = indexManager.SearchByField(fieldName, value, maxResults);
        return ConvertTopDocsToMembers(topDocs);
    }

    public List<MemberInfo> SearchWithCodeExamples(int maxResults)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        // Search for documents that have the codeExample field
        List<Document> documents = indexManager.SearchByFieldExists("codeExample", maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    public List<MemberInfo> SearchByException(string exceptionType, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exceptionType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        // Search by exception type field with the full type name
        TopDocs topDocs = indexManager.SearchByField("exceptionType", exceptionType, maxResults);
        return ConvertTopDocsToMembers(topDocs);
    }

    public List<MemberInfo> SearchByParameterCount(int minParams, int maxParams, int maxResults)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minParams);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxParams, minParams);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        // Search for methods with parameter count in range
        List<Document> documents = indexManager.SearchByIntRange("parameterCount", minParams, maxParams, maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    public List<MemberInfo> SearchByComplexity(int minComplexity, int maxComplexity, int maxResults)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minComplexity);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxComplexity, minComplexity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        // Search for methods with cyclomatic complexity in range
        List<Document> documents = indexManager.SearchByIntRange("cyclomaticComplexity", minComplexity, maxComplexity, maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    // Additional package-specific searches
    public List<MemberInfo> SearchByPackage(string packageId, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        TopDocs topDocs = indexManager.SearchByField("packageId", packageId, maxResults);
        return ConvertTopDocsToMembers(topDocs);
    }

    public List<MemberInfo> SearchByVersion(string version, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        TopDocs topDocs = indexManager.SearchByField("packageVersion", version, maxResults);
        return ConvertTopDocsToMembers(topDocs);
    }

    public List<MemberInfo> SearchByTargetFramework(string framework, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(framework);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        TopDocs topDocs = indexManager.SearchByField("targetFramework", framework, maxResults);
        return ConvertTopDocsToMembers(topDocs);
    }

    private List<MemberInfo> ConvertTopDocsToMembers(TopDocs topDocs)
    {
        if (topDocs?.ScoreDocs == null)
            return [];

        List<MemberInfo> members = new(topDocs.ScoreDocs.Length);

        foreach (ScoreDoc? scoreDoc in topDocs.ScoreDocs)
        {
            Document? doc = indexManager.GetDocument(scoreDoc.Doc);
            if (doc != null)
            {
                MemberInfo? member = ConvertDocumentToMember(doc);
                if (member != null)
                {
                    members.Add(member);
                }
            }
        }

        return members;
    }

    private static List<MemberInfo> ConvertDocumentsToMembers(List<Document> documents)
    {
        List<MemberInfo> members = new(documents.Count);

        foreach (Document doc in documents)
        {
            MemberInfo? member = ConvertDocumentToMember(doc);
            if (member != null)
            {
                members.Add(member);
            }
        }

        return members;
    }

    private static MemberInfo? ConvertDocumentToMember(Document document)
    {
        string? id = document.Get("id");
        string? memberTypeStr = document.Get("memberType");
        string? name = document.Get("name");
        string? fullName = document.Get("fullName");
        string? assembly = document.Get("assembly");
        string? namespaceName = document.Get("namespace");

        if (string.IsNullOrEmpty(id) ||
            string.IsNullOrEmpty(memberTypeStr) ||
            string.IsNullOrEmpty(name) ||
            string.IsNullOrEmpty(fullName) ||
            string.IsNullOrEmpty(assembly) ||
            string.IsNullOrEmpty(namespaceName))
        {
            return null;
        }

        if (!Enum.TryParse<MemberType>(memberTypeStr, out MemberType memberType))
        {
            return null;
        }

        // Extract related types
        string[]? relatedTypeFields = document.GetValues("relatedType");
        ImmutableArray<string> relatedTypes = relatedTypeFields != null
            ? [.. relatedTypeFields]
            : [];

        // Extract cross-references
        string[]? crossRefFields = document.GetValues("crossref");
        List<CrossReference> crossRefs = [];

        if (crossRefFields != null)
        {
            foreach (string crossRefId in crossRefFields)
            {
                // Try to determine cross-reference type from specific fields
                ReferenceType refType = ReferenceType.SeeAlso;

                if (document.Get($"crossref_Inherits") == crossRefId)
                    refType = ReferenceType.Inheritance;
                else if (document.Get($"crossref_Implements") == crossRefId)
                    refType = ReferenceType.Inheritance;
                else if (document.Get($"crossref_Return") == crossRefId)
                    refType = ReferenceType.ReturnType;
                else if (document.Get($"crossref_Param") == crossRefId)
                    refType = ReferenceType.Parameter;

                crossRefs.Add(new CrossReference
                {
                    SourceId = id,
                    TargetId = crossRefId,
                    Type = refType,
                    Context = string.Empty
                });
            }
        }

        // Extract code examples
        string[]? exampleFields = document.GetValues("codeExample");
        string[]? exampleDescFields = document.GetValues("codeExampleDescription");
        List<CodeExample> examples = [];

        if (exampleFields != null)
        {
            for (int i = 0; i < exampleFields.Length; i++)
            {
                examples.Add(new CodeExample
                {
                    Code = exampleFields[i],
                    Description = exampleDescFields != null && i < exampleDescFields.Length
                        ? exampleDescFields[i]
                        : string.Empty
                });
            }
        }

        // Extract exceptions
        string[]? exceptionTypes = document.GetValues("exceptionType");
        string[]? exceptionConditions = document.GetValues("exceptionCondition");
        List<ExceptionInfo> exceptions = [];

        if (exceptionTypes != null)
        {
            for (int i = 0; i < exceptionTypes.Length; i++)
            {
                exceptions.Add(new ExceptionInfo
                {
                    Type = exceptionTypes[i],
                    Condition = exceptionConditions != null && i < exceptionConditions.Length
                        ? exceptionConditions[i]
                        : null
                });
            }
        }

        // Extract attributes
        string[]? attributeTypes = document.GetValues("attribute");
        List<AttributeInfo> attributes = [];

        if (attributeTypes != null)
        {
            foreach (string attrType in attributeTypes)
            {
                attributes.Add(new AttributeInfo
                {
                    Type = attrType,
                    Properties = ImmutableDictionary<string, string>.Empty // Not stored in index
                });
            }
        }

        // Extract parameters
        string[]? parameterFields = document.GetValues("parameter");
        string[]? parameterDescFields = document.GetValues("parameterDescription");
        List<ParameterInfo> parameters = [];

        if (parameterFields != null)
        {
            for (int i = 0; i < parameterFields.Length; i++)
            {
                // Parse parameter field which is in format "Type Name"
                string paramField = parameterFields[i];
                int spaceIndex = paramField.IndexOf(' ');
                string paramType = spaceIndex > 0 ? paramField[..spaceIndex] : "";
                string paramName = spaceIndex > 0 ? paramField[(spaceIndex + 1)..] : paramField;

                parameters.Add(new ParameterInfo
                {
                    Name = paramName,
                    Type = paramType,
                    Description = parameterDescFields != null && i < parameterDescFields.Length
                        ? parameterDescFields[i]
                        : null,
                    Position = i,
                    IsOptional = false,
                    IsParams = false,
                    IsOut = false,
                    IsRef = false
                });
            }
        }

        // Extract complexity metrics
        ComplexityMetrics? complexity = null;
        string? paramCountStr = document.Get("parameterCount");
        string? complexityStr = document.Get("cyclomaticComplexity");
        string? docLinesStr = document.Get("documentationLineCount");

        if (!string.IsNullOrEmpty(paramCountStr) &&
            !string.IsNullOrEmpty(complexityStr) &&
            !string.IsNullOrEmpty(docLinesStr) &&
            int.TryParse(paramCountStr, out int paramCount) &&
            int.TryParse(complexityStr, out int cyclomaticComplexity) &&
            int.TryParse(docLinesStr, out int docLines))
        {
            complexity = new ComplexityMetrics
            {
                ParameterCount = paramCount,
                CyclomaticComplexity = cyclomaticComplexity,
                DocumentationLineCount = docLines
            };
        }

        // Version tracking fields
        string? packageId = document.Get("packageId");
        string? packageVersion = document.Get("packageVersion");
        string? targetFramework = document.Get("targetFramework");
        string? isFromNuGetCacheStr = document.Get("isFromNuGetCache");
        string? sourceFilePath = document.Get("sourceFilePath");

        return new MemberInfo
        {
            Id = id,
            MemberType = memberType,
            Name = name,
            FullName = fullName,
            Assembly = assembly,
            Namespace = namespaceName,
            Summary = document.Get("summary"),
            Remarks = document.Get("remarks"),
            Returns = document.Get("returns"),
            SeeAlso = document.Get("seeAlso"),
            CrossReferences = [.. crossRefs],
            RelatedTypes = relatedTypes,
            CodeExamples = [.. examples],
            Exceptions = [.. exceptions],
            Attributes = [.. attributes],
            Parameters = [.. parameters],
            Complexity = complexity,
            PackageId = !string.IsNullOrEmpty(packageId) ? packageId : null,
            PackageVersion = !string.IsNullOrEmpty(packageVersion) ? packageVersion : null,
            TargetFramework = !string.IsNullOrEmpty(targetFramework) ? targetFramework : null,
            IsFromNuGetCache = bool.TryParse(isFromNuGetCacheStr, out bool isFromCache) && isFromCache,
            SourceFilePath = !string.IsNullOrEmpty(sourceFilePath) ? sourceFilePath : null
        };
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            indexManager?.Dispose();
        }
    }
}