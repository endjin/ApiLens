using System.ComponentModel;
using System.Text.Json;
using ApiLens.Cli.Services;
using ApiLens.Core.Formatting;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;

namespace ApiLens.Cli.Commands;

public class QueryCommand : Command<QueryCommand.Settings>
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

    public QueryCommand(
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
            // Create index manager and query engine with the specified path
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(settings.IndexPath);
            using IQueryEngine queryEngine = queryEngineFactory.Create(indexManager);

            MetadataService metadataService = new();
            metadataService.StartTiming();

            List<MemberInfo> results;

            // Check if we have any filters specified
            if (settings.MemberTypeFilter.HasValue ||
                !string.IsNullOrEmpty(settings.NamespaceFilter) ||
                !string.IsNullOrEmpty(settings.AssemblyFilter))
            {
                // Use the new SearchWithFilters method
                results = queryEngine.SearchWithFilters(
                    settings.Query,
                    settings.MemberTypeFilter,
                    settings.NamespaceFilter,
                    settings.AssemblyFilter,
                    settings.MaxResults);
            }
            else
            {
                // Use existing search methods
                results = settings.QueryType switch
                {
                    QueryType.Name => queryEngine.SearchByName(settings.Query, settings.MaxResults),
                    QueryType.Content => queryEngine.SearchByContent(settings.Query, settings.MaxResults),
                    QueryType.Namespace => queryEngine.SearchByNamespace(settings.Query, settings.MaxResults),
                    QueryType.Id => queryEngine.GetById(settings.Query) is { } member ? [member] : [],
                    QueryType.Assembly => queryEngine.SearchByAssembly(settings.Query, settings.MaxResults),
                    QueryType.Method => SearchForMethods(queryEngine, settings.Query, settings.MinParams, settings.MaxParams, settings.MaxResults),
                    _ => []
                };
            }

            if (results.Count == 0)
            {
                if (settings.Format == OutputFormat.Json)
                {
                    // Still output empty JSON for machine parsing
                    OutputJson(results, indexManager, metadataService, settings);
                }
                else
                {
                    // Provide helpful suggestions
                    AnsiConsole.MarkupLine("[yellow]No results found.[/]");
                    
                    var suggestionService = new SuggestionService(queryEngine);
                    var queryType = ConvertToSuggestionQueryType(settings.QueryType);
                    var similarNames = suggestionService.GetSimilarNames(settings.Query, queryType);
                    var suggestionMessage = suggestionService.FormatSuggestionMessage(settings.Query, queryType, similarNames);
                    
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]" + Markup.Escape(suggestionMessage) + "[/]");
                }
                return 0;
            }

            switch (settings.Format)
            {
                case OutputFormat.Json:
                    OutputJson(results, indexManager, metadataService, settings);
                    break;
                case OutputFormat.Table:
                    OutputTable(results);
                    break;
                case OutputFormat.Markdown:
                    OutputMarkdown(results);
                    break;
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during query:[/] {ex.Message}");
            return 1;
        }
    }

    private static List<MemberInfo> SearchForMethods(IQueryEngine queryEngine, string pattern, int? minParams, int? maxParams, int maxResults)
    {
        // If parameter count filters are specified, use specialized search
        if (minParams.HasValue || maxParams.HasValue)
        {
            int min = minParams ?? 0;
            int max = maxParams ?? int.MaxValue;
            var paramFilteredMethods = queryEngine.GetByParameterCount(min, max, maxResults * 2);
            
            // If a pattern is specified, filter by it
            if (!string.IsNullOrWhiteSpace(pattern) && pattern != "*")
            {
                var patternLower = pattern.ToLowerInvariant().Replace("*", ".*").Replace("?", ".");
                var regex = new System.Text.RegularExpressions.Regex(patternLower, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                paramFilteredMethods = paramFilteredMethods
                    .Where(m => regex.IsMatch(m.Name))
                    .ToList();
            }
            
            return paramFilteredMethods.Take(maxResults).ToList();
        }
        
        // First, search for methods with matching names (supports wildcards)
        var nameResults = queryEngine.SearchByName(pattern, maxResults * 2);
        
        // Filter to only methods
        var methods = nameResults.Where(m => m.MemberType == MemberType.Method).ToList();
        
        // If we have enough results, return them
        if (methods.Count >= maxResults)
        {
            return methods.Take(maxResults).ToList();
        }
        
        // Otherwise, also search in content for method signatures
        var contentResults = queryEngine.SearchByContent(pattern, maxResults);
        var contentMethods = contentResults.Where(m => m.MemberType == MemberType.Method);
        
        // Combine and deduplicate
        var allMethods = methods.Concat(contentMethods)
            .DistinctBy(m => m.Id)
            .Take(maxResults)
            .ToList();
        
        return allMethods;
    }

    private static Services.QueryType ConvertToSuggestionQueryType(QueryType queryType)
    {
        return queryType switch
        {
            QueryType.Name => Services.QueryType.Name,
            QueryType.Content => Services.QueryType.Content,
            QueryType.Namespace => Services.QueryType.Namespace,
            QueryType.Id => Services.QueryType.Id,
            QueryType.Assembly => Services.QueryType.Assembly,
            QueryType.Method => Services.QueryType.Method,
            _ => Services.QueryType.Content
        };
    }

    private static void OutputJson(List<MemberInfo> results, ILuceneIndexManager indexManager,
        MetadataService metadataService, Settings settings)
    {
        ResponseMetadata metadata = metadataService.BuildMetadata(results, indexManager,
            settings.Query, settings.QueryType.ToString());

        JsonResponse<List<MemberInfo>> response = new()
        {
            Results = results,
            Metadata = metadata
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);
        
        // Temporarily set unlimited width to prevent JSON wrapping
        var originalWidth = AnsiConsole.Profile.Width;
        AnsiConsole.Profile.Width = int.MaxValue;
        AnsiConsole.WriteLine(json);
        AnsiConsole.Profile.Width = originalWidth;
    }

    private static void OutputTable(List<MemberInfo> results)
    {
        Table table = new();
        table.AddColumn("Type");
        table.AddColumn("Name");
        table.AddColumn("Namespace");
        table.AddColumn("Assembly");
        table.AddColumn("Version");

        foreach (MemberInfo result in results)
        {
            string versionInfo = FormatVersionInfo(result);

            table.AddRow(
                result.MemberType.ToString(),
                Markup.Escape(GenericTypeFormatter.FormatTypeName(result.Name)),
                Markup.Escape(GenericTypeFormatter.FormatTypeName(result.Namespace)),
                Markup.Escape(result.Assembly),
                Markup.Escape(versionInfo)
            );
        }

        AnsiConsole.Write(table);
    }

    private static string FormatVersionInfo(MemberInfo member)
    {
        if (string.IsNullOrWhiteSpace(member.PackageVersion))
        {
            return "N/A";
        }

        List<string> parts = [];

        if (!string.IsNullOrWhiteSpace(member.PackageVersion))
        {
            parts.Add(member.PackageVersion);
        }

        if (!string.IsNullOrWhiteSpace(member.TargetFramework))
        {
            parts.Add($"[{member.TargetFramework}]");
        }

        return string.Join(" ", parts);
    }

    private static void OutputMarkdown(List<MemberInfo> results)
    {
        AnsiConsole.WriteLine("# Query Results");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine($"Found {results.Count} result(s)");
        AnsiConsole.WriteLine();

        foreach (MemberInfo result in results)
        {
            AnsiConsole.WriteLine($"## {GenericTypeFormatter.FormatFullName(result.FullName)}");
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine($"- **Type**: {result.MemberType}");
            AnsiConsole.WriteLine($"- **Namespace**: {GenericTypeFormatter.FormatTypeName(result.Namespace)}");
            AnsiConsole.WriteLine($"- **Assembly**: {result.Assembly}");

            // Add version information if available
            if (!string.IsNullOrWhiteSpace(result.PackageId) || result.IsFromNuGetCache)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine("### Version Information");

                if (!string.IsNullOrWhiteSpace(result.PackageId))
                {
                    string packageDisplay = result.PackageId;
                    if (!string.IsNullOrWhiteSpace(result.PackageVersion))
                    {
                        packageDisplay += $" v{result.PackageVersion}";
                    }

                    AnsiConsole.WriteLine($"- **Package**: {packageDisplay}");
                }

                if (!string.IsNullOrWhiteSpace(result.TargetFramework))
                {
                    AnsiConsole.WriteLine($"- **Framework**: {result.TargetFramework}");
                }

                if (result.IsFromNuGetCache)
                {
                    AnsiConsole.WriteLine($"- **Source**: NuGet Cache");
                }
            }

            if (!string.IsNullOrWhiteSpace(result.Summary))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine("### Summary");
                AnsiConsole.WriteLine(result.Summary);
            }

            AnsiConsole.WriteLine();
        }
    }

    public sealed class Settings : CommandSettings
    {
        [Description(
            "Search query. For name: exact match. For content: supports wildcards (*,?), fuzzy (~), boolean (AND,OR,NOT), phrases (\"exact phrase\")")]
        [CommandArgument(0, "<query>")]
        public string Query { get; init; } = string.Empty;

        [Description("Path to the Lucene index directory (default: ./index). Create with 'analyze' or 'index' command first")]
        [CommandOption("-i|--index")]
        public string IndexPath { get; init; } = "./index";

        [Description("Maximum results to return (default: 10, max: 1000). Use larger values for comprehensive searches")]
        [CommandOption("-m|--max")]
        public int MaxResults { get; init; } = 10;

        [Description("Query type: name (exact), content (full-text), method (with params), namespace, id, assembly")]
        [CommandOption("-t|--type")]
        public QueryType QueryType { get; init; } = QueryType.Name;

        [Description("Output format: table (human-readable), json (for parsing), markdown (for docs)")]
        [CommandOption("-f|--format")]
        public OutputFormat Format { get; init; } = OutputFormat.Table;

        [Description("Filter by member type: Type, Method, Property, Field, Event. Narrows results to specific member kinds")]
        [CommandOption("--member-type")]
        public MemberType? MemberTypeFilter { get; init; }

        [Description("Filter by namespace pattern. Supports wildcards: System.* matches all System namespaces")]
        [CommandOption("--namespace")]
        public string? NamespaceFilter { get; init; }

        [Description("Filter by assembly pattern. Supports wildcards: *.Core matches all Core assemblies")]
        [CommandOption("--assembly")]
        public string? AssemblyFilter { get; init; }

        [Description("For method searches: minimum parameter count (0-10). Use with --type method")]
        [CommandOption("--min-params")]
        public int? MinParams { get; init; }

        [Description("For method searches: maximum parameter count (0-10). Use with --type method")]
        [CommandOption("--max-params")]
        public int? MaxParams { get; init; }
    }

    public enum QueryType
    {
        [Description("Search by exact name match (case-insensitive, no wildcards)")]
        Name,

        [Description("Search in content/documentation (supports Lucene query syntax)")]
        Content,

        [Description("Search by namespace (exact match)")]
        Namespace,

        [Description("Get by exact member ID (e.g., T:System.String, M:System.String.Split)")]
        Id,

        [Description("Search by assembly name (exact match)")]
        Assembly,

        [Description("Search for methods by name/signature pattern (supports wildcards)")]
        Method
    }
}