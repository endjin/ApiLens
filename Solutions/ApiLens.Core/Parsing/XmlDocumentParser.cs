using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ApiLens.Core.Infrastructure;
using ApiLens.Core.Models;

namespace ApiLens.Core.Parsing;

public sealed class XmlDocumentParser : IXmlDocumentParser
{
    private readonly StringInternCache stringCache;
    private readonly ObjectPool<StringBuilder> stringBuilderPool;

    // Pre-interned common strings
    private readonly string memberElementName = "member";
    private readonly string nameAttributeName = "name";
    private readonly string assemblyElementName = "assembly";

    public XmlDocumentParser()
    {
        stringCache = new StringInternCache();
        stringBuilderPool = new ObjectPool<StringBuilder>(
            () => new StringBuilder(1024),
            sb => sb.Clear(),
            maxSize: 64);
    }

    public async IAsyncEnumerable<MemberInfo> ParseXmlFileStreamAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        XmlReaderSettings settings = new()
        {
            Async = true,
            IgnoreWhitespace = true,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        };

        using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        using XmlReader reader = XmlReader.Create(fileStream, settings);

        string? assemblyName = null;

        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.Name == assemblyElementName && reader.Depth == 1)
                {
                    assemblyName = await ReadAssemblyNameAsync(reader);
                }
                else if (reader.Name == memberElementName && reader.Depth == 2)
                {
                    MemberInfo? member = await ParseMemberAsync(reader, assemblyName ?? "Unknown");
                    if (member != null)
                    {
                        yield return member;
                    }
                }
            }
        }
    }

    public async Task<BatchParseResult> ParseXmlFilesAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<MemberInfo> members = [];
        List<string> errors = [];
        int successfulFiles = 0;
        int failedFiles = 0;
        long bytesProcessed = 0;

        foreach (string filePath in filePaths)
        {
            try
            {
                FileInfo fileInfo = new(filePath);
                bytesProcessed += fileInfo.Length;

                await foreach (MemberInfo member in ParseXmlFileStreamAsync(filePath, cancellationToken))
                {
                    members.Add(member);
                }

                successfulFiles++;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                failedFiles++;
                errors.Add($"Failed to parse {filePath}: {ex.Message}");
            }
        }

        return new BatchParseResult
        {
            TotalFiles = successfulFiles + failedFiles,
            SuccessfulFiles = successfulFiles,
            FailedFiles = failedFiles,
            TotalMembers = members.Count,
            ElapsedTime = stopwatch.Elapsed,
            BytesProcessed = bytesProcessed,
            Members = [.. members],
            Errors = [.. errors]
        };
    }

    private async Task<string?> ReadAssemblyNameAsync(XmlReader reader)
    {
        while (await reader.ReadAsync())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == nameAttributeName)
            {
                return await reader.ReadElementContentAsStringAsync();
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == assemblyElementName)
            {
                break;
            }
        }
        return null;
    }

    private async Task<MemberInfo?> ParseMemberAsync(XmlReader reader, string assemblyName)
    {
        string? nameAttribute = reader.GetAttribute(nameAttributeName);
        if (string.IsNullOrEmpty(nameAttribute))
            return null;

        (MemberType? memberType, string id) = ParseMemberId(nameAttribute);
        if (memberType == null)
            return null;

        StringBuilder sb = stringBuilderPool.Rent();
        string? summary = null;
        string? remarks = null;
        string? returns = null;
        string? seeAlso = null;
        List<ParameterInfo> parameters = [];
        List<ExceptionInfo> exceptions = [];
        List<CodeExample> examples = [];

        try
        {
            XmlReader subtree = reader.ReadSubtree();
            while (await subtree.ReadAsync())
            {
                if (subtree.NodeType == XmlNodeType.Element)
                {
                    switch (subtree.Name)
                    {
                        case "summary":
                            summary = await ReadElementContentAsync(subtree, sb);
                            break;
                        case "remarks":
                            remarks = await ReadElementContentAsync(subtree, sb);
                            break;
                        case "returns":
                            returns = await ReadElementContentAsync(subtree, sb);
                            break;
                        case "param":
                            ParameterInfo? param = await ParseParameterAsync(subtree, sb);
                            if (param != null)
                                parameters.Add(param);
                            break;
                        case "exception":
                            ExceptionInfo? exception = await ParseExceptionAsync(subtree, sb);
                            if (exception != null)
                                exceptions.Add(exception);
                            break;
                        case "example":
                            CodeExample? example = await ParseExampleAsync(subtree, sb);
                            if (example != null)
                                examples.Add(example);
                            break;
                        case "seealso":
                            seeAlso = subtree.GetAttribute("cref") ?? await ReadElementContentAsync(subtree, sb);
                            break;
                    }
                }
            }

            string name = ExtractNameFromId(id, memberType.Value);
            string fullName = ExtractFullNameFromId(id, memberType.Value);
            string namespaceName = ExtractNamespaceFromId(id, memberType.Value);

            return new MemberInfo
            {
                Id = stringCache.GetOrAdd(nameAttribute),
                MemberType = memberType.Value,
                Name = stringCache.GetOrAdd(name),
                FullName = stringCache.GetOrAdd(fullName),
                Assembly = stringCache.GetOrAdd(assemblyName),
                Namespace = stringCache.GetOrAdd(namespaceName),
                Summary = summary,
                Remarks = remarks,
                Returns = returns,
                SeeAlso = seeAlso,
                Parameters = [.. parameters],
                Exceptions = [.. exceptions],
                CodeExamples = [.. examples],
                IndexedAt = DateTime.UtcNow
            };
        }
        finally
        {
            stringBuilderPool.Return(sb);
        }
    }

    private static async Task<string> ReadElementContentAsync(XmlReader reader, StringBuilder sb)
    {
        sb.Clear();

        while (await reader.ReadAsync())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                    sb.Append(reader.Value);
                    break;
                case XmlNodeType.Element:
                    if (reader.Name is "see" or "paramref")
                    {
                        string? cref = reader.GetAttribute("cref") ?? reader.GetAttribute("name");
                        if (!string.IsNullOrEmpty(cref))
                        {
                            sb.Append(' ').Append(cref).Append(' ');
                        }
                    }
                    break;
                case XmlNodeType.EndElement:
                    return NormalizeWhitespace(sb.ToString());
            }
        }

        return NormalizeWhitespace(sb.ToString());
    }

    private async Task<ParameterInfo?> ParseParameterAsync(XmlReader reader, StringBuilder sb)
    {
        string? name = reader.GetAttribute(nameAttributeName);
        if (string.IsNullOrEmpty(name))
            return null;

        string description = await ReadElementContentAsync(reader, sb);

        return new ParameterInfo
        {
            Name = stringCache.GetOrAdd(name),
            Type = "", // Would need to be extracted from method signature
            Description = description,
            Position = 0, // Would need to be determined from method signature
            IsOptional = false,
            IsParams = false,
            IsOut = false,
            IsRef = false
        };
    }

    private async Task<ExceptionInfo?> ParseExceptionAsync(XmlReader reader, StringBuilder sb)
    {
        string? cref = reader.GetAttribute("cref");
        if (string.IsNullOrEmpty(cref))
            return null;

        string condition = await ReadElementContentAsync(reader, sb);

        return new ExceptionInfo
        {
            Type = stringCache.GetOrAdd(ExtractTypeNameFromCref(cref)),
            Condition = condition
        };
    }

    private static async Task<CodeExample?> ParseExampleAsync(XmlReader reader, StringBuilder sb)
    {
        string code = await ReadElementContentAsync(reader, sb);
        if (string.IsNullOrEmpty(code))
            return null;

        return new CodeExample
        {
            Code = code,
            Description = string.Empty
        };
    }

    private static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Simple normalization - replace multiple whitespaces with single space
        return System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\s+", " ");
    }

    private static string ExtractTypeNameFromCref(string cref)
    {
        // Remove the prefix (T:, M:, etc.) if present
        if (cref.Length > 2 && cref[1] == ':')
            return cref[2..];
        return cref;
    }

    // Legacy synchronous methods for compatibility
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
            return null;

        (MemberType? memberType, string id) = ParseMemberId(nameAttribute);
        if (memberType == null)
            return null;

        string summary = ExtractSummary(memberElement);
        string name = ExtractNameFromId(id, memberType.Value);
        string fullName = ExtractFullNameFromId(id, memberType.Value);
        string namespaceName = ExtractNamespaceFromId(id, memberType.Value);

        // Extract additional documentation elements
        string? returns = ExtractElementText(memberElement, "returns");
        string? remarks = ExtractElementText(memberElement, "remarks");

        // Extract all seealso references
        List<string?> seeAlsoElements = memberElement.Elements("seealso")
            .Select(e => e.Attribute("cref")?.Value)
            .Where(cref => !string.IsNullOrEmpty(cref))
            .ToList();
        string? seeAlso = seeAlsoElements.Count > 0 ? string.Join(" ", seeAlsoElements) : null;

        // Extract code examples
        List<CodeExample> examples = [];
        foreach (XElement exampleElement in memberElement.Elements("example"))
        {
            XElement? codeElement = exampleElement.Element("code");
            if (codeElement != null)
            {
                // Extract code and dedent it
                // First check if the raw code has the issue
                string rawValue = codeElement.Value;

                // Simple approach: if the code looks like it has common indentation, remove it
                string code = RemoveCommonLeadingWhitespace(rawValue);

                // Extract description (text before code element)
                string description = "";
                XNode? textNode = exampleElement.FirstNode;
                while (textNode != null && textNode != codeElement)
                {
                    if (textNode.NodeType == XmlNodeType.Text)
                    {
                        description += textNode.ToString();
                    }
                    textNode = textNode.NextNode;
                }
                description = description.Trim();

                examples.Add(new CodeExample
                {
                    Code = code,
                    Description = description
                });
            }
            else
            {
                // If no code element, treat entire content as code
                string code = exampleElement.Value.Trim();
                if (!string.IsNullOrEmpty(code))
                {
                    examples.Add(new CodeExample
                    {
                        Code = code,
                        Description = string.Empty
                    });
                }
            }
        }

        // Extract parameters
        List<ParameterInfo> parameters = [];
        List<string> parameterTypes = ExtractParameterTypesFromId(id, memberType.Value);

        foreach (XElement paramElement in memberElement.Elements("param"))
        {
            string? paramName = paramElement.Attribute("name")?.Value;
            if (!string.IsNullOrEmpty(paramName))
            {
                int paramIndex = parameters.Count;
                parameters.Add(new ParameterInfo
                {
                    Name = paramName,
                    Type = paramIndex < parameterTypes.Count ? parameterTypes[paramIndex] : "",
                    Description = NormalizeWhitespace(paramElement.Value),
                    Position = paramIndex,
                    IsOptional = false,
                    IsParams = false,
                    IsOut = false,
                    IsRef = false
                });
            }
        }

        // Extract exceptions
        List<ExceptionInfo> exceptions = [];
        foreach (XElement exceptionElement in memberElement.Elements("exception"))
        {
            string? cref = exceptionElement.Attribute("cref")?.Value;
            if (!string.IsNullOrEmpty(cref))
            {
                exceptions.Add(new ExceptionInfo
                {
                    Type = ExtractTypeNameFromCref(cref),
                    Condition = NormalizeWhitespace(exceptionElement.Value)
                });
            }
        }

        // Calculate complexity metrics
        ComplexityMetrics? complexity = null;
        if (memberType.Value == MemberType.Method)
        {
            int docLineCount = CalculateDocumentationLineCount(memberElement);
            complexity = new ComplexityMetrics
            {
                ParameterCount = parameters.Count,
                CyclomaticComplexity = 1, // Base complexity, would need code analysis for real value
                DocumentationLineCount = docLineCount
            };
        }

        return new MemberInfo
        {
            Id = nameAttribute,
            MemberType = memberType.Value,
            Name = name,
            FullName = fullName,
            Assembly = "Unknown",
            Namespace = namespaceName,
            Summary = summary,
            Remarks = remarks,
            Returns = returns,
            SeeAlso = seeAlso,
            Parameters = [.. parameters],
            Exceptions = [.. exceptions],
            CodeExamples = [.. examples],
            Complexity = complexity
        };
    }

    private static (MemberType? Type, string Id) ParseMemberId(string nameAttribute)
    {
        if (string.IsNullOrWhiteSpace(nameAttribute) || nameAttribute.Length < 2)
            return (null, string.Empty);

        char prefix = nameAttribute[0];
        string id = nameAttribute[2..]; // Skip "X:"

        MemberType? memberType = prefix switch
        {
            'T' => MemberType.Type,
            'M' => MemberType.Method,
            'P' => MemberType.Property,
            'F' => MemberType.Field,
            'E' => MemberType.Event,
            _ => (MemberType?)null
        };

        return (memberType, id);
    }

    private static string ExtractSummary(XElement memberElement)
    {
        XElement? summaryElement = memberElement.Element("summary");
        if (summaryElement == null)
            return string.Empty;

        return NormalizeWhitespace(summaryElement.Value);
    }

    private static string? ExtractElementText(XElement parentElement, string elementName)
    {
        XElement? element = parentElement.Element(elementName);
        if (element == null)
            return null;

        return NormalizeWhitespace(element.Value);
    }

    private static string ExtractNameFromId(string id, MemberType memberType)
    {
        if (memberType == MemberType.Type)
        {
            int lastDot = id.LastIndexOf('.');
            return lastDot >= 0 ? id[(lastDot + 1)..] : id;
        }

        // For members, extract the member name
        int parenIndex = id.IndexOf('(');
        int memberStart = id.LastIndexOf('.', parenIndex >= 0 ? parenIndex : id.Length - 1);

        if (memberStart >= 0)
        {
            string memberName = parenIndex >= 0
                ? id.Substring(memberStart + 1, parenIndex - memberStart - 1)
                : id[(memberStart + 1)..];

            // Handle special names
            return memberName switch
            {
                "#ctor" => ".ctor",
                "#cctor" => ".cctor",
                _ => memberName
            };
        }

        return id;
    }

    private static string ExtractFullNameFromId(string id, MemberType memberType)
    {
        // For all member types, return the full ID including parameters for methods
        return id;
    }

    private static string ExtractNamespaceFromId(string id, MemberType memberType)
    {
        // Find the last dot before any generic or method parameters
        int parenIndex = id.IndexOf('(');
        int genericIndex = id.IndexOf('`');

        int searchEnd = id.Length;
        if (parenIndex >= 0)
            searchEnd = Math.Min(searchEnd, parenIndex);
        if (genericIndex >= 0)
            searchEnd = Math.Min(searchEnd, genericIndex);

        int lastDot = id.LastIndexOf('.', searchEnd - 1);
        if (lastDot <= 0)
            return string.Empty;

        if (memberType == MemberType.Type)
        {
            // For types, everything before the last dot is the namespace
            return id[..lastDot];
        }
        else
        {
            // For members, we need to find the type's namespace
            // which is everything before the second-to-last dot
            int secondLastDot = id.LastIndexOf('.', lastDot - 1);
            return secondLastDot >= 0 ? id[..secondLastDot] : string.Empty;
        }
    }

    private static int CalculateDocumentationLineCount(XElement memberElement)
    {
        // Count non-empty text lines in all documentation elements
        int lineCount = 0;

        foreach (XElement element in memberElement.Elements())
        {
            string text = element.Value;
            if (!string.IsNullOrWhiteSpace(text))
            {
                // Count actual lines in the text
                lineCount += text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            }
        }

        return lineCount;
    }

    private static List<string> ExtractParameterTypesFromId(string id, MemberType memberType)
    {
        List<string> types = [];

        if (memberType != MemberType.Method)
            return types;

        // Find the parameter section in parentheses
        int parenStart = id.IndexOf('(');
        int parenEnd = id.LastIndexOf(')');

        if (parenStart >= 0 && parenEnd > parenStart)
        {
            string paramSection = id.Substring(parenStart + 1, parenEnd - parenStart - 1);
            if (!string.IsNullOrEmpty(paramSection))
            {
                // Split by comma, handling nested generics
                List<string> paramTypes = SplitParameterTypes(paramSection);
                types.AddRange(paramTypes);
            }
        }

        return types;
    }

    private static List<string> SplitParameterTypes(string paramSection)
    {
        List<string> types = [];
        StringBuilder currentType = new();
        int depth = 0;

        foreach (char c in paramSection)
        {
            if (c == ',' && depth == 0)
            {
                types.Add(currentType.ToString().Trim());
                currentType.Clear();
            }
            else
            {
                if (c is '{' or '<')
                    depth++;
                else if (c is '}' or '>')
                    depth--;
                currentType.Append(c);
            }
        }

        if (currentType.Length > 0)
            types.Add(currentType.ToString().Trim());

        return types;
    }

    private static string DedentCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return string.Empty;

        // Split into lines, preserving empty lines
        // Handle both Windows and Unix line endings
        code = code.Replace("\r\n", "\n");
        string[] lines = code.Split('\n');

        // Find first and last non-empty lines
        int firstNonEmpty = -1;
        int lastNonEmpty = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                if (firstNonEmpty == -1) firstNonEmpty = i;
                lastNonEmpty = i;
            }
        }

        if (firstNonEmpty == -1)
            return string.Empty;

        // Trim to relevant lines
        List<string> relevantLines = [];
        for (int i = firstNonEmpty; i <= lastNonEmpty; i++)
        {
            relevantLines.Add(lines[i]);
        }

        // Find minimum indentation
        int minIndent = int.MaxValue;
        foreach (string line in relevantLines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            int indent = 0;
            foreach (char c in line)
            {
                if (c == ' ')
                    indent++;
                else if (c == '\t')
                    indent += 4;
                else
                    break;
            }

            minIndent = Math.Min(minIndent, indent);
        }

        if (minIndent is 0 or int.MaxValue)
            return string.Join('\n', relevantLines);

        // Remove minimum indentation from each line
        List<string> result = [];
        foreach (string line in relevantLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                result.Add(string.Empty);
            }
            else if (line.Length >= minIndent)
            {
                // Remove exactly minIndent spaces/tabs
                int toRemove = minIndent;
                int pos = 0;
                while (pos < line.Length && toRemove > 0)
                {
                    if (line[pos] == ' ')
                    {
                        toRemove--;
                        pos++;
                    }
                    else if (line[pos] == '\t')
                    {
                        toRemove -= 4;
                        pos++;
                    }
                    else
                    {
                        break;
                    }
                }
                result.Add(line.Substring(pos));
            }
            else
            {
                result.Add(line);
            }
        }

        return string.Join('\n', result);
    }

    private static string RemoveCommonLeadingWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Trim the text first to remove leading/trailing whitespace including newlines
        text = text.Trim();

        string[] lines = text.Split('\n');
        if (lines.Length == 0)
            return string.Empty;

        // Special case: if there's only one line, just return it
        if (lines.Length == 1)
            return text;

        // Find the minimum indentation among lines that have indentation
        // (skip lines with 0 indentation as they're likely already at the left margin)
        int minIndent = int.MaxValue;
        bool hasIndentedLines = false;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            int indent = 0;
            foreach (char c in line)
            {
                if (c == ' ')
                    indent++;
                else
                    break;
            }

            // Only consider lines with some indentation
            if (indent > 0)
            {
                minIndent = Math.Min(minIndent, indent);
                hasIndentedLines = true;
            }
        }

        // If no lines have indentation, or we couldn't find a common indent, return as-is
        if (!hasIndentedLines || minIndent == int.MaxValue)
            return text;

        // Remove the common indentation from indented lines only
        for (int i = 0; i < lines.Length; i++)
        {
            // Count the indentation of this line
            int lineIndent = 0;
            foreach (char c in lines[i])
            {
                if (c == ' ')
                    lineIndent++;
                else
                    break;
            }

            // Only dedent lines that have at least the minimum indentation
            if (lineIndent >= minIndent)
            {
                lines[i] = lines[i].Substring(minIndent);
            }
        }

        // Join lines back together
        return string.Join('\n', lines);
    }

    /// <summary>
    /// Parses all members from an XML document (for backward compatibility with tests).
    /// </summary>
    public ImmutableArray<MemberInfo> ParseMembers(XDocument document, string assemblyName)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(assemblyName);

        List<MemberInfo> members = [];
        XElement? membersElement = document.Root?.Element("members");

        if (membersElement != null)
        {
            foreach (XElement memberElement in membersElement.Elements("member"))
            {
                MemberInfo? member = ParseMember(memberElement);
                if (member != null)
                {
                    // Update the assembly name
                    member = member with { Assembly = assemblyName };
                    members.Add(member);
                }
            }
        }

        return [.. members];
    }
}