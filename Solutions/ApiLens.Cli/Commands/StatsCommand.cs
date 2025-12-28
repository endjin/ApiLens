using System.ComponentModel;
using System.Text.Json;
using ApiLens.Cli.Services;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;
using Spectre.Console;

namespace ApiLens.Cli.Commands;

public class StatsCommand : Command<StatsCommand.Settings>
{
    private readonly ILuceneIndexManagerFactory indexManagerFactory;
    private readonly IIndexPathResolver indexPathResolver;
    private readonly IQueryEngineFactory queryEngineFactory;
    private readonly IAnsiConsole console;

    public StatsCommand(
        ILuceneIndexManagerFactory indexManagerFactory,
        IIndexPathResolver indexPathResolver,
        IQueryEngineFactory queryEngineFactory,
        IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(indexManagerFactory);
        ArgumentNullException.ThrowIfNull(indexPathResolver);
        ArgumentNullException.ThrowIfNull(queryEngineFactory);
        ArgumentNullException.ThrowIfNull(console);
        this.indexManagerFactory = indexManagerFactory;
        this.indexPathResolver = indexPathResolver;
        this.queryEngineFactory = queryEngineFactory;
        this.console = console;
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            // Create index manager with the specified path
            // Resolve the actual index path
            string resolvedIndexPath = indexPathResolver.ResolveIndexPath(settings.IndexPath);

            // Create index manager
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(resolvedIndexPath);

            MetadataService metadataService = new();
            metadataService.StartTiming();

            // Get index statistics
            IndexStatistics? stats = indexManager.GetIndexStatistics();

            // Calculate documentation quality metrics if requested
            DocumentationMetrics? docMetrics = null;
            if (settings.IncludeDocumentationMetrics && stats != null)
            {
                using IQueryEngine queryEngine = queryEngineFactory.Create(indexManager);
                docMetrics = CalculateDocumentationMetrics(queryEngine, stats);
            }

            if (stats == null)
            {
                if (settings.Format == OutputFormat.Json)
                {
                    JsonResponse<object> errorResponse = new()
                    {
                        Results = new { error = "No index found at the specified location." },
                        Metadata = metadataService.BuildMetadata(indexManager)
                    };
                    string errorJson = JsonSerializer.Serialize(errorResponse, GetJsonOptions());

                    // Temporarily set unlimited width to prevent JSON wrapping
                    var originalWidth = console.Profile.Width;
                    console.Profile.Width = int.MaxValue;
                    console.WriteLine(errorJson);
                    console.Profile.Width = originalWidth;
                }
                else
                {
                    console.MarkupLine("[yellow]No index found at the specified location.[/]");
                }
                return 0;
            }

            switch (settings.Format)
            {
                case OutputFormat.Json:
                    OutputJson(stats, docMetrics, indexManager, metadataService);
                    break;
                case OutputFormat.Markdown:
                    OutputMarkdown(stats, docMetrics);
                    break;
                case OutputFormat.Table:
                default:
                    OutputTable(stats, docMetrics);
                    break;
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            console.MarkupLine($"[red]Error getting index statistics:[/] {ex.Message}");
            return 1;
        }
    }

    private void OutputTable(IndexStatistics stats, DocumentationMetrics? docMetrics)
    {
        console.WriteLine();
        Table table = new();
        table.AddColumn("Property");
        table.AddColumn("Value");

        table.AddRow("Index Path", stats.IndexPath);
        table.AddRow("Total Size", FormatSize(stats.TotalSizeInBytes));
        table.AddRow("Document Count", stats.DocumentCount.ToString("N0"));
        table.AddRow("Field Count", stats.FieldCount.ToString("N0"));
        table.AddRow("Last Modified", stats.LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown");

        if (stats.FileCount > 0)
        {
            table.AddRow("File Count", stats.FileCount.ToString("N0"));
        }

        console.Write(table);

        // Show documentation metrics if available
        if (docMetrics != null)
        {
            console.WriteLine();
            console.Write(new Rule("[bold yellow]Documentation Quality Metrics[/]"));

            Table docTable = new();
            docTable.AddColumn("Metric");
            docTable.AddColumn("Value");

            docTable.AddRow("Total Members", docMetrics.TotalMembers.ToString("N0"));
            docTable.AddRow("Documented Members", $"{docMetrics.DocumentedMembers:N0} ({docMetrics.DocumentationCoverage:P1})");
            docTable.AddRow("Well-Documented", $"{docMetrics.WellDocumentedMembers:N0} ({docMetrics.WellDocumentedPercentage:P1})");
            docTable.AddRow("With Examples", $"{docMetrics.MembersWithExamples:N0} ({docMetrics.ExampleCoverage:P1})");
            docTable.AddRow("Average Score", $"{docMetrics.AverageDocScore:F1}/100");

            if (docMetrics.PoorlyDocumentedTypes.Any())
            {
                docTable.AddRow("[dim]Needs Attention[/]", string.Join(", ", docMetrics.PoorlyDocumentedTypes.Take(3)));
            }

            console.Write(docTable);
        }
    }

    private void OutputJson(IndexStatistics stats, DocumentationMetrics? docMetrics, ILuceneIndexManager indexManager,
        MetadataService metadataService)
    {
        object statsData = docMetrics != null ?
            new
            {
                stats.IndexPath,
                stats.TotalSizeInBytes,
                TotalSizeFormatted = FormatSize(stats.TotalSizeInBytes),
                stats.DocumentCount,
                stats.FieldCount,
                stats.FileCount,
                LastModified = stats.LastModified?.ToString("O"),
                DocumentationMetrics = new
                {
                    docMetrics.TotalMembers,
                    docMetrics.DocumentedMembers,
                    docMetrics.WellDocumentedMembers,
                    docMetrics.MembersWithExamples,
                    docMetrics.AverageDocScore,
                    docMetrics.DocumentationCoverage,
                    docMetrics.WellDocumentedPercentage,
                    docMetrics.ExampleCoverage,
                    docMetrics.PoorlyDocumentedTypes
                }
            } :
            new
            {
                stats.IndexPath,
                stats.TotalSizeInBytes,
                TotalSizeFormatted = FormatSize(stats.TotalSizeInBytes),
                stats.DocumentCount,
                stats.FieldCount,
                stats.FileCount,
                LastModified = stats.LastModified?.ToString("O")
            };

        ResponseMetadata metadata = metadataService.BuildMetadata(indexManager,
            queryType: "stats");

        JsonResponse<object> response = new()
        {
            Results = statsData,
            Metadata = metadata
        };

        string json = JsonSerializer.Serialize(response, GetJsonOptions());

        // Temporarily set unlimited width to prevent JSON wrapping
        var originalWidth = console.Profile.Width;
        console.Profile.Width = int.MaxValue;
        console.WriteLine(json);
        console.Profile.Width = originalWidth;
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JsonSanitizer.CreateSafeJsonEncoder(),
            Converters = { new SanitizingJsonConverterFactory() }
        };
    }

