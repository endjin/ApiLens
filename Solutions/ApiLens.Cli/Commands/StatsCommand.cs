using System.ComponentModel;
using System.Text.Json;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;

namespace ApiLens.Cli.Commands;

public class StatsCommand : Command<StatsCommand.Settings>
{
    private readonly ILuceneIndexManagerFactory indexManagerFactory;

    public StatsCommand(ILuceneIndexManagerFactory indexManagerFactory)
    {
        ArgumentNullException.ThrowIfNull(indexManagerFactory);
        this.indexManagerFactory = indexManagerFactory;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        try
        {
            // Create index manager with the specified path
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(settings.IndexPath);

            // Get index statistics
            IndexStatistics? stats = indexManager.GetIndexStatistics();

            if (stats == null)
            {
                AnsiConsole.MarkupLine("[yellow]No index found at the specified location.[/]");
                return 0;
            }

            switch (settings.Format)
            {
                case OutputFormat.Json:
                    OutputJson(stats);
                    break;
                case OutputFormat.Markdown:
                    OutputMarkdown(stats);
                    break;
                case OutputFormat.Table:
                default:
                    OutputTable(stats);
                    break;
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error getting index statistics:[/] {ex.Message}");
            return 1;
        }
    }

    private static void OutputTable(IndexStatistics stats)
    {
        AnsiConsole.WriteLine();
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

        AnsiConsole.Write(table);
    }

    private static void OutputJson(IndexStatistics stats)
    {
        JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        string json = JsonSerializer.Serialize(new
        {
            stats.IndexPath,
            stats.TotalSizeInBytes,
            TotalSizeFormatted = FormatSize(stats.TotalSizeInBytes),
            stats.DocumentCount,
            stats.FieldCount,
            stats.FileCount,
            LastModified = stats.LastModified?.ToString("O")
        }, jsonOptions);

        AnsiConsole.WriteLine(json);
    }

    private static void OutputMarkdown(IndexStatistics stats)
    {
        AnsiConsole.WriteLine("# Index Statistics");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine($"**Index Path**: `{stats.IndexPath}`");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("| Metric | Value |");
        AnsiConsole.WriteLine("|--------|-------|");
        AnsiConsole.WriteLine($"| Total Size | {FormatSize(stats.TotalSizeInBytes)} |");
        AnsiConsole.WriteLine($"| Documents | {stats.DocumentCount:N0} |");
        AnsiConsole.WriteLine($"| Fields | {stats.FieldCount:N0} |");
        AnsiConsole.WriteLine($"| Files | {stats.FileCount:N0} |");
        AnsiConsole.WriteLine($"| Last Modified | {stats.LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"} |");
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

    public sealed class Settings : CommandSettings
    {
        [Description("Path to the Lucene index directory")]
        [CommandOption("-i|--index")]
        public string IndexPath { get; init; } = "./index";

        [Description("Output format")]
        [CommandOption("-f|--format")]
        public OutputFormat Format { get; init; } = OutputFormat.Table;
    }
}