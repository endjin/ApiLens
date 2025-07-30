using ApiLens.Core.Models;
using Lucene.Net.Documents;

namespace ApiLens.Core.Lucene;

public interface IDocumentBuilder
{
    Document BuildDocument(MemberInfo memberInfo);
    Document BuildDocument(TypeInfo typeInfo, string? summary = null, string? remarks = null);
    Document BuildDocument(MethodInfo methodInfo);
}