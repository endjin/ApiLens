using System.Xml.Linq;
using ApiLens.Core.Models;

namespace ApiLens.Core.Parsing;

public class XmlDocumentParser : IXmlDocumentParser
{
    public ApiAssemblyInfo ParseAssembly(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        string? assemblyName = document.Root?
            .Element("assembly")?
            .Element("name")?
            .Value;

        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            throw new InvalidOperationException("Assembly name not found in XML document");
        }

        return new ApiAssemblyInfo
        {
            Name = assemblyName,
            Version = "0.0.0.0",
            Culture = "neutral"
        };
    }

    public MemberInfo? ParseMember(XElement memberElement)
    {
        ArgumentNullException.ThrowIfNull(memberElement);

        string? nameAttribute = memberElement.Attribute("name")?.Value;
        if (string.IsNullOrWhiteSpace(nameAttribute))
        {
            return null;
        }

        (MemberType? memberType, string id) = ParseMemberId(nameAttribute);
        if (memberType == null)
        {
            return null;
        }

        string summary = ExtractSummary(memberElement);
        string name = ExtractNameFromId(id, memberType.Value);
        string fullName = ExtractFullNameFromId(id, memberType.Value);
        string namespaceName = ExtractNamespaceFromId(id);

        // Extract new metadata
        ImmutableArray<CodeExample> codeExamples = ExtractCodeExamples(memberElement);
        ImmutableArray<ExceptionInfo> exceptions = ExtractExceptions(memberElement);
        ImmutableArray<ParameterInfo> parameters = ExtractParameters(memberElement, id);
        string? returns = ExtractReturns(memberElement);
        string? seeAlso = ExtractSeeAlso(memberElement);
        ComplexityMetrics? complexity = CalculateComplexity(memberElement, parameters);
        string? remarks = ExtractRemarks(memberElement);

        return new MemberInfo
        {
            Id = nameAttribute,
            MemberType = memberType.Value,
            Name = name,
            FullName = fullName,
            Assembly = string.Empty, // Will be set by the caller
            Namespace = namespaceName,
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks,
            CodeExamples = codeExamples,
            Exceptions = exceptions,
            Parameters = parameters,
            Returns = returns,
            SeeAlso = seeAlso,
            Complexity = complexity
        };
    }

    private static (MemberType? type, string id) ParseMemberId(string memberId)
    {
        if (string.IsNullOrWhiteSpace(memberId) || memberId.Length < 2 || memberId[1] != ':')
        {
            return (null, string.Empty);
        }

        MemberType? memberType = memberId[0] switch
        {
            'T' => MemberType.Type,
            'M' => MemberType.Method,
            'P' => MemberType.Property,
            'F' => MemberType.Field,
            'E' => MemberType.Event,
            _ => null
        };

        return (memberType, memberId[2..]);
    }

    private static string ExtractSummary(XElement memberElement)
    {
        string? summary = memberElement.Element("summary")?.Value;
        return summary?.Trim() ?? string.Empty;
    }

    private static string ExtractNameFromId(string id, MemberType memberType)
    {
        return memberType switch
        {
            MemberType.Type => ExtractTypeNameFromId(id),
            MemberType.Method => ExtractMethodNameFromId(id),
            MemberType.Property => ExtractPropertyNameFromId(id),
            MemberType.Field => ExtractFieldNameFromId(id),
            MemberType.Event => ExtractEventNameFromId(id),
            _ => id
        };
    }

    private static string ExtractTypeNameFromId(string id)
    {
        int lastDot = id.LastIndexOf('.');
        if (lastDot >= 0)
        {
            return id[(lastDot + 1)..];
        }
        return id;
    }

    private static string ExtractMethodNameFromId(string id)
    {
        int parenIndex = id.IndexOf('(');
        string withoutParams = parenIndex >= 0 ? id[..parenIndex] : id;

        int lastDot = withoutParams.LastIndexOf('.');
        if (lastDot >= 0)
        {
            return withoutParams[(lastDot + 1)..];
        }
        return withoutParams;
    }

    private static string ExtractPropertyNameFromId(string id)
    {
        int lastDot = id.LastIndexOf('.');
        if (lastDot >= 0)
        {
            return id[(lastDot + 1)..];
        }
        return id;
    }

    private static string ExtractFieldNameFromId(string id)
    {
        int lastDot = id.LastIndexOf('.');
        if (lastDot >= 0)
        {
            return id[(lastDot + 1)..];
        }
        return id;
    }

    private static string ExtractEventNameFromId(string id)
    {
        int lastDot = id.LastIndexOf('.');
        if (lastDot >= 0)
        {
            return id[(lastDot + 1)..];
        }
        return id;
    }

    private static string ExtractFullNameFromId(string id, MemberType memberType)
    {
        return memberType switch
        {
            MemberType.Method => ExtractMethodFullNameFromId(id),
            _ => ExtractTypeFullNameFromId(id)
        };
    }

    private static string ExtractMethodFullNameFromId(string id)
    {
        return id;
    }

    private static string ExtractTypeFullNameFromId(string id)
    {
        int genericIndex = id.IndexOf('`');
        if (genericIndex >= 0)
        {
            return id[..genericIndex];
        }
        return id;
    }

    private static string ExtractNamespaceFromId(string id)
    {
        int parenIndex = id.IndexOf('(');
        string withoutParams = parenIndex >= 0 ? id[..parenIndex] : id;

        int lastDot = withoutParams.LastIndexOf('.');
        if (lastDot >= 0)
        {
            return withoutParams[..lastDot];
        }
        return string.Empty;
    }

    public ImmutableArray<MemberInfo> ParseMembers(XDocument document, string assemblyName)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

        XElement? membersElement = document.Root?.Element("members");
        if (membersElement == null)
        {
            return [];
        }

        ImmutableArray<MemberInfo>.Builder builder = ImmutableArray.CreateBuilder<MemberInfo>();

        foreach (XElement memberElement in membersElement.Elements("member"))
        {
            MemberInfo? member = ParseMember(memberElement);
            if (member != null)
            {
                member = member with { Assembly = assemblyName };
                builder.Add(member);
            }
        }

        return builder.ToImmutable();
    }

    public ApiAssemblyInfo? ParseXmlFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            XDocument document = XDocument.Load(filePath);
            return ParseAssembly(document);
        }
        catch (System.Xml.XmlException)
        {
            // Return null if file is not valid XML
            return null;
        }
        catch (IOException)
        {
            // Return null if file cannot be read
            return null;
        }
    }

    private static ImmutableArray<CodeExample> ExtractCodeExamples(XElement memberElement)
    {
        List<CodeExample> examples = [];

        foreach (XElement exampleElement in memberElement.Elements("example"))
        {
            string description = "";
            string code = "";

            // Extract description (text before <code> element)
            XNode? firstNode = exampleElement.FirstNode;
            if (firstNode is XText textNode)
            {
                description = textNode.Value.Trim();
            }

            // Extract code
            XElement? codeElement = exampleElement.Element("code");
            if (codeElement != null)
            {
                // Preserve line breaks but normalize indentation
                string[] lines = codeElement.Value.Split('\n');
                List<string> trimmedLines = [];

                // Find minimum indentation (ignoring empty lines)
                int minIndent = int.MaxValue;
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        int indent = line.Length - line.TrimStart().Length;
                        minIndent = Math.Min(minIndent, indent);
                    }
                }

                // Remove common indentation
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        trimmedLines.Add("");
                    }
                    else if (line.Length > minIndent)
                    {
                        trimmedLines.Add(line[minIndent..]);
                    }
                    else
                    {
                        trimmedLines.Add(line.TrimStart());
                    }
                }

                code = string.Join("\n", trimmedLines).Trim();
            }

            if (!string.IsNullOrWhiteSpace(code))
            {
                examples.Add(new CodeExample
                {
                    Description = description,
                    Code = code,
                    Language = "csharp" // Default, could be extracted from attributes
                });
            }
        }

        return [.. examples];
    }

    private static ImmutableArray<ExceptionInfo> ExtractExceptions(XElement memberElement)
    {
        List<ExceptionInfo> exceptions = [];

        foreach (XElement exceptionElement in memberElement.Elements("exception"))
        {
            string? cref = exceptionElement.Attribute("cref")?.Value;
            if (!string.IsNullOrWhiteSpace(cref) && cref.StartsWith("T:"))
            {
                string typeName = cref[2..]; // Remove "T:" prefix
                string condition = exceptionElement.Value.Trim();

                exceptions.Add(new ExceptionInfo
                {
                    Type = typeName,
                    Condition = string.IsNullOrWhiteSpace(condition) ? null : condition
                });
            }
        }

        return [.. exceptions];
    }

    private static ImmutableArray<ParameterInfo> ExtractParameters(XElement memberElement, string memberId)
    {
        List<ParameterInfo> parameters = [];

        // Extract parameter types from member ID
        List<string> parameterTypes = ExtractParameterTypesFromId(memberId);

        // Extract parameter descriptions from XML
        Dictionary<string, string> paramDescriptions = [];
        foreach (XElement paramElement in memberElement.Elements("param"))
        {
            string? name = paramElement.Attribute("name")?.Value;
            if (!string.IsNullOrWhiteSpace(name))
            {
                paramDescriptions[name] = paramElement.Value.Trim();
            }
        }

        // Match parameter names with types
        int position = 0;
        foreach (XElement paramElement in memberElement.Elements("param"))
        {
            string? name = paramElement.Attribute("name")?.Value;
            if (!string.IsNullOrWhiteSpace(name))
            {
                string type = position < parameterTypes.Count ? parameterTypes[position] : "Unknown";
                string? description = paramDescriptions.GetValueOrDefault(name);

                parameters.Add(new ParameterInfo
                {
                    Name = name,
                    Type = type,
                    Position = position,
                    IsOptional = false, // Would need more info to determine
                    IsParams = false,
                    IsOut = false,
                    IsRef = false,
                    Description = description
                });

                position++;
            }
        }

        return [.. parameters];
    }

    private static List<string> ExtractParameterTypesFromId(string memberId)
    {
        List<string> types = [];

        int parenIndex = memberId.IndexOf('(');
        if (parenIndex >= 0 && memberId.EndsWith(')'))
        {
            string paramsPart = memberId[(parenIndex + 1)..^1];
            if (!string.IsNullOrWhiteSpace(paramsPart))
            {
                // Simple split by comma - this won't handle nested generics perfectly
                types.AddRange(paramsPart.Split(',').Select(p => p.Trim()));
            }
        }

        return types;
    }

    private static string? ExtractReturns(XElement memberElement)
    {
        XElement? returnsElement = memberElement.Element("returns");
        return returnsElement?.Value.Trim();
    }

    private static string? ExtractRemarks(XElement memberElement)
    {
        XElement? remarksElement = memberElement.Element("remarks");
        return remarksElement?.Value.Trim();
    }

    private static string? ExtractSeeAlso(XElement memberElement)
    {
        List<string> references = [];

        foreach (XElement seeAlsoElement in memberElement.Elements("seealso"))
        {
            string? cref = seeAlsoElement.Attribute("cref")?.Value;
            if (!string.IsNullOrWhiteSpace(cref))
            {
                // Extract the type/member name from the cref
                if (cref.Length > 2 && cref[1] == ':')
                {
                    string reference = cref[2..];
                    references.Add(reference);
                }
            }
        }

        return references.Count > 0 ? string.Join("; ", references) : null;
    }

    private static ComplexityMetrics? CalculateComplexity(XElement memberElement, ImmutableArray<ParameterInfo> parameters)
    {
        // Simple complexity calculation based on available data
        string allText = string.Join("\n",
            memberElement.Element("summary")?.Value ?? "",
            memberElement.Element("remarks")?.Value ?? "",
            string.Join("\n", memberElement.Elements("param").Select(p => p.Value)),
            memberElement.Element("returns")?.Value ?? ""
        );

        int lineCount = allText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        // Very basic cyclomatic complexity estimation based on keywords
        int complexity = 1; // Base complexity
        string[] complexityKeywords = ["if", "else", "switch", "case", "for", "while", "catch", "throw"];
        foreach (string keyword in complexityKeywords)
        {
            if (allText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                complexity++;
            }
        }

        return new ComplexityMetrics
        {
            ParameterCount = parameters.Length,
            CyclomaticComplexity = complexity,
            DocumentationLineCount = lineCount
        };
    }
}