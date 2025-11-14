using System.ComponentModel;
using System.Text.Json;
using ApiLens.Cli.Services;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;

namespace ApiLens.Cli.Commands;

public class ComplexityCommand : Command<ComplexityCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JsonSanitizer.CreateSafeJsonEncoder(),
        Converters = { new SanitizingJsonConverterFactory() }
    };

    private readonly ILuceneIndexManagerFactory indexManagerFactory;
    private readonly IIndexPathResolver indexPathResolver;
    private readonly IQueryEngineFactory queryEngineFactory;

    public ComplexityCommand(
        ILuceneIndexManagerFactory indexManagerFactory,
        IIndexPathResolver indexPathResolver,
        IQueryEngineFactory queryEngineFactory)
    {
        ArgumentNullException.ThrowIfNull(indexManagerFactory);
        ArgumentNullException.ThrowIfNull(indexPathResolver);
        ArgumentNullException.ThrowIfNull(queryEngineFactory);

        this.indexManagerFactory = indexManagerFactory;
        this.indexPathResolver = indexPathResolver;
        this.queryEngineFactory = queryEngineFactory;
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            // Create index manager and query engine with the specified path
            // Resolve the actual index path
            string resolvedIndexPath = indexPathResolver.ResolveIndexPath(settings.IndexPath);

            // Create index manager
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(resolvedIndexPath);
            using IQueryEngine queryEngine = queryEngineFactory.Create(indexManager);

            MetadataService metadataService = new();
            metadataService.StartTiming();

            List<MemberInfo> results;
            string criteria = string.Empty;

            if (settings.MinComplexity.HasValue)
            {
                // Get methods with minimum complexity
                results = queryEngine.GetComplexMethods(settings.MinComplexity.Value, settings.MaxResults);
                criteria = $"complexity >= {settings.MinComplexity}";
            }
            else if (settings.MinParams.HasValue || settings.MaxParams.HasValue)
            {
                // Get methods by parameter count
                int min = settings.MinParams ?? 0;
                int max = settings.MaxParams ?? int.MaxValue;
                results = queryEngine.GetByParameterCount(min, max, settings.MaxResults);

                criteria = settings.MaxParams.HasValue
                    ? $"parameters: {min}-{max}"
                    : $"parameters >= {min}";
            }
            else
            {
                if (settings.Format != OutputFormat.Json)
                {
                    AnsiConsole.MarkupLine(
                        "[yellow]Please specify --min-complexity, --min-params, or --max-params.[/]");
                }
                else
                {
                    // Return error in JSON format
                    JsonResponse<object> errorResponse = new()
                    {
                        Results = new { error = "Please specify --min-complexity, --min-params, or --max-params." },
                        Metadata = metadataService.BuildMetadata(indexManager)
                    };
                    string errorJson = JsonSerializer.Serialize(errorResponse, JsonOptions);

                    // Temporarily set unlimited width to prevent JSON wrapping
                    var originalWidth = AnsiConsole.Profile.Width;
                    AnsiConsole.Profile.Width = int.MaxValue;
                    AnsiConsole.WriteLine(errorJson);
                    AnsiConsole.Profile.Width = originalWidth;
                }

                return 1;
            }

            if (results.Count == 0)
            {
                if (settings.Format == OutputFormat.Json)
                {
                    OutputJson([], criteria, settings.ShowStats, indexManager, metadataService);
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No methods found matching the criteria.[/]");
                }

                return 0;
            }

            // Sort by complexity or parameter count
            List<MemberInfo> sortedResults = settings.SortBy switch
            {
                "params" => [.. results.OrderByDescending(r => r.Complexity?.ParameterCount ?? 0)],
                "complexity" => [.. results.OrderByDescending(r => r.Complexity?.CyclomaticComplexity ?? 0)],
                _ => results
            };

            switch (settings.Format)
            {
                case OutputFormat.Json:
                    OutputJson(sortedResults, criteria, settings.ShowStats, indexManager, metadataService);
                    break;
                case OutputFormat.Markdown:
                    OutputMarkdown(sortedResults, criteria, settings.ShowStats);
                    break;
                case OutputFormat.Table:
                default:
                    OutputTable(sortedResults, criteria, settings.ShowStats);
                    break;
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error analyzing complexity:[/] {ex.Message}");
            return 1;
        }
    }

    private static void OutputJson(List<MemberInfo> results, string criteria, bool showStats,
        ILuceneIndexManager indexManager, MetadataService metadataService)
    {
        List<ComplexityMetrics> metrics = [.. results
            .Where(r => r.Complexity != null)
            .Select(r => r.Complexity!)];

        var output = new
        {
            criteria = criteria,
            results = results.Select(member => new
            {
                memberInfo =
                    new
                    {
                        id = member.Id,
                        name = member.Name,
                        fullName = member.FullName,
                        summary = member.Summary,
                        @namespace = member.Namespace,
                        assembly = member.Assembly
                    },
                complexity =
                    member.Complexity != null
                        ? new
                        {
                            parameterCount = member.Complexity.ParameterCount,
                            cyclomaticComplexity = member.Complexity.CyclomaticComplexity,
                            documentationLineCount = member.Complexity.DocumentationLineCount
                        }
                        : null
            }),
            statistics = showStats && metrics.Count > 0
                ? new
                {
                    averageParameters = metrics.Average(m => m.ParameterCount),
                    maxParameters = metrics.Max(m => m.ParameterCount),
                    averageComplexity = metrics.Average(m => m.CyclomaticComplexity),
                    maxComplexity = metrics.Max(m => m.CyclomaticComplexity),
                    averageDocLines = metrics.Average(m => m.DocumentationLineCount)
                }
                : null
        };

        ResponseMetadata metadata = metadataService.BuildMetadata(results, indexManager,
            query: criteria, queryType: "complexity",
            commandMetadata: showStats && metrics.Count > 0
                ? new Dictionary<string, object> { ["includesStatistics"] = true }
                : null);

        JsonResponse<object> response = new()
        {
            Results = output,
            Metadata = metadata
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);

        // Temporarily set unlimited width to prevent JSON wrapping
        var originalWidth = AnsiConsole.Profile.Width;
        AnsiConsole.Profile.Width = int.MaxValue;
        AnsiConsole.WriteLine(json);
        AnsiConsole.Profile.Width = originalWidth;
    }

    private static void OutputMarkdown(List<MemberInfo> results, string criteria, bool showStats)
    {
        AnsiConsole.WriteLine("# Complexity Analysis");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine($"Criteria: {criteria}");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine($"Found {results.Count} method(s).");
        AnsiConsole.WriteLine();

        AnsiConsole.WriteLine("| Method | Parameters | Complexity | Doc Lines |");
        AnsiConsole.WriteLine("|--------|------------|------------|-----------|");

        foreach (MemberInfo member in results)
        {
            ComplexityMetrics? metrics = member.Complexity;
            AnsiConsole.WriteLine(
                $"| {member.FullName} | {metrics?.ParameterCount ?? 0} | {metrics?.CyclomaticComplexity ?? 0} | {metrics?.DocumentationLineCount ?? 0} |");
        }

        if (showStats && results.Any(r => r.Complexity != null))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("## Statistics");
            AnsiConsole.WriteLine();

            List<ComplexityMetrics> metrics = [.. results
                .Where(r => r.Complexity != null)
                .Select(r => r.Complexity!)];

            if (metrics.Count > 0)
            {
                AnsiConsole.WriteLine("| Metric | Value |");
                AnsiConsole.WriteLine("|--------|-------|");
                AnsiConsole.WriteLine($"| Average Parameters | {metrics.Average(m => m.ParameterCount):F1} |");
                AnsiConsole.WriteLine($"| Max Parameters | {metrics.Max(m => m.ParameterCount)} |");
                AnsiConsole.WriteLine($"| Average Complexity | {metrics.Average(m => m.CyclomaticComplexity):F1} |");
                AnsiConsole.WriteLine($"| Max Complexity | {metrics.Max(m => m.CyclomaticComplexity)} |");
                AnsiConsole.WriteLine($"| Average Doc Lines | {metrics.Average(m => m.DocumentationLineCount):F1} |");
            }
        }
    }

    private static void OutputTable(List<MemberInfo> results, string criteria, bool showStats)
    {
        AnsiConsole.MarkupLine($"[green]Methods with {criteria}:[/]");
        AnsiConsole.WriteLine();

        Table table = new();
        table.AddColumn("Method");
        table.AddColumn(new TableColumn("Parameters").Centered());
        table.AddColumn(new TableColumn("Complexity").Centered());
        table.AddColumn(new TableColumn("Doc Lines").Centered());

        foreach (MemberInfo member in results)
        {
            ComplexityMetrics? metrics = member.Complexity;

            table.AddRow(
                Markup.Escape(member.FullName),
                metrics?.ParameterCount.ToString() ?? "-",
                metrics?.CyclomaticComplexity.ToString() ?? "-",
                metrics?.DocumentationLineCount.ToString() ?? "-"
            );
        }

        AnsiConsole.Write(table);

        // Show statistics
        if (showStats && results.Any(r => r.Complexity != null))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold]Statistics[/]").RuleStyle("dim"));

            List<ComplexityMetrics> metrics = [.. results
                .Where(r => r.Complexity != null)
                .Select(r => r.Complexity!)];

            if (metrics.Count > 0)
            {
                Grid grid = new();
                grid.AddColumn();
                grid.AddColumn();

                grid.AddRow("[bold]Metric[/]", "[bold]Value[/]");
                grid.AddRow("Average Parameters", metrics.Average(m => m.ParameterCount).ToString("F1"));
                grid.AddRow("Max Parameters", metrics.Max(m => m.ParameterCount).ToString());
                grid.AddRow("Average Complexity", metrics.Average(m => m.CyclomaticComplexity).ToString("F1"));
                grid.AddRow("Max Complexity", metrics.Max(m => m.CyclomaticComplexity).ToString());
                grid.AddRow("Average Doc Lines", metrics.Average(m => m.DocumentationLineCount).ToString("F1"));

                AnsiConsole.Write(grid);
            }
        }
    }

    public sealed class Settings : CommandSettings
    {
        [Description("Minimum cyclomatic complexity")]
        [CommandOption("--min-complexity")]
        public int? MinComplexity { get; init; }

        [Description("Minimum number of parameters")]
        [CommandOption("--min-params")]
        public int? MinParams { get; init; }

        [Description("Maximum number of parameters")]
        [CommandOption("--max-params")]
        public int? MaxParams { get; init; }

        [Description("Path to the Lucene index directory")]
        [CommandOption("-i|--index")]
        public string IndexPath { get; init; } = "./index";

        [Description("Maximum number of results to return")]
        [CommandOption("-m|--max")]
        public int MaxResults { get; init; } = 20;

        [Description("Sort results by (params|complexity)")]
        [CommandOption("-s|--sort")]
        public string SortBy { get; init; } = "complexity";

        [Description("Show statistics")]
        [CommandOption("--stats")]
        public bool ShowStats { get; init; }

        [Description("Output format")]
        [CommandOption("-f|--format")]
        public OutputFormat Format { get; init; } = OutputFormat.Table;
    }
}