    private void OutputMarkdown(IndexStatistics stats, DocumentationMetrics? docMetrics)
    {
        console.WriteLine("# Index Statistics");
        console.WriteLine();
        console.WriteLine($"**Index Path**: `{stats.IndexPath}`");
        console.WriteLine();
        console.WriteLine("| Metric | Value |");
        console.WriteLine("|--------|-------|");
        console.WriteLine($"| Total Size | {FormatSize(stats.TotalSizeInBytes)} |");
        console.WriteLine($"| Documents | {stats.DocumentCount:N0} |");
        console.WriteLine($"| Fields | {stats.FieldCount:N0} |");
        console.WriteLine($"| Files | {stats.FileCount:N0} |");
        console.WriteLine(
            $"| Last Modified | {stats.LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"} |");
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int order = 0;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    private static DocumentationMetrics CalculateDocumentationMetrics(IQueryEngine queryEngine, IndexStatistics stats)
    {
        // Sample a reasonable number of documents for analysis
        int sampleSize = Math.Min(stats.DocumentCount, 1000);
        var allMembers = queryEngine.SearchByContent("*", sampleSize);

        // Find poorly documented public types
        var publicTypes = allMembers
            .Where(m => m.MemberType == MemberType.Type && m.DocumentationScore < 40)
            .OrderBy(m => m.DocumentationScore)
            .Take(10)
            .Select(m => m.Name)
            .ToList();

        var metrics = new DocumentationMetrics
        {
            TotalMembers = stats.DocumentCount,
            DocumentedMembers = allMembers.Count(m => m.IsDocumented),
            WellDocumentedMembers = allMembers.Count(m => m.IsWellDocumented),
            MembersWithExamples = allMembers.Count(m => m.CodeExamples.Any()),
            AverageDocScore = allMembers.Any() ? allMembers.Average(m => m.DocumentationScore) : 0,
            PoorlyDocumentedTypes = publicTypes
        };

        return metrics;
    }

    private class DocumentationMetrics
    {
        public int TotalMembers { get; init; }
        public int DocumentedMembers { get; init; }
        public int WellDocumentedMembers { get; init; }
        public int MembersWithExamples { get; init; }
        public double AverageDocScore { get; init; }
        public List<string> PoorlyDocumentedTypes { get; init; } = [];

        public double DocumentationCoverage => TotalMembers > 0 ? (double)DocumentedMembers / TotalMembers : 0;
        public double WellDocumentedPercentage => TotalMembers > 0 ? (double)WellDocumentedMembers / TotalMembers : 0;
        public double ExampleCoverage => TotalMembers > 0 ? (double)MembersWithExamples / TotalMembers : 0;
    }

    public sealed class Settings : CommandSettings
    {
        [Description("Path to the Lucene index directory (default: ~/.apilens/index or APILENS_INDEX env var)")]
        [CommandOption("-i|--index")]
        public string? IndexPath { get; init; }

        [Description("Output format")]
        [CommandOption("-f|--format")]
        public OutputFormat Format { get; init; } = OutputFormat.Table;

        [Description("Include documentation quality metrics")]
        [CommandOption("-d|--doc-metrics")]
        public bool IncludeDocumentationMetrics { get; init; }
    }
}