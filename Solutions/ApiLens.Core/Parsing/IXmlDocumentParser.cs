using System.Xml.Linq;
using ApiLens.Core.Models;

namespace ApiLens.Core.Parsing;

public interface IXmlDocumentParser
{
    ApiAssemblyInfo? ParseXmlFile(string filePath);
    ApiAssemblyInfo ParseAssembly(XDocument document);
    MemberInfo? ParseMember(XElement memberElement);
    ImmutableArray<MemberInfo> ParseMembers(XDocument document, string assemblyName);
}