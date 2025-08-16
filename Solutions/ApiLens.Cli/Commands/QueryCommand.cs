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
    private readonly IIndexPathResolver indexPathResolver;

    public QueryCommand(
        ILuceneIndexManagerFactory indexManagerFactory,
        IQueryEngineFactory queryEngineFactory,
        IIndexPathResolver indexPathResolver)
    {
        ArgumentNullException.ThrowIfNull(indexManagerFactory);
        ArgumentNullException.ThrowIfNull(queryEngineFactory);
        ArgumentNullException.ThrowIfNull(indexPathResolver);

        this.indexManagerFactory = indexManagerFactory;
        this.queryEngineFactory = queryEngineFactory;
        this.indexPathResolver = indexPathResolver;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        try
        {
            // Resolve the actual index path
            string resolvedIndexPath = indexPathResolver.ResolveIndexPath(settings.IndexPath);

            // Create index manager and query engine with the specified path
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(resolvedIndexPath);
            using IQueryEngine queryEngine = queryEngineFactory.Create(indexManager);

            MetadataService metadataService = new();
            metadataService.StartTiming();

            List<MemberInfo> results;

            // Check if declaring type filter is specified
            if (!string.IsNullOrEmpty(settings.DeclaringTypeFilter))
            {
                // Search by declaring type first
                results = queryEngine.SearchByDeclaringType(settings.DeclaringTypeFilter, settings.MaxResults * 2);

                // Then filter by query if provided
                if (!string.IsNullOrEmpty(settings.Query) && settings.Query != "*")
                {
                    var queryLower = settings.Query.ToLowerInvariant();
                    bool hasWildcards = settings.Query.Contains('*') || settings.Query.Contains('?');

                    if (hasWildcards)
                    {
                        var pattern = queryLower.Replace("*", ".*").Replace("?", ".");
                        var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        results = results.Where(r => regex.IsMatch(r.Name)).ToList();
                    }
                    else
                    {
                        results = results.Where(r => r.Name.Contains(queryLower, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                }

                // Apply member type filter if specified
                if (settings.MemberTypeFilter.HasValue)
                {
                    results = results.Where(r => r.MemberType == settings.MemberTypeFilter.Value).ToList();
                }

                results = results.Take(settings.MaxResults).ToList();
            }
            // Check if we have any other filters specified
            else if (settings.MemberTypeFilter.HasValue ||
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
                    QueryType.Name => HandleNameQuery(queryEngine, settings.Query, settings.MaxResults, settings.IgnoreCase),
                    QueryType.Content => queryEngine.SearchByContent(settings.Query, settings.MaxResults),
                    QueryType.Namespace => queryEngine.SearchByNamespace(settings.Query, settings.MaxResults),
                    QueryType.Id => queryEngine.GetById(settings.Query) is { } member ? [member] : [],
                    QueryType.Assembly => queryEngine.SearchByAssembly(settings.Query, settings.MaxResults),
                    QueryType.Method => SearchForMethods(queryEngine, settings.Query, settings.MinParams, settings.MaxParams, settings.MaxResults),
                    _ => []
                };
            }

            // Apply additional filters
            if (settings.WithExamples)
            {
                results = results.Where(r => r.CodeExamples.Any()).ToList();
            }

            if (settings.EntryPointsOnly)
            {
                var entryPointMethods = new[] { "Create", "Parse", "Load", "Open", "Build", "From", "New", "Make", "Get", "Add", "Initialize" };
                results = results.Where(r =>
                    r.MemberType == MemberType.Method &&
                    entryPointMethods.Any(ep =>
                        r.Name.Equals(ep, StringComparison.OrdinalIgnoreCase) ||
                        r.Name.StartsWith(ep, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            // Apply sorting
            if (settings.QualityFirst)
            {
                results = results.OrderByDescending(r => r.DocumentationScore)
                    .ThenBy(r => r.Name)
                    .ToList();
            }

            // Apply deduplication if requested
            if (settings.Distinct && results.Count > 0)
            {
                var deduplicationService = new ResultDeduplicationService();
                results = deduplicationService.DeduplicateResults(results, true);
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
                    if (settings.GroupBy != GroupBy.None)
                        OutputGroupedTable(results, settings.GroupBy);
                    else
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

    private static List<MemberInfo> HandleNameQuery(IQueryEngine queryEngine, string query, int maxResults, bool ignoreCase = false)
    {
        // Handle special case of "*" to get all results
        if (query == "*" || query == "**")
        {
            // Use SearchWithFilters with wildcard to get all
            return queryEngine.SearchWithFilters("*", null, null, null, maxResults);
        }

        // Handle wildcards in general
        if (query.Contains('*') || query.Contains('?'))
        {
            return queryEngine.SearchWithFilters(query, null, null, null, maxResults);
        }

        // Normal name search for non-wildcard queries - now with ignoreCase support
        return queryEngine.SearchByName(query, maxResults, ignoreCase);
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

        // Handle wildcard for all methods
        if (pattern == "*" || pattern == "**")
        {
            var methodResults = queryEngine.SearchWithFilters("*", MemberType.Method, null, null, maxResults);
            return methodResults;
        }

        // Handle wildcards in pattern
        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            var wildcardResults = queryEngine.SearchWithFilters(pattern, MemberType.Method, null, null, maxResults * 2);
            return wildcardResults.Take(maxResults).ToList();
        }

        // First, search for methods with matching names (no wildcards)
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

    private static void OutputGroupedTable(List<MemberInfo> results, GroupBy groupBy)
    {
        var groups = groupBy switch
        {
            GroupBy.Namespace => results.GroupBy(r => r.Namespace),
            GroupBy.Assembly => results.GroupBy(r => r.Assembly),
            GroupBy.MemberType => results.GroupBy(r => r.MemberType.ToString()),
            GroupBy.Category => results.GroupBy(r => CategorizeResult(r)),
            _ => results.GroupBy(r => "All")
        };

        foreach (var group in groups.OrderBy(g => g.Key))
        {
            AnsiConsole.Write(new Rule($"[bold yellow]{Markup.Escape(group.Key ?? "Unknown")}[/] ({group.Count()} items)")
            {
                Justification = Justify.Left
            });

            var table = new Table();
            table.AddColumn("Type");
            table.AddColumn("Name");
            if (groupBy != GroupBy.Namespace)
                table.AddColumn("Namespace");
            if (groupBy != GroupBy.Assembly)
                table.AddColumn("Assembly");

            foreach (var result in group.Take(20)) // Limit items per group
            {
                var row = new List<string>
                {
                    result.MemberType.ToString(),
                    Markup.Escape(GenericTypeFormatter.FormatTypeName(result.Name))
                };

                if (groupBy != GroupBy.Namespace)
                    row.Add(Markup.Escape(GenericTypeFormatter.FormatTypeName(result.Namespace)));
                if (groupBy != GroupBy.Assembly)
                    row.Add(Markup.Escape(result.Assembly));

                table.AddRow(row.ToArray());
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
    }

    private static string CategorizeResult(MemberInfo member)
    {
        // Categorize based on common patterns
        if (member.Name.EndsWith("Exception"))
            return "Exceptions";
        if (member.Name.StartsWith("I") && member.Name.Length > 1 && char.IsUpper(member.Name[1]))
            return "Interfaces";
        if (member.Name.EndsWith("Attribute"))
            return "Attributes";
        if (member.Name.EndsWith("EventArgs"))
            return "Events";
        if (member.Name.EndsWith("Handler") || member.Name.EndsWith("Delegate"))
            return "Delegates";
        if (member.Name.EndsWith("Collection") || member.Name.EndsWith("List") || member.Name.EndsWith("Dictionary"))
            return "Collections";
        if (member.Name.EndsWith("Builder") || member.Name.EndsWith("Factory"))
            return "Builders/Factories";
        if (member.MemberType == MemberType.Method)
            return "Methods";
        if (member.MemberType == MemberType.Property)
            return "Properties";
        if (member.MemberType == MemberType.Type)
            return "Types";
        return "Other";
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

        [Description("Filter by declaring type. Shows only members of the specified type")]
        [CommandOption("--declaring-type")]
        public string? DeclaringTypeFilter { get; init; }

        [Description("For method searches: minimum parameter count (0-10). Use with --type method")]
        [CommandOption("--min-params")]
        public int? MinParams { get; init; }

        [Description("For method searches: maximum parameter count (0-10). Use with --type method")]
        [CommandOption("--max-params")]
        public int? MaxParams { get; init; }

        [Description("Group results by: none (default), category, namespace, assembly, membertype")]
        [CommandOption("--group-by")]
        public GroupBy GroupBy { get; init; } = GroupBy.None;

        [Description("Only show items with code examples")]
        [CommandOption("--with-examples")]
        public bool WithExamples { get; init; }

        [Description("Sort results by documentation quality first")]
        [CommandOption("--quality-first")]
        public bool QualityFirst { get; init; }

        [Description("Show only main entry point methods (Create, Parse, Load, etc.)")]
        [CommandOption("--entry-points")]
        public bool EntryPointsOnly { get; init; }

        [Description("Show only distinct members (one per unique ID, aggregating all framework versions)")]
        [CommandOption("--distinct")]
        public bool Distinct { get; init; } = true; // Default to true for better UX

        [Description("Perform case-insensitive search (slower but more flexible)")]
        [CommandOption("--ignore-case|-c")]
        public bool IgnoreCase { get; init; }
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

    public enum GroupBy
    {
        [Description("No grouping - flat list")]
        None,

        [Description("Group by functional category")]
        Category,

        [Description("Group by namespace")]
        Namespace,

        [Description("Group by assembly")]
        Assembly,

        [Description("Group by member type (Type, Method, Property, etc.)")]
        MemberType
    }
}