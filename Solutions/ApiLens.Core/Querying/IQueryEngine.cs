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
    List<MemberInfo> SearchByAssembly(string assemblyName, int maxResults);

    // Specialized queries for rich metadata
    List<MemberInfo> SearchByCodeExample(string codePattern, int maxResults);
    List<MemberInfo> GetByExceptionType(string exceptionType, int maxResults);
    List<MemberInfo> GetByParameterCount(int min, int max, int maxResults);
    List<MemberInfo> GetMethodsWithExamples(int maxResults);
    List<MemberInfo> GetComplexMethods(int minComplexity, int maxResults);
}