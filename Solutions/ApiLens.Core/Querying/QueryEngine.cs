using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using Lucene.Net.Documents;

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

        List<Document> documents = indexManager.SearchByField("nameText", name.ToLowerInvariant(), maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    public List<MemberInfo> SearchByContent(string searchText, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchText);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        List<Document> documents = indexManager.SearchByField("content", searchText, maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    public List<MemberInfo> SearchByNamespace(string namespaceName, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        List<Document> documents = indexManager.SearchByField("namespaceText", namespaceName, maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    public MemberInfo? GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        List<Document> documents = indexManager.SearchByField("id", id, 1);
        return documents.Count > 0 ? ConvertDocumentToMember(documents[0]) : null;
    }

    public List<MemberInfo> GetByType(MemberType memberType, int maxResults)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        List<Document> documents = indexManager.SearchByField("memberType", memberType.ToString(), maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    public List<MemberInfo> GetTypeMembers(string typeName, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        // Search for members that belong to this type
        List<Document> documents = indexManager.SearchByField("fullName", typeName + ".", maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    public List<MemberInfo> SearchByAssembly(string assemblyName, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        List<Document> documents = indexManager.SearchByField("assembly", assemblyName, maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    private static List<MemberInfo> ConvertDocumentsToMembers(List<Document> documents)
    {
        List<MemberInfo> members = [];

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

    private static MemberInfo? ConvertDocumentToMember(Document doc)
    {
        string? id = doc.Get("id");
        string? memberTypeStr = doc.Get("memberType");

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(memberTypeStr))
        {
            return null;
        }

        if (!Enum.TryParse<MemberType>(memberTypeStr, out MemberType memberType))
        {
            return null;
        }

        // Extract code examples
        List<CodeExample> codeExamples = [];
        string[] codeExampleValues = doc.GetValues("codeExample");
        string[] codeExampleDescriptions = doc.GetValues("codeExampleDescription");

        for (int i = 0; i < codeExampleValues.Length; i++)
        {
            codeExamples.Add(new CodeExample
            {
                Code = codeExampleValues[i],
                Description = i < codeExampleDescriptions.Length ? codeExampleDescriptions[i] : "",
                Language = "csharp"
            });
        }

        // Extract exceptions
        List<ExceptionInfo> exceptions = [];
        string[] exceptionTypes = doc.GetValues("exceptionType");
        string[] exceptionConditions = doc.GetValues("exceptionCondition");

        for (int i = 0; i < exceptionTypes.Length; i++)
        {
            exceptions.Add(new ExceptionInfo
            {
                Type = exceptionTypes[i],
                Condition = i < exceptionConditions.Length ? exceptionConditions[i] : null
            });
        }

        // Extract parameters
        List<ParameterInfo> parameters = [];
        string[] parameterValues = doc.GetValues("parameter");
        string[] parameterDescriptions = doc.GetValues("parameterDescription");

        for (int i = 0; i < parameterValues.Length; i++)
        {
            string[] parts = parameterValues[i].Split(' ', 2);
            if (parts.Length == 2)
            {
                parameters.Add(new ParameterInfo
                {
                    Type = parts[0],
                    Name = parts[1],
                    Position = i,
                    Description = i < parameterDescriptions.Length ? parameterDescriptions[i] : null,
                    IsOptional = false,
                    IsParams = false,
                    IsOut = false,
                    IsRef = false
                });
            }
        }

        // Extract complexity metrics
        ComplexityMetrics? complexity = null;
        string? paramCountStr = doc.Get("parameterCount");
        string? cyclomaticStr = doc.Get("cyclomaticComplexity");
        string? lineCountStr = doc.Get("documentationLineCount");
        if (paramCountStr != null && cyclomaticStr != null && lineCountStr != null)
        {
            if (int.TryParse(paramCountStr, out int paramCount) &&
                int.TryParse(cyclomaticStr, out int cyclomatic) &&
                int.TryParse(lineCountStr, out int lineCount))
            {
                complexity = new ComplexityMetrics
                {
                    ParameterCount = paramCount,
                    CyclomaticComplexity = cyclomatic,
                    DocumentationLineCount = lineCount
                };
            }
        }

        return new MemberInfo
        {
            Id = id,
            MemberType = memberType,
            Name = doc.Get("name") ?? string.Empty,
            FullName = doc.Get("fullName") ?? string.Empty,
            Assembly = doc.Get("assembly") ?? string.Empty,
            Namespace = doc.Get("namespace") ?? string.Empty,
            Summary = doc.Get("summary"),
            Remarks = doc.Get("remarks"),
            CodeExamples = [.. codeExamples],
            Exceptions = [.. exceptions],
            Parameters = [.. parameters],
            Returns = doc.Get("returns"),
            SeeAlso = doc.Get("seeAlso"),
            Complexity = complexity,
            // Version tracking fields
            PackageId = NullIfEmpty(doc.Get("packageId")),
            PackageVersion = NullIfEmpty(doc.Get("packageVersion")),
            TargetFramework = NullIfEmpty(doc.Get("targetFramework")),
            IsFromNuGetCache = bool.TryParse(doc.Get("isFromNuGetCache"), out bool fromCache) && fromCache,
            SourceFilePath = NullIfEmpty(doc.Get("sourceFilePath"))
        };
    }

    public List<MemberInfo> SearchByCodeExample(string codePattern, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codePattern);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        List<Document> documents = indexManager.SearchByField("codeExample", codePattern, maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    public List<MemberInfo> GetByExceptionType(string exceptionType, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exceptionType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        List<Document> documents = indexManager.SearchByField("exceptionType", exceptionType, maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    public List<MemberInfo> GetByParameterCount(int min, int max, int maxResults)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(min);
        ArgumentOutOfRangeException.ThrowIfNegative(max);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        if (min > max)
        {
            throw new ArgumentException("Min parameter count cannot be greater than max parameter count");
        }

        List<Document> documents = indexManager.SearchByIntRange("parameterCount", min, max, maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public List<MemberInfo> GetMethodsWithExamples(int maxResults)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        // Search for documents that have the codeExample field
        List<Document> documents = indexManager.SearchByFieldExists("codeExample", maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    public List<MemberInfo> GetComplexMethods(int minComplexity, int maxResults)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minComplexity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        List<Document> documents = indexManager.SearchByIntRange("cyclomaticComplexity", minComplexity, int.MaxValue, maxResults);
        return ConvertDocumentsToMembers(documents);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
    }
}