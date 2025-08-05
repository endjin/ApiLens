using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace ApiLens.Core.Querying;

public class QueryEngine : IQueryEngine
{
    private readonly ILuceneIndexManager indexManager;
    private bool disposed;

    // Common .NET namespaces for exception type resolution
    private static readonly string[] CommonExceptionNamespaces =
    {
        "System",
        "System.IO",
        "System.Net",
        "System.Net.Http",
        "System.Data",
        "System.Xml",
        "System.Collections",
        "System.Collections.Generic",
        "System.Threading",
        "System.Threading.Tasks",
        "System.Security",
        "System.Runtime",
        "System.Runtime.InteropServices",
        "System.ComponentModel",
        "System.Configuration",
        "System.Diagnostics",
        "System.Linq",
        "System.Text"
    };

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

        // Build a comprehensive query based on the search term characteristics
        Query query = BuildExceptionSearchQuery(exceptionType);

        // Execute search with extra results for deduplication
        TopDocs topDocs = indexManager.SearchWithQuery(query, maxResults * 2);

        // Convert and deduplicate results
        return ConvertTopDocsToMembers(topDocs)
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .Take(maxResults)
            .ToList();
    }

    private Query BuildExceptionSearchQuery(string searchTerm)
    {
        BooleanQuery queryBuilder = new();

        bool hasWildcards = searchTerm.Contains('*') || searchTerm.Contains('?');
        bool hasNamespace = searchTerm.Contains('.');

        if (hasWildcards)
        {
            // Wildcard search strategy
            AddWildcardQueries(queryBuilder, searchTerm, hasNamespace);
        }
        else if (hasNamespace)
        {
            // Namespaced search strategy (e.g., "System.IOException")
            AddNamespacedQueries(queryBuilder, searchTerm);
        }
        else
        {
            // Simple name search strategy (e.g., "IOException")
            AddSimpleNameQueries(queryBuilder, searchTerm);
        }

        // Always add text field search as fallback
        queryBuilder.Add(new TermQuery(new Term("exceptionTypeText", searchTerm.ToLowerInvariant())), Occur.SHOULD);

        return queryBuilder;
    }

    private void AddWildcardQueries(BooleanQuery queryBuilder, string pattern, bool hasNamespace)
    {
        // Try wildcard on full exception type
        Query? fullWildcard = CreateWildcardQuery("exceptionType", pattern);
        if (fullWildcard != null)
        {
            queryBuilder.Add(fullWildcard, Occur.SHOULD);
        }

        // If no namespace, also try on simple name and with common namespaces
        if (!hasNamespace)
        {
            Query? simpleWildcard = CreateWildcardQuery("exceptionSimpleName", pattern);
            if (simpleWildcard != null)
            {
                queryBuilder.Add(simpleWildcard, Occur.SHOULD);
            }

            // Try with common namespaces
            foreach (string ns in CommonExceptionNamespaces.Take(5)) // Limit to most common
            {
                Query? nsWildcard = CreateWildcardQuery("exceptionType", $"{ns}.{pattern}");
                if (nsWildcard != null)
                {
                    queryBuilder.Add(nsWildcard, Occur.SHOULD);
                }
            }
        }
    }

    private void AddNamespacedQueries(BooleanQuery queryBuilder, string searchTerm)
    {
        // Exact match
        queryBuilder.Add(new TermQuery(new Term("exceptionType", searchTerm)), Occur.SHOULD);

        // Prefix match (e.g., "System.IOException" matches "System.IO.IOException")
        Query? prefixQuery = CreateWildcardQuery("exceptionType", $"{searchTerm}*");
        if (prefixQuery != null)
        {
            queryBuilder.Add(prefixQuery, Occur.SHOULD);
        }

        // Middle wildcard (e.g., "System.IOException" -> "System.*.IOException")
        int lastDot = searchTerm.LastIndexOf('.');
        if (lastDot > 0 && lastDot < searchTerm.Length - 1)
        {
            string namespacePart = searchTerm.Substring(0, lastDot);
            string typeName = searchTerm.Substring(lastDot + 1);

            Query? middleWildcard = CreateWildcardQuery("exceptionType", $"{namespacePart}.*.{typeName}");
            if (middleWildcard != null)
            {
                queryBuilder.Add(middleWildcard, Occur.SHOULD);
            }

            // Also search just the type name
            queryBuilder.Add(new TermQuery(new Term("exceptionSimpleName", typeName.ToLowerInvariant())), Occur.SHOULD);
        }
    }

    private void AddSimpleNameQueries(BooleanQuery queryBuilder, string searchTerm)
    {
        // Search simple name field
        queryBuilder.Add(new TermQuery(new Term("exceptionSimpleName", searchTerm.ToLowerInvariant())), Occur.SHOULD);

        // Try exact match with common namespaces
        List<string> namespacedTypes = CommonExceptionNamespaces
            .Select(ns => $"{ns}.{searchTerm}")
            .ToList();

        foreach (string nsType in namespacedTypes)
        {
            queryBuilder.Add(new TermQuery(new Term("exceptionType", nsType)), Occur.SHOULD);
        }

        // Suffix wildcard for partial matches (e.g., "Exception" matches "ArgumentException")
        Query? suffixQuery = CreateWildcardQuery("exceptionType", $"*{searchTerm}");
        if (suffixQuery != null)
        {
            queryBuilder.Add(suffixQuery, Occur.SHOULD);
        }
    }

    private Query? CreateWildcardQuery(string fieldName, string pattern)
    {
        // Lucene doesn't allow leading wildcards by default
        if (pattern.StartsWith('*') || pattern.StartsWith('?'))
            return null;

        try
        {
            return new WildcardQuery(new Term(fieldName, pattern));
        }
        catch
        {
            return null;
        }
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