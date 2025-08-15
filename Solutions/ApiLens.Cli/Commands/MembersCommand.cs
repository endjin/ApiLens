using System.ComponentModel;
using System.Text.Json;
using ApiLens.Cli.Services;
using ApiLens.Core.Formatting;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;

namespace ApiLens.Cli.Commands;

public class MembersCommand : Command<MembersCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JsonSanitizer.CreateSafeJsonEncoder(),
        Converters = { new SanitizingJsonConverterFactory() }
    };

    private readonly ILuceneIndexManagerFactory indexManagerFactory;
    private readonly IQueryEngineFactory queryEngineFactory;

    public MembersCommand(
        ILuceneIndexManagerFactory indexManagerFactory,
        IQueryEngineFactory queryEngineFactory)
    {
        ArgumentNullException.ThrowIfNull(indexManagerFactory);
        ArgumentNullException.ThrowIfNull(queryEngineFactory);

        this.indexManagerFactory = indexManagerFactory;
        this.queryEngineFactory = queryEngineFactory;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        try
        {
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(settings.IndexPath);
            using IQueryEngine queryEngine = queryEngineFactory.Create(indexManager);

            MetadataService metadataService = new();
            metadataService.StartTiming();

            // First, find the type to get its full name
            var typeResults = queryEngine.SearchByName(settings.TypeName, 10);
            var targetType = typeResults.FirstOrDefault(m => 
                m.MemberType == MemberType.Type && 
                (m.Name.Equals(settings.TypeName, StringComparison.OrdinalIgnoreCase) ||
                 m.FullName.Equals(settings.TypeName, StringComparison.OrdinalIgnoreCase)));

            if (targetType == null)
            {
                // Try searching with wildcards if exact match fails
                typeResults = queryEngine.SearchWithFilters($"*{settings.TypeName}*", MemberType.Type, null, null, 10);
                targetType = typeResults.FirstOrDefault();
                
                if (targetType == null)
                {
                    if (settings.Format == OutputFormat.Json)
                    {
                        OutputJson([], null, indexManager, metadataService, settings);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Type '{settings.TypeName}' not found.[/]");
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine("[dim]Try using the full type name (e.g., 'System.String' instead of 'String')[/]");
                    }
                    return 1;
                }
            }

            // Now find all members of this type
            // Use declaringType field for accurate member retrieval
            var allMembers = queryEngine.SearchByDeclaringType(targetType.FullName, settings.MaxResults);
            
            // If no results, try with GetTypeMembers as fallback for backward compatibility
            if (allMembers.Count == 0)
            {
                allMembers = queryEngine.GetTypeMembers(targetType.FullName, settings.MaxResults);
            }
            
            // Apply deduplication if requested
            if (settings.Distinct)
            {
                var deduplicationService = new ResultDeduplicationService();
                allMembers = deduplicationService.DeduplicateResults(allMembers, true);
            }

            // Filter to exclude the type itself unless requested
            if (!settings.IncludeType)
            {
                allMembers = allMembers.Where(m => m.Id != targetType.Id).ToList();
            }

            // Group by member type
            var groupedMembers = allMembers
                .GroupBy(m => m.MemberType)
                .OrderBy(g => GetMemberTypeOrder(g.Key));

            switch (settings.Format)
            {
                case OutputFormat.Json:
                    OutputJson(allMembers, targetType, indexManager, metadataService, settings);
                    break;
                case OutputFormat.Table:
                    OutputTable(targetType, groupedMembers, settings);
                    break;
                case OutputFormat.Markdown:
                    OutputMarkdown(targetType, groupedMembers, settings);
                    break;
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static int GetMemberTypeOrder(MemberType memberType)
    {
        return memberType switch
        {
            MemberType.Type => 0,
            MemberType.Property => 1,
            MemberType.Method => 2,
            MemberType.Field => 3,
            MemberType.Event => 4,
            _ => 99
        };
    }

    private static void OutputJson(List<MemberInfo> members, MemberInfo? targetType, 
        ILuceneIndexManager indexManager, MetadataService metadataService, Settings settings)
    {
        var response = new
        {
            Type = targetType,
            Members = members.GroupBy(m => m.MemberType.ToString())
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(m => new
                    {
                        m.Name,
                        m.FullName,
                        m.Summary,
                        m.Parameters,
                        m.Returns,
                        m.MemberType,
                        m.PackageVersion,
                        m.TargetFramework
                    }).ToList()
                ),
            Metadata = metadataService.BuildMetadata(members, indexManager, 
                settings.TypeName, "members")
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);
        
        var originalWidth = AnsiConsole.Profile.Width;
        AnsiConsole.Profile.Width = int.MaxValue;
        AnsiConsole.WriteLine(json);
        AnsiConsole.Profile.Width = originalWidth;
    }

    private static void OutputTable(MemberInfo targetType, 
        IEnumerable<IGrouping<MemberType, MemberInfo>> groupedMembers, Settings settings)
    {
        AnsiConsole.Write(new Rule($"[bold yellow]{Markup.Escape(targetType.FullName)}[/]")
        {
            Justification = Justify.Left
        });

        if (!string.IsNullOrWhiteSpace(targetType.Summary))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(targetType.Summary)}[/]");
        }

        AnsiConsole.WriteLine();

        foreach (var group in groupedMembers)
        {
            var memberTypePlural = group.Key switch
            {
                MemberType.Property => "Properties",
                MemberType.Method => "Methods",
                MemberType.Field => "Fields",
                MemberType.Event => "Events",
                _ => $"{group.Key}s"
            };

            AnsiConsole.MarkupLine($"[bold cyan]{memberTypePlural}[/] ({group.Count()})");
            
            var table = new Table();
            table.AddColumn("Name");
            
            if (group.Key == MemberType.Method)
            {
                table.AddColumn("Parameters");
                table.AddColumn("Returns");
            }
            else if (group.Key == MemberType.Property || group.Key == MemberType.Field)
            {
                table.AddColumn("Type");
            }
            
            if (settings.ShowSummary)
            {
                table.AddColumn("Summary");
            }

            foreach (var member in group.Take(settings.MaxPerType))
            {
                var row = new List<string> { Markup.Escape(FormatMemberName(member)) };
                
                if (group.Key == MemberType.Method)
                {
                    row.Add(Markup.Escape(FormatParameters(member)));
                    row.Add(Markup.Escape(member.ReturnType ?? ExtractTypeFromMember(member)));
                }
                else if (group.Key == MemberType.Property || group.Key == MemberType.Field)
                {
                    // Extract type from the signature if available
                    var type = ExtractTypeFromMember(member);
                    row.Add(Markup.Escape(type));
                }
                
                if (settings.ShowSummary && !string.IsNullOrWhiteSpace(member.Summary))
                {
                    var summary = member.Summary.Length > 100 
                        ? member.Summary[..97] + "..." 
                        : member.Summary;
                    row.Add(Markup.Escape(summary));
                }
                
                table.AddRow(row.ToArray());
            }

            AnsiConsole.Write(table);
            
            if (group.Count() > settings.MaxPerType)
            {
                AnsiConsole.MarkupLine($"[dim]  ... and {group.Count() - settings.MaxPerType} more[/]");
            }
            
            AnsiConsole.WriteLine();
        }

        var totalMembers = groupedMembers.Sum(g => g.Count());
        AnsiConsole.MarkupLine($"[green]Total: {totalMembers} member(s)[/]");
    }

    private static void OutputMarkdown(MemberInfo targetType,
        IEnumerable<IGrouping<MemberType, MemberInfo>> groupedMembers, Settings settings)
    {
        AnsiConsole.WriteLine($"# {targetType.FullName}");
        AnsiConsole.WriteLine();
        
        if (!string.IsNullOrWhiteSpace(targetType.Summary))
        {
            AnsiConsole.WriteLine(targetType.Summary);
            AnsiConsole.WriteLine();
        }

        foreach (var group in groupedMembers)
        {
            var memberTypePlural = group.Key switch
            {
                MemberType.Property => "Properties",
                MemberType.Method => "Methods",
                MemberType.Field => "Fields",
                MemberType.Event => "Events",
                _ => $"{group.Key}s"
            };

            AnsiConsole.WriteLine($"## {memberTypePlural}");
            AnsiConsole.WriteLine();

            foreach (var member in group.Take(settings.MaxPerType))
            {
                AnsiConsole.WriteLine($"### {FormatMemberName(member)}");
                
                if (group.Key == MemberType.Method)
                {
                    AnsiConsole.WriteLine($"- **Parameters**: {FormatParameters(member)}");
                    AnsiConsole.WriteLine($"- **Returns**: {member.ReturnType ?? ExtractTypeFromMember(member)}");
                }
                
                if (!string.IsNullOrWhiteSpace(member.Summary))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.WriteLine("**Summary:**");
                    AnsiConsole.WriteLine(member.Summary);
                }
                
                AnsiConsole.WriteLine();
            }
        }
    }

    private static string FormatMemberName(MemberInfo member)
    {
        // Use the Name property directly as it's now properly extracted
        var name = member.Name;
        
        // Add modifiers for methods
        if (member.MemberType == MemberType.Method)
        {
            var modifiers = new List<string>();
            if (member.IsStatic) modifiers.Add("[static]");
            if (member.IsAsync) modifiers.Add("[async]");
            if (member.IsExtension) modifiers.Add("[ext]");
            
            if (modifiers.Any())
                return $"{string.Join(" ", modifiers)} {name}";
        }
        
        return name;
    }

    private static string FormatParameters(MemberInfo member)
    {
        if (member.Parameters.Length == 0)
            return "()";
        
        var paramStrings = member.Parameters.Select(p => 
        {
            var type = !string.IsNullOrWhiteSpace(p.Type) ? p.Type : "object";
            return $"{GenericTypeFormatter.FormatTypeName(type)} {p.Name}";
        });
        return $"({string.Join(", ", paramStrings)})";
    }

    private static string ExtractTypeFromMember(MemberInfo member)
    {
        // Use the ReturnType property if available
        if (!string.IsNullOrWhiteSpace(member.ReturnType))
            return member.ReturnType;
            
        // For properties and fields, try to extract from Returns description
        if (!string.IsNullOrWhiteSpace(member.Returns))
        {
            // Try to extract type from the beginning of the returns description
            var words = member.Returns.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0 && char.IsUpper(words[0][0]))
                return words[0].TrimEnd('.', ',');
        }
        
        // Fallback
        return member.MemberType == MemberType.Method ? "void" : "object";
    }

    public sealed class Settings : CommandSettings
    {
        [Description("The name of the type to explore (e.g., 'IndexWriter' or 'System.String')")]
        [CommandArgument(0, "<type-name>")]
        public string TypeName { get; init; } = string.Empty;

        [Description("Path to the Lucene index directory")]
        [CommandOption("-i|--index")]
        public string IndexPath { get; init; } = "./index";

        [Description("Maximum total results to return")]
        [CommandOption("-m|--max")]
        public int MaxResults { get; init; } = 1000;

        [Description("Maximum results per member type (e.g., max 20 methods)")]
        [CommandOption("--max-per-type")]
        public int MaxPerType { get; init; } = 20;

        [Description("Output format")]
        [CommandOption("-f|--format")]
        public OutputFormat Format { get; init; } = OutputFormat.Table;

        [Description("Show member summaries in output")]
        [CommandOption("--show-summary")]
        public bool ShowSummary { get; init; }

        [Description("Include the type itself in the results")]
        [CommandOption("--include-type")]
        public bool IncludeType { get; init; }

        [Description("Show only distinct members (deduplicate across frameworks)")]
        [CommandOption("--distinct")]
        public bool Distinct { get; init; } = true; // Default to true for better UX
    }
}