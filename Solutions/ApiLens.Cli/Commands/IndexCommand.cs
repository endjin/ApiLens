using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Services;

namespace ApiLens.Cli.Commands;

public class IndexCommand : AsyncCommand<IndexCommand.Settings>
{
    private readonly ILuceneIndexManagerFactory indexManagerFactory;
    private readonly IFileSystemService fileSystem;

    public IndexCommand(
        ILuceneIndexManagerFactory indexManagerFactory,
        IFileSystemService fileSystem)
    {
        ArgumentNullException.ThrowIfNull(indexManagerFactory);
        ArgumentNullException.ThrowIfNull(fileSystem);

        this.indexManagerFactory = indexManagerFactory;
        this.fileSystem = fileSystem;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (!fileSystem.FileExists(settings.Path) && !fileSystem.DirectoryExists(settings.Path))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Path '{settings.Path}' does not exist.");
            return 1;
        }

        try
        {
            // Get all XML files upfront
            List<string> xmlFiles = GetXmlFiles(settings.Path, settings.Pattern);

            if (xmlFiles.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No XML files found to index.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[green]Found {xmlFiles.Count} XML file(s) to index.[/]");

            // Create index manager
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(settings.IndexPath);

            if (settings.Clean)
            {
                AnsiConsole.MarkupLine("[yellow]Cleaning index...[/]");
                indexManager.DeleteAll();
                await indexManager.CommitAsync();
            }

            // Create progress display
            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    ProgressTask task = ctx.AddTask("[green]Indexing XML files[/]", maxValue: xmlFiles.Count);

                    // Index all files using high-performance batch operations
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    IndexingResult result = await indexManager.IndexXmlFilesAsync(xmlFiles);
                    stopwatch.Stop();

                    task.Value = xmlFiles.Count;
                    task.StopTask();

                    // Display results
                    AnsiConsole.WriteLine();
                    DisplayResults(result, indexManager, settings.IndexPath);
                });

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during indexing:[/] {ex.Message}");
            return 1;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            AnsiConsole.MarkupLine($"[red]Unexpected error:[/] {ex.Message}");
            if (settings.Verbose)
            {
                AnsiConsole.WriteException(ex);
            }
            return 1;
        }
    }

    private static void DisplayResults(IndexingResult result, ILuceneIndexManager indexManager, string indexPath)
    {
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold yellow]Indexing Results[/]");

        table.AddColumn("[bold]Metric[/]");
        table.AddColumn("[bold]Value[/]", c => c.RightAligned());

        // Basic stats
        table.AddRow("Documents Indexed", result.SuccessfulDocuments.ToString("N0"));
        table.AddRow("Failed Documents", result.FailedDocuments.ToString("N0"));
        table.AddRow("Total Time", FormatDuration(result.ElapsedTime));
        table.AddRow("Documents/Second", result.DocumentsPerSecond.ToString("N2"));
        table.AddRow("Throughput", $"{result.MegabytesPerSecond:N2} MB/s");
        table.AddRow("Data Processed", FormatSize(result.BytesProcessed));

        // Performance metrics
        if (result.Metrics != null)
        {
            table.AddEmptyRow();
            table.AddRow("[dim]Performance Metrics[/]", "");
            table.AddRow("Avg Batch Commit", $"{result.Metrics.AverageBatchCommitTimeMs:N2} ms");
            table.AddRow("Peak Threads", result.Metrics.PeakThreadCount.ToString());
            table.AddRow("Peak Memory", FormatSize(result.Metrics.PeakWorkingSetBytes));
            table.AddRow("GC Gen0/1/2", $"{result.Metrics.Gen0Collections}/{result.Metrics.Gen1Collections}/{result.Metrics.Gen2Collections}");
        }

        // Index info
        IndexStatistics? stats = indexManager.GetIndexStatistics();
        if (stats != null)
        {
            table.AddEmptyRow();
            table.AddRow("[dim]Index Statistics[/]", "");
            table.AddRow("Total Documents", stats.DocumentCount.ToString("N0"));
            table.AddRow("Index Size", FormatSize(stats.TotalSizeInBytes));
            table.AddRow("Index Location", stats.IndexPath);
        }

        AnsiConsole.Write(table);

        // Display errors if any
        if (result.Errors.Length > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]Errors encountered ({result.Errors.Length}):[/]");
            foreach (string error in result.Errors.Take(10))
            {
                AnsiConsole.MarkupLine($"  [red]•[/] {error}");
            }
            if (result.Errors.Length > 10)
            {
                AnsiConsole.MarkupLine($"  [dim]... and {result.Errors.Length - 10} more[/]");
            }
        }
    }

    private List<string> GetXmlFiles(string path, string? pattern)
    {
        List<string> files = [];

        if (fileSystem.FileExists(path) && path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            files.Add(path);
        }
        else if (fileSystem.DirectoryExists(path))
        {
            pattern ??= "*.xml";
            bool recursive = pattern.Contains("**");
            string searchPattern = pattern.Replace("**/", "");

            files.AddRange(fileSystem.GetFiles(path, searchPattern, recursive));
        }

        return files;
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

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMilliseconds < 1000)
            return $"{duration.TotalMilliseconds:N0} ms";
        else if (duration.TotalSeconds < 60)
            return $"{duration.TotalSeconds:N2} s";
        else
            return $"{duration.TotalMinutes:N2} min";
    }

    /// <summary>
    /// Extracts NuGet package information from a file path.
    /// </summary>
    public static (string PackageId, string Version, string Framework)? ExtractNuGetInfo(string filePath)
    {
        // Pattern to match NuGet cache paths: .../packageid/version/lib|ref/framework/*.xml
        Regex regex = new(@"[\\/](?<packageId>[^\\/]+)[\\/](?<version>[^\\/]+)[\\/](?:lib|ref)[\\/](?<framework>[^\\/]+)[\\/][^\\/]+\.xml$", RegexOptions.IgnoreCase);
        Match match = regex.Match(filePath);

        if (match.Success)
        {
            return (
                PackageId: match.Groups["packageId"].Value,
                Version: match.Groups["version"].Value,
                Framework: match.Groups["framework"].Value
            );
        }

        return null;
    }

    public sealed class Settings : CommandSettings
    {
        [Description("Path to XML documentation file or directory")]
        [CommandArgument(0, "<path>")]
        public string Path { get; init; } = string.Empty;

        [Description("Path to the Lucene index directory")]
        [CommandOption("-i|--index")]
        public string IndexPath { get; init; } = "./index";

        [Description("Clean the index before adding new documents")]
        [CommandOption("-c|--clean")]
        public bool Clean { get; init; }

        [Description("File pattern for matching files (when path is a directory)")]
        [CommandOption("-p|--pattern")]
        public string? Pattern { get; init; }

        [Description("Show verbose output including stack traces")]
        [CommandOption("-v|--verbose")]
        public bool Verbose { get; init; }
    }
}