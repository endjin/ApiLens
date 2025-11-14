using System.ComponentModel;
using System.Diagnostics;
using ApiLens.Cli.Services;
using ApiLens.Core.Helpers;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Services;

namespace ApiLens.Cli.Commands;

public class IndexCommand : AsyncCommand<IndexCommand.Settings>
{
    private readonly ILuceneIndexManagerFactory indexManagerFactory;
    private readonly IFileSystemService fileSystem;
    private readonly IFileHashHelper fileHashHelper;
    private readonly IIndexPathResolver indexPathResolver;

    public IndexCommand(
        ILuceneIndexManagerFactory indexManagerFactory,
        IFileSystemService fileSystem,
        IFileHashHelper fileHashHelper,
        IIndexPathResolver indexPathResolver)
    {
        ArgumentNullException.ThrowIfNull(indexManagerFactory);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(fileHashHelper);
        ArgumentNullException.ThrowIfNull(indexPathResolver);

        this.indexManagerFactory = indexManagerFactory;
        this.fileSystem = fileSystem;
        this.fileHashHelper = fileHashHelper;
        this.indexPathResolver = indexPathResolver;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
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

            // Resolve the actual index path
            string resolvedIndexPath = indexPathResolver.ResolveIndexPath(settings.IndexPath);

            // Create index manager
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(resolvedIndexPath);

            List<string> filesToIndex;

            if (settings.Clean)
            {
                AnsiConsole.MarkupLine("[yellow]Cleaning index...[/]");
                indexManager.DeleteAll();
                await indexManager.CommitAsync();
                filesToIndex = xmlFiles;
            }
            else
            {
                // Get existing packages from index for change detection
                Dictionary<string, HashSet<string>> indexedPackages = [];

                await AnsiConsole.Status()
                    .StartAsync("Analyzing index for change detection...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Star);
                        ctx.SpinnerStyle(Style.Parse("yellow"));

                        indexedPackages = await Task.Run(() => indexManager.GetIndexedPackageVersions());
                    });

                // Determine which files need indexing
                filesToIndex = [];
                HashSet<string> assembliesToUpdate = [];
                int skippedFiles = 0;

                await AnsiConsole.Status()
                    .StartAsync("Computing file hashes for change detection...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Star);
                        ctx.SpinnerStyle(Style.Parse("yellow"));

                        foreach (string file in xmlFiles)
                        {
                            // Check if it's a NuGet file
                            (string PackageId, string Version, string Framework)? nugetInfo =
                                NuGetHelper.ExtractNuGetInfo(file);

                            if (nugetInfo.HasValue)
                            {
                                // For NuGet files, check by package ID and version
                                if (!indexedPackages.TryGetValue(nugetInfo.Value.PackageId,
                                        out HashSet<string>? versions) ||
                                    !versions.Contains(nugetInfo.Value.Version))
                                {
                                    filesToIndex.Add(file);
                                    assembliesToUpdate.Add(nugetInfo.Value.PackageId);
                                }
                                else
                                {
                                    skippedFiles++;
                                }
                            }
                            else
                            {
                                try
                                {
                                    // For non-NuGet files, we need to compute hash
                                    string fileHash = await fileHashHelper.ComputeFileHashAsync(file);

                                    // We need to get the assembly name from the file
                                    // For now, use filename as a proxy (will be replaced by actual assembly name during parsing)
                                    string assemblyName = System.IO.Path.GetFileNameWithoutExtension(file)
                                        .ToLowerInvariant();

                                    if (!indexedPackages.TryGetValue(assemblyName, out HashSet<string>? versions) ||
                                        !versions.Contains(fileHash))
                                    {
                                        filesToIndex.Add(file);
                                        assembliesToUpdate.Add(assemblyName);
                                    }
                                    else
                                    {
                                        skippedFiles++;
                                    }
                                }
                                catch (IOException)
                                {
                                    // If we can't compute the hash (e.g., file doesn't exist in test scenarios),
                                    // treat it as a new file that needs indexing
                                    filesToIndex.Add(file);
                                    string assemblyName = System.IO.Path.GetFileNameWithoutExtension(file)
                                        .ToLowerInvariant();
                                    assembliesToUpdate.Add(assemblyName);
                                }
                            }
                        }
                    });

                // Report what we're doing
                int totalInIndex = indexedPackages.Sum(kvp => kvp.Value.Count);
                AnsiConsole.MarkupLine(
                    $"[dim]Index contains {totalInIndex:N0} file versions across {indexedPackages.Count:N0} assemblies.[/]");

                if (skippedFiles > 0)
                {
                    AnsiConsole.MarkupLine(
                        $"[green]Skipping {skippedFiles:N0} file(s) already up-to-date in index.[/]");
                }

                if (filesToIndex.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]All files are already up-to-date. Nothing to index.[/]");
                    return 0;
                }

                AnsiConsole.MarkupLine($"[green]Found {filesToIndex.Count:N0} file(s) to index (new or updated).[/]");

                // Clean up old versions if needed
                if (assembliesToUpdate.Count > 0)
                {
                    int documentsBeforeCleanup = indexManager.GetTotalDocuments();

                    await AnsiConsole.Status()
                        .StartAsync($"Removing old versions from {assembliesToUpdate.Count:N0} assemblies...",
                            async ctx =>
                            {
                                ctx.Spinner(Spinner.Known.Star);
                                ctx.SpinnerStyle(Style.Parse("yellow"));

                                await Task.Run(() =>
                                {
                                    indexManager.DeleteDocumentsByPackageIds(assembliesToUpdate);
                                });
                                await indexManager.CommitAsync();
                            });

                    int documentsAfterCleanup = indexManager.GetTotalDocuments();
                    int documentsRemoved = documentsBeforeCleanup - documentsAfterCleanup;

                    if (documentsRemoved > 0)
                    {
                        AnsiConsole.MarkupLine(
                            $"[green]Removed {documentsRemoved:N0} API members from old versions.[/]");
                    }
                }
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
                    ProgressTask task = ctx.AddTask("[green]Indexing XML files[/]", maxValue: filesToIndex.Count);

                    // Index all files using high-performance batch operations
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    IndexingResult result = await indexManager.IndexXmlFilesAsync(
                        filesToIndex,
                        filesProcessed => task.Value = filesProcessed);
                    stopwatch.Stop();

                    task.StopTask();

                    // Display results
                    AnsiConsole.WriteLine();
                    DisplayResults(result, indexManager, resolvedIndexPath);
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
            table.AddRow("GC Gen0/1/2",
                $"{result.Metrics.Gen0Collections}/{result.Metrics.Gen1Collections}/{result.Metrics.Gen2Collections}");
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
        {
            return $"{duration.TotalMilliseconds:N0} ms";
        }
        else if (duration.TotalSeconds < 60)
        {
            return $"{duration.TotalSeconds:N2} s";
        }
        else
        {
            return $"{duration.TotalMinutes:N2} min";
        }
    }

    /// <summary>
    /// Extracts NuGet package information from a file path.
    /// </summary>
    public static (string PackageId, string Version, string Framework)? ExtractNuGetInfo(string filePath)
    {
        // Delegate to the shared helper
        return NuGetHelper.ExtractNuGetInfo(filePath);
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