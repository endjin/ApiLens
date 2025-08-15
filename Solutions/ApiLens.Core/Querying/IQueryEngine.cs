using ApiLens.Core.Models;

namespace ApiLens.Core.Querying;

public interface IQueryEngine : IDisposable
{
    // Core search methods
    List<MemberInfo> SearchByName(string name, int maxResults);
    List<MemberInfo> SearchByContent(string searchText, int maxResults);
    List<MemberInfo> SearchByNamespace(string namespaceName, int maxResults);
    MemberInfo? GetById(string id);
    List<MemberInfo> GetByType(MemberType memberType, int maxResults);
    List<MemberInfo> GetTypeMembers(string typeName, int maxResults);
    List<MemberInfo> SearchByDeclaringType(string declaringType, int maxResults);
    List<MemberInfo> SearchByAssembly(string assemblyName, int maxResults);
    List<MemberInfo> SearchByPackage(string packageId, int maxResults);

    // Specialized queries for rich metadata
    List<MemberInfo> SearchByCodeExample(string codePattern, int maxResults);
    List<MemberInfo> GetByExceptionType(string exceptionType, int maxResults);
    List<MemberInfo> GetByParameterCount(int min, int max, int maxResults);
    List<MemberInfo> GetMethodsWithExamples(int maxResults);
    List<MemberInfo> GetComplexMethods(int minComplexity, int maxResults);

    // Assembly and type filtering
    List<MemberInfo> ListTypesFromAssembly(string assemblyPattern, int maxResults);
    List<MemberInfo> ListTypesFromPackage(string packagePattern, int maxResults);
    List<MemberInfo> SearchByNamespacePattern(string namespacePattern, int maxResults);
    List<MemberInfo> SearchByAssemblyAndType(string assemblyPattern, MemberType? memberType, int maxResults);

    // Advanced filtering with wildcards
    List<MemberInfo> SearchWithFilters(string namePattern, MemberType? memberType,
        string? namespacePattern, string? assemblyPattern, int maxResults);
}