using System.Xml.Linq;
using ApiLens.Core.Models;

namespace ApiLens.Core.Parsing;

public interface IXmlDocumentParser
{
    // High-performance streaming operations
    IAsyncEnumerable<MemberInfo>
        ParseXmlFileStreamAsync(string filePath, CancellationToken cancellationToken = default);

    Task<BatchParseResult> ParseXmlFilesAsync(IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default);

    // Legacy synchronous operations (for compatibility)
    ApiAssemblyInfo ParseAssembly(XDocument document);
    MemberInfo? ParseMember(XElement memberElement);
    ImmutableArray<MemberInfo> ParseMembers(XDocument document, string assemblyName);
}