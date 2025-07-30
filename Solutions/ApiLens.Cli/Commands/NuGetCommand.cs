using System.Collections.Immutable;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Services;
using Lucene.Net.Documents;

namespace ApiLens.Cli.Commands;

/// <summary>
/// Command for scanning and indexing NuGet package cache.
/// </summary>
public class NuGetCommand : Command<NuGetCommand.Settings>
{
    private readonly IFileSystemService fileSystem;
    private readonly INuGetCacheScanner scanner;

    public NuGetCommand(IFileSystemService fileSystem, INuGetCacheScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(scanner);

        this.fileSystem = fileSystem;
        this.scanner = scanner;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
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
            AnsiConsole.Status()
                .Start("Scanning NuGet cache...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));
                });

            ImmutableArray<NuGetPackageInfo> packages = scanner.ScanNuGetCache();

            // Apply package filter if specified
            if (!string.IsNullOrWhiteSpace(settings.PackageFilter))
            {
                Regex filterRegex = new(settings.PackageFilter, RegexOptions.IgnoreCase);
                packages = [.. packages.Where(p => filterRegex.IsMatch(p.PackageId))];
            }

            // Apply latest-only filter if specified
            if (settings.LatestOnly)
            {
                packages = scanner.GetLatestVersions(packages);
            }

            AnsiConsole.MarkupLine($"[green]Found {packages.Length} package(s) with XML documentation[/]");

            // If list-only, display packages and exit
            if (settings.ListOnly)
            {
                DisplayPackageList(packages);
                return 0;
            }

            // Index the packages
            return IndexPackages(packages, settings);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static void DisplayPackageList(ImmutableArray<NuGetPackageInfo> packages)
    {
        Table table = new();
        table.AddColumn("Package");
        table.AddColumn("Version");
        table.AddColumn("Framework");
        table.AddColumn("Path");

        foreach (NuGetPackageInfo package in packages.OrderBy(p => p.PackageId).ThenBy(p => p.Version).ThenBy(p => p.TargetFramework))
        {
            table.AddRow(
                package.PackageId,
                package.Version,
                package.TargetFramework,
                Markup.Escape(package.XmlDocumentationPath)
            );
        }

        AnsiConsole.Write(table);
    }

    private static int IndexPackages(ImmutableArray<NuGetPackageInfo> packages, Settings settings)
    {
        using LuceneIndexManager indexManager = new(settings.IndexPath);

        if (settings.Clean)
        {
            AnsiConsole.MarkupLine("[yellow]Cleaning index...[/]");
            indexManager.DeleteAll();
            indexManager.Commit();
        }

        XmlDocumentParser parser = new();
        DocumentBuilder documentBuilder = new();
        int totalMembers = 0;
        int failed = 0;

        AnsiConsole.Progress()
            .Start(ctx =>
            {
                ProgressTask task = ctx.AddTask("[green]Indexing packages[/]", maxValue: packages.Length);

                foreach (NuGetPackageInfo package in packages)
                {
                    task.Description = $"[green]Indexing {package.PackageId} v{package.Version} ({package.TargetFramework})[/]";

                    try
                    {
                        // Load and parse the XML file
                        XDocument document = XDocument.Load(package.XmlDocumentationPath);
                        ApiAssemblyInfo assembly = parser.ParseAssembly(document);
                        ImmutableArray<MemberInfo> members = parser.ParseMembers(document, assembly.Name);

                        foreach (MemberInfo member in members)
                        {
                            // Enhance member with NuGet package information
                            MemberInfo enrichedMember = member with
                            {
                                PackageId = package.PackageId,
                                PackageVersion = package.Version,
                                TargetFramework = package.TargetFramework,
                                IsFromNuGetCache = true,
                                SourceFilePath = package.XmlDocumentationPath,
                                IndexedAt = package.IndexedAt
                            };

                            Document doc = documentBuilder.BuildDocument(enrichedMember);
                            indexManager.AddDocument(doc);
                            totalMembers++;
                        }
                    }
                    catch (System.Xml.XmlException ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to parse XML for {package.PackageId}:[/] {ex.Message}");
                        failed++;
                    }
                    catch (IOException ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to read {package.PackageId}:[/] {ex.Message}");
                        failed++;
                    }

                    task.Increment(1);
                }
            });

        indexManager.Commit();

        // Get index statistics
        IndexStatistics? stats = indexManager.GetIndexStatistics();

        AnsiConsole.MarkupLine($"[green]Indexing complete![/]");
        AnsiConsole.MarkupLine($"  Packages processed: {packages.Length - failed}");
        AnsiConsole.MarkupLine($"  Members indexed: {totalMembers}");
        if (failed > 0)
        {
            AnsiConsole.MarkupLine($"  Failed: {failed} package(s)");
        }

        if (stats != null)
        {
            AnsiConsole.MarkupLine($"  Index size: {FormatSize(stats.TotalSizeInBytes)}");
            AnsiConsole.MarkupLine($"  Index location: {stats.IndexPath}");
        }

        return 0;
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

        [Description("Clean the index before adding new documents")]
        [CommandOption("-c|--clean")]
        public bool Clean { get; init; }

        [Description("Only index the latest version of each package")]
        [CommandOption("-l|--latest")]
        public bool LatestOnly { get; init; }

        [Description("Filter packages by regex pattern (e.g., 'newtonsoft.*')")]
        [CommandOption("-f|--filter")]
        public string? PackageFilter { get; init; }

        [Description("List packages without indexing")]
        [CommandOption("--list")]
        public bool ListOnly { get; init; }
    }
}