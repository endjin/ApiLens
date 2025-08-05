using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Services;

namespace ApiLens.Cli.Commands;

/// <summary>
/// Command for scanning and indexing NuGet package cache.
/// </summary>
public class NuGetCommand : AsyncCommand<NuGetCommand.Settings>
{
    private readonly IFileSystemService fileSystem;
    private readonly INuGetCacheScanner scanner;
    private readonly IPackageDeduplicationService deduplicationService;
    private readonly ILuceneIndexManagerFactory indexManagerFactory;

    public NuGetCommand(
        IFileSystemService fileSystem,
        INuGetCacheScanner scanner,
        IPackageDeduplicationService deduplicationService,
        ILuceneIndexManagerFactory indexManagerFactory)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(deduplicationService);
        ArgumentNullException.ThrowIfNull(indexManagerFactory);

        this.fileSystem = fileSystem;
        this.scanner = scanner;
        this.deduplicationService = deduplicationService;
        this.indexManagerFactory = indexManagerFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        Stopwatch commandStopwatch = Stopwatch.StartNew();
        
        try
        {
            // Get NuGet cache path
            string cachePath = fileSystem.GetUserNuGetCachePath();
            AnsiConsole.MarkupLine($"[green]NuGet cache location:[/] {cachePath}");

            if (!fileSystem.DirectoryExists(cachePath))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] NuGet cache directory does not exist.");
                return 1;
            }

            // Scan for packages
            List<NuGetPackageInfo> packages = [];

            await AnsiConsole.Status()
                .StartAsync("Scanning NuGet cache...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));

                    ImmutableArray<NuGetPackageInfo> allPackages = await scanner.ScanDirectoryAsync(cachePath, cancellationToken: default);
                    
                    if (!string.IsNullOrEmpty(settings.PackageFilter))
                    {
                        string regexPattern = settings.PackageFilter.Replace("*", ".*");
                        Regex regex = new(regexPattern, RegexOptions.IgnoreCase);
                        packages = allPackages.Where(p => regex.IsMatch(p.PackageId)).ToList();
                    }
                    else
                    {
                        packages = allPackages.ToList();
                    }
                });

            if (packages.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No packages found matching the filter.[/]");
                commandStopwatch.Stop();
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]Total command execution time: {FormatDuration(commandStopwatch.Elapsed)}[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[green]Found {packages.Count} NuGet package(s) with XML documentation.[/]");

            // Apply version filter if specified
            if (!string.IsNullOrWhiteSpace(settings.VersionFilter))
            {
                await AnsiConsole.Status()
                    .StartAsync("Applying version filter...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Star);
                        ctx.SpinnerStyle(Style.Parse("yellow"));

                        await Task.Run(() =>
                        {
                            Regex versionRegex = new(settings.VersionFilter, RegexOptions.IgnoreCase);
                            packages = packages.Where(p => versionRegex.IsMatch(p.Version)).ToList();
                        });
                    });
                AnsiConsole.MarkupLine($"[green]After version filter: {packages.Count} package(s).[/]");
            }

            // Apply latest-only filter if specified
            if (settings.LatestOnly)
            {
                await AnsiConsole.Status()
                    .StartAsync("Filtering to latest versions only...", ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Star);
                        ctx.SpinnerStyle(Style.Parse("yellow"));

                        ImmutableArray<NuGetPackageInfo> latestPackages = scanner.GetLatestVersions([.. packages]);
                        packages = latestPackages.ToList();
                        return Task.CompletedTask;
                    });
                AnsiConsole.MarkupLine($"[green]After latest-only filter: {packages.Count} package(s) to process.[/]");
            }

            if (settings.List)
            {
                DisplayPackageList(packages);
                commandStopwatch.Stop();
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]Total command execution time: {FormatDuration(commandStopwatch.Elapsed)}[/]");
                return 0;
            }

            // Create index manager
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(settings.IndexPath);

            List<NuGetPackageInfo> packagesToIndex;
            HashSet<string> packageIdsToDelete = [];

            if (settings.Clean)
            {
                AnsiConsole.MarkupLine("[yellow]Cleaning index...[/]");
                indexManager.DeleteAll();
                await indexManager.CommitAsync();
                packagesToIndex = packages;
            }
            else
            {
                // Get existing packages from index for change detection
                Dictionary<string, HashSet<(string Version, string Framework)>> indexedPackagesWithFramework = new();
                HashSet<string> indexedXmlPaths = [];
                HashSet<string> emptyXmlPaths = [];

                await AnsiConsole.Status()
                    .StartAsync("Analyzing index for change detection...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Star);
                        ctx.SpinnerStyle(Style.Parse("yellow"));

                        indexedPackagesWithFramework = await Task.Run(() => indexManager.GetIndexedPackageVersionsWithFramework());
                        indexedXmlPaths = await Task.Run(() => indexManager.GetIndexedXmlPaths());
                        emptyXmlPaths = await Task.Run(() => indexManager.GetEmptyXmlPaths());
                    });

                // Use deduplication service for efficient single-pass processing
                PackageDeduplicationResult deduplicationResult = deduplicationService.DeduplicatePackages(
                    packages,
                    indexedPackagesWithFramework,
                    indexedXmlPaths,
                    emptyXmlPaths,
                    settings.LatestOnly);

                packagesToIndex = deduplicationResult.PackagesToIndex.ToList();
                packageIdsToDelete = deduplicationResult.PackageIdsToDelete.ToHashSet();

                // Report deduplication statistics
                DeduplicationStats stats = deduplicationResult.Stats;
                int totalInIndex = indexedPackagesWithFramework.Sum(kvp => kvp.Value.Count);
                int uniquePackageVersions = indexedPackagesWithFramework.Sum(kvp => kvp.Value.Select(v => v.Version).Distinct().Count());
                
                AnsiConsole.MarkupLine($"[dim]Index contains {uniquePackageVersions:N0} package versions across {indexedPackagesWithFramework.Count:N0} packages.[/]");

                if (stats.EmptyXmlFilesSkipped > 0)
                {
                    AnsiConsole.MarkupLine($"[dim]Skipping {stats.EmptyXmlFilesSkipped:N0} known empty XML files.[/]");
                }

                if (stats.UniqueXmlFiles > 0 && stats.TotalScannedPackages > stats.UniqueXmlFiles)
                {
                    AnsiConsole.MarkupLine($"[dim]Found {stats.TotalScannedPackages:N0} package entries sharing {stats.UniqueXmlFiles:N0} unique XML files.[/]");
                }

                if (deduplicationResult.SkippedPackages > 0)
                {
                    AnsiConsole.MarkupLine($"[green]Skipping {deduplicationResult.SkippedPackages:N0} package(s) already up-to-date in index.[/]");
                }

                if (packagesToIndex.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]All packages are already up-to-date. Nothing to index.[/]");
                    commandStopwatch.Stop();
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[dim]Total command execution time: {FormatDuration(commandStopwatch.Elapsed)}[/]");
                    return 0;
                }

                AnsiConsole.MarkupLine($"[green]Found {packagesToIndex.Count:N0} XML file(s) to index ({stats.NewPackages:N0} new, {stats.UpdatedPackages:N0} updated).[/]");

                // Clean up old versions if needed
                if (packageIdsToDelete.Count > 0)
                {
                    int documentsBeforeCleanup = indexManager.GetTotalDocuments();

                    await AnsiConsole.Status()
                        .StartAsync($"Removing old versions from {packageIdsToDelete.Count:N0} package(s)...", async ctx =>
                        {
                            ctx.Spinner(Spinner.Known.Star);
                            ctx.SpinnerStyle(Style.Parse("yellow"));

                            await Task.Run(() =>
                            {
                                indexManager.DeleteDocumentsByPackageIds(packageIdsToDelete);
                            });
                            await indexManager.CommitAsync();
                        });

                    int documentsAfterCleanup = indexManager.GetTotalDocuments();
                    int documentsRemoved = documentsBeforeCleanup - documentsAfterCleanup;

                    if (documentsRemoved > 0)
                    {
                        AnsiConsole.MarkupLine($"[green]Removed {documentsRemoved:N0} API members from old versions.[/]");
                    }
                }
            }

            // Index packages
            IndexingResult result = null!;
            await AnsiConsole.Progress()
                .AutoClear(true)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    ProgressTask task = ctx.AddTask("[green]Indexing NuGet packages[/]", maxValue: packagesToIndex.Count);

                    // Get all XML files from packages
                    List<string> xmlFiles = packagesToIndex.Select(p => p.XmlDocumentationPath).Distinct().ToList();

                    // Index all files using high-performance batch operations
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    result = await indexManager.IndexXmlFilesAsync(
                        xmlFiles,
                        filesProcessed => task.Value = filesProcessed);
                    stopwatch.Stop();

                    task.StopTask();
                });

            // Display results after progress context
            AnsiConsole.WriteLine();
            DisplayResults(result, indexManager, settings.IndexPath);

            commandStopwatch.Stop();
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Total command execution time: {FormatDuration(commandStopwatch.Elapsed)}[/]");

            return 0;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            if (settings.Verbose)
            {
                AnsiConsole.WriteException(ex);
            }
            return 1;
        }
    }

    private static void DisplayPackageList(List<NuGetPackageInfo> packages)
    {
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold yellow]NuGet Packages with XML Documentation[/]");

        table.AddColumn("[bold]Package ID[/]");
        table.AddColumn("[bold]Version[/]");
        table.AddColumn("[bold]Framework[/]");
        table.AddColumn("[bold]XML Size[/]", c => c.RightAligned());

        foreach (NuGetPackageInfo package in packages.OrderBy(p => p.PackageId).ThenBy(p => p.Version))
        {
            FileInfo fileInfo = new(package.XmlDocumentationPath);
            long fileSize = 0;
            try
            {
                fileSize = fileInfo.Length;
            }
            catch (FileNotFoundException)
            {
                // File doesn't exist - use 0 size (common in tests)
                fileSize = 0;
            }

            table.AddRow(
                package.PackageId,
                package.Version,
                package.TargetFramework,
                FormatSize(fileSize));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Total packages:[/] {packages.Count}");
    }

    private static void DisplayResults(IndexingResult result, ILuceneIndexManager indexManager, string indexPath)
    {
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold yellow]NuGet Indexing Results[/]");

        table.AddColumn("[bold]Metric[/]");
        table.AddColumn("[bold]Value[/]", c => c.RightAligned());

        // Basic stats
        table.AddRow("API Members Processed", result.SuccessfulDocuments.ToString("N0"));
        table.AddRow("Failed Members", result.FailedDocuments.ToString("N0"));
        table.AddRow("Total Time", FormatDuration(result.ElapsedTime));
        table.AddRow("Members/Second", result.DocumentsPerSecond.ToString("N2"));
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
            table.AddRow("Total API Members", stats.DocumentCount.ToString("N0"));
            table.AddRow("Index Size", FormatSize(stats.TotalSizeInBytes));
            table.AddRow("Index Location", stats.IndexPath);

            // Show deduplication info if significant difference
            if (result.SuccessfulDocuments > stats.DocumentCount)
            {
                int deduplicated = result.SuccessfulDocuments - stats.DocumentCount;
                double dedupePercent = (deduplicated / (double)result.SuccessfulDocuments) * 100;
                table.AddRow("[dim]Duplicate Members Skipped[/]", $"{deduplicated:N0} ({dedupePercent:N1}%)");
            }
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


    public sealed class Settings : CommandSettings
    {
        [Description("Path to the Lucene index directory")]
        [CommandOption("-i|--index")]
        public string IndexPath { get; init; } = "./index";

        [Description("Clean the index before adding new documents")]
        [CommandOption("-c|--clean")]
        public bool Clean { get; init; }

        [Description("Filter packages by name (supports wildcards)")]
        [CommandOption("-p|--package|--filter")]
        public string? PackageFilter { get; init; }

        [Description("Filter packages by version (regex)")]
        [CommandOption("-v|--version")]
        public string? VersionFilter { get; init; }

        [Description("List packages without indexing")]
        [CommandOption("-l|--list")]
        public bool List { get; init; }

        [Description("Only include the latest version of each package per target framework")]
        [CommandOption("--latest-only")]
        public bool LatestOnly { get; init; }

        [Description("Show verbose output")]
        [CommandOption("--verbose")]
        public bool Verbose { get; init; }
    }
}