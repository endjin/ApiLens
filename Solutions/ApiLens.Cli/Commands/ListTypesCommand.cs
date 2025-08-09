using System.ComponentModel;
using System.Text.Json;
using ApiLens.Cli.Services;
using ApiLens.Core.Formatting;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;

namespace ApiLens.Cli.Commands;

public class ListTypesCommand : Command<ListTypesCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ILuceneIndexManagerFactory indexManagerFactory;
    private readonly IQueryEngineFactory queryEngineFactory;

    public ListTypesCommand(
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
            // Create index manager and query engine
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(settings.IndexPath);
            using IQueryEngine queryEngine = queryEngineFactory.Create(indexManager);

            MetadataService metadataService = new();
            metadataService.StartTiming();

            // Validate that at least one filter is provided
            if (string.IsNullOrWhiteSpace(settings.Assembly) &&
                string.IsNullOrWhiteSpace(settings.Package) &&
                string.IsNullOrWhiteSpace(settings.Namespace))
            {
                if (settings.Format == OutputFormat.Json)
                {
                    JsonResponse<object> errorResponse = new()
                    {
                        Results = new { error = "At least one filter (--assembly, --package, or --namespace) must be specified." },
                        Metadata = metadataService.BuildMetadata(indexManager)
                    };
                    string errorJson = JsonSerializer.Serialize(errorResponse, JsonOptions);
                    AnsiConsole.WriteLine(errorJson);
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] At least one filter (--assembly, --package, or --namespace) must be specified.");
                }
                return 1;
            }

            // Build the combined query
            List<MemberInfo> results = GetFilteredResults(queryEngine, settings);

            if (results.Count == 0)
            {
                if (settings.Format == OutputFormat.Json)
                {
                    OutputJson(results, indexManager, metadataService, settings);
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No types found matching the specified filters.[/]");
                }
                return 0;
            }

            // Group by assembly/package for better readability
            IEnumerable<IGrouping<string, MemberInfo>> groupedResults = settings.GroupBy switch
            {
                GroupByOption.Assembly => results.GroupBy(r => r.Assembly),
                GroupByOption.Package => results.GroupBy(r => r.PackageId ?? "Unknown"),
                GroupByOption.Namespace => results.GroupBy(r => r.Namespace),
                _ => results.GroupBy(r => r.Assembly)
            };

            switch (settings.Format)
            {
                case OutputFormat.Json:
                    OutputJson(results, indexManager, metadataService, settings);
                    break;
                case OutputFormat.Table:
                    OutputTable(groupedResults, settings);
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[green]Found {results.Count} {(settings.IncludeMembers ? "members" : "types")}[/]");
                    break;
                case OutputFormat.Markdown:
                    OutputMarkdown(groupedResults, settings);
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[green]Found {results.Count} {(settings.IncludeMembers ? "members" : "types")}[/]");
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

    private List<MemberInfo> GetFilteredResults(IQueryEngine queryEngine, Settings settings)
    {
        List<MemberInfo> results = [];

        // Start with the most specific filter
        if (!string.IsNullOrWhiteSpace(settings.Package))
        {
            results = settings.IncludeMembers
                ? queryEngine.SearchByPackage(settings.Package, settings.MaxResults)
                : queryEngine.ListTypesFromPackage(settings.Package, settings.MaxResults);
        }
        else if (!string.IsNullOrWhiteSpace(settings.Assembly))
        {
            results = settings.IncludeMembers
                ? queryEngine.SearchByAssembly(settings.Assembly, settings.MaxResults)
                : queryEngine.ListTypesFromAssembly(settings.Assembly, settings.MaxResults);
        }
        else if (!string.IsNullOrWhiteSpace(settings.Namespace))
        {
            results = queryEngine.SearchByNamespacePattern(settings.Namespace, settings.MaxResults);
            if (!settings.IncludeMembers)
            {
                results = [.. results.Where(m => m.MemberType == MemberType.Type)];
            }
        }

        // Apply additional filters if specified
        if (results.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(settings.Namespace) &&
                !string.IsNullOrWhiteSpace(settings.Package))
            {
                // Filter by namespace if both package and namespace are specified
                string namespacePattern = settings.Namespace;
                bool isWildcard = namespacePattern.Contains('*') || namespacePattern.Contains('?');

                if (isWildcard)
                {
                    // Convert wildcard to regex
                    string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(namespacePattern)
                        .Replace("\\*", ".*")
                        .Replace("\\?", ".") + "$";
                    System.Text.RegularExpressions.Regex regex = new(regexPattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    results = [.. results.Where(m => regex.IsMatch(m.Namespace))];
                }
                else
                {
                    results = [.. results.Where(m => m.Namespace.Equals(namespacePattern,
                        StringComparison.OrdinalIgnoreCase))];
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.Assembly) &&
                !string.IsNullOrWhiteSpace(settings.Package))
            {
                // Filter by assembly if both package and assembly are specified
                string assemblyPattern = settings.Assembly;
                bool isWildcard = assemblyPattern.Contains('*') || assemblyPattern.Contains('?');

                if (isWildcard)
                {
                    string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(assemblyPattern)
                        .Replace("\\*", ".*")
                        .Replace("\\?", ".") + "$";
                    System.Text.RegularExpressions.Regex regex = new(regexPattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    results = [.. results.Where(m => regex.IsMatch(m.Assembly))];
                }
                else
                {
                    results = [.. results.Where(m => m.Assembly.Equals(assemblyPattern,
                        StringComparison.OrdinalIgnoreCase))];
                }
            }
        }

        return results;
    }

    private static void OutputJson(List<MemberInfo> results, ILuceneIndexManager indexManager,
        MetadataService metadataService, Settings settings)
    {
        List<string> filters = [];
        if (!string.IsNullOrWhiteSpace(settings.Assembly))
        {
            filters.Add($"assembly: {settings.Assembly}");
        }

        if (!string.IsNullOrWhiteSpace(settings.Package))
        {
            filters.Add($"package: {settings.Package}");
        }

        if (!string.IsNullOrWhiteSpace(settings.Namespace))
        {
            filters.Add($"namespace: {settings.Namespace}");
        }

        ResponseMetadata metadata = metadataService.BuildMetadata(results, indexManager,
            query: string.Join(", ", filters),
            queryType: "list-types",
            commandMetadata: new Dictionary<string, object>
            {
                ["includeMembers"] = settings.IncludeMembers,
                ["groupBy"] = settings.GroupBy.ToString()
            });

        JsonResponse<List<MemberInfo>> response = new()
        {
            Results = results,
            Metadata = metadata
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);
        AnsiConsole.WriteLine(json);
    }

    private static void OutputTable(IEnumerable<IGrouping<string, MemberInfo>> groupedResults, Settings settings)
    {
        foreach (IGrouping<string, MemberInfo> group in groupedResults)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[bold yellow]{group.Key}[/]"));

            Table table = new();
            table.AddColumn("Type");
            table.AddColumn("Name");
            table.AddColumn("Namespace");

            if (!string.IsNullOrWhiteSpace(settings.Package))
            {
                table.AddColumn("Package");
            }

            foreach (MemberInfo member in group.OrderBy(m => m.Namespace).ThenBy(m => m.Name))
            {
                List<string> row =
                [
                    member.MemberType.ToString(),
                    Markup.Escape(GenericTypeFormatter.FormatTypeName(member.Name)),
                    Markup.Escape(member.Namespace)
                ];

                if (!string.IsNullOrWhiteSpace(settings.Package))
                {
                    row.Add(Markup.Escape(member.PackageId ?? "N/A"));
                }

                table.AddRow(row.ToArray());
            }

            AnsiConsole.Write(table);
        }
    }

    private static void OutputMarkdown(IEnumerable<IGrouping<string, MemberInfo>> groupedResults, Settings settings)
    {
        AnsiConsole.WriteLine("# Type Listing Results");
        AnsiConsole.WriteLine();

        foreach (IGrouping<string, MemberInfo> group in groupedResults)
        {
            AnsiConsole.WriteLine($"## {group.Key}");
            AnsiConsole.WriteLine();

            foreach (MemberInfo member in group.OrderBy(m => m.Namespace).ThenBy(m => m.Name))
            {
                AnsiConsole.WriteLine($"### {GenericTypeFormatter.FormatFullName(member.FullName)}");
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine($"- **Type**: {member.MemberType}");
                AnsiConsole.WriteLine($"- **Namespace**: {member.Namespace}");
                AnsiConsole.WriteLine($"- **Assembly**: {member.Assembly}");

                if (!string.IsNullOrWhiteSpace(member.PackageId))
                {
                    AnsiConsole.WriteLine($"- **Package**: {member.PackageId} v{member.PackageVersion ?? "Unknown"}");
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

    public sealed class Settings : CommandSettings
    {
        [Description("Filter by assembly name (supports wildcards: *, ?)")]
        [CommandOption("-a|--assembly")]
        public string? Assembly { get; init; }

        [Description("Filter by NuGet package ID (supports wildcards: *, ?)")]
        [CommandOption("-p|--package")]
        public string? Package { get; init; }

        [Description("Filter by namespace (supports wildcards: *, ?)")]
        [CommandOption("-n|--namespace")]
        public string? Namespace { get; init; }

        [Description("Include all members, not just types")]
        [CommandOption("--include-members")]
        public bool IncludeMembers { get; init; }

        [Description("Group results by (assembly, package, or namespace)")]
        [CommandOption("-g|--group-by")]
        public GroupByOption GroupBy { get; init; } = GroupByOption.Assembly;

        [Description("Path to the Lucene index directory")]
        [CommandOption("-i|--index")]
        public string IndexPath { get; init; } = "./index";

        [Description("Maximum number of results to return")]
        [CommandOption("-m|--max")]
        public int MaxResults { get; init; } = 100;

        [Description("Output format")]
        [CommandOption("-f|--format")]
        public OutputFormat Format { get; init; } = OutputFormat.Table;
    }

    public enum GroupByOption
    {
        [Description("Group by assembly")]
        Assembly,

        [Description("Group by package")]
        Package,

        [Description("Group by namespace")]
        Namespace
    }
}