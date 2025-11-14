using System.ComponentModel;
using System.Diagnostics;
using ApiLens.Cli.Services;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Services;
using Spectre.Console;
using IOPath = System.IO.Path;

namespace ApiLens.Cli.Commands;

/// <summary>
/// Command for analyzing and indexing packages from a project or solution.
/// </summary>
public class AnalyzeCommand : AsyncCommand<AnalyzeCommand.Settings>
{
    private readonly IProjectAnalysisService projectAnalysis;
    private readonly INuGetCacheScanner nugetScanner;
    private readonly ILuceneIndexManagerFactory indexFactory;
    private readonly IFileSystemService fileSystem;
    private readonly IIndexPathResolver indexPathResolver;
    private readonly IAnsiConsole console;

    public AnalyzeCommand(
        IProjectAnalysisService projectAnalysis,
        INuGetCacheScanner nugetScanner,
        ILuceneIndexManagerFactory indexFactory,
        IFileSystemService fileSystem,
        IIndexPathResolver indexPathResolver,
        IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(projectAnalysis);
        ArgumentNullException.ThrowIfNull(nugetScanner);
        ArgumentNullException.ThrowIfNull(indexFactory);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(indexPathResolver);
        ArgumentNullException.ThrowIfNull(console);

        this.projectAnalysis = projectAnalysis;
        this.nugetScanner = nugetScanner;
        this.indexFactory = indexFactory;
        this.fileSystem = fileSystem;
        this.indexPathResolver = indexPathResolver;
        this.console = console;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<PROJECT|SOLUTION>")]
        [Description("Path to .csproj, .fsproj, or .sln file")]
        public string Path { get; set; } = string.Empty;

        [CommandOption("-i|--index <PATH>")]
        [Description("Index directory path (default: ~/.apilens/index or APILENS_INDEX env var)")]
        public string? IndexPath { get; set; }

        [CommandOption("--include-transitive")]
        [Description("Include transitive dependencies")]
        public bool IncludeTransitive { get; set; }

        [CommandOption("--use-assets")]
        [Description("Parse project.assets.json for resolved versions")]
        public bool UseAssetsFile { get; set; }

        [CommandOption("--format <FORMAT>")]
        [Description("Output format: table, json, markdown")]
        public OutputFormat Format { get; set; } = OutputFormat.Table;

        [CommandOption("--clean")]
        [Description("Clean the index before analyzing")]
        public bool CleanIndex { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate input
            if (!projectAnalysis.IsProjectOrSolution(settings.Path))
            {
                console.MarkupLine("[red]Error:[/] File must be a .sln, .csproj, .fsproj, or .vbproj file");
                return 1;
            }

            if (!fileSystem.FileExists(settings.Path))
            {
                console.MarkupLine($"[red]Error:[/] File not found: {settings.Path}");
                return 1;
            }

            // Analyze the project/solution
            console.MarkupLine($"[green]Analyzing:[/] {settings.Path}");

            ProjectAnalysisResult analysisResult = await console.Status()
                .StartAsync("Analyzing project structure...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));
                    return await projectAnalysis.AnalyzeAsync(
                        settings.Path,
                        settings.IncludeTransitive,
                        settings.UseAssetsFile);
                });

            // Display analysis summary
            DisplayAnalysisSummary(analysisResult);

            if (analysisResult.Packages.Count == 0)
            {
                console.MarkupLine("[yellow]No packages found to analyze.[/]");
                return 0;
            }

            // Find packages in NuGet cache
            string cachePath = fileSystem.GetUserNuGetCachePath();
            console.MarkupLine($"[green]NuGet cache:[/] {cachePath}");

            List<NuGetPackageInfo> packagesToIndex = await FindPackagesInCache(analysisResult.Packages, cachePath);

            if (packagesToIndex.Count == 0)
            {
                console.MarkupLine("[yellow]No packages with XML documentation found in NuGet cache.[/]");
                return 0;
            }

            // Index the packages
            string indexPath = indexPathResolver.ResolveIndexPath(settings.IndexPath);
            await IndexPackages(packagesToIndex, indexPath, settings.CleanIndex);

            // Display results
            DisplayResults(analysisResult, packagesToIndex, settings.Format, stopwatch.Elapsed);

            return 0;
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] {ex.Message}");
            if (console.Profile.Capabilities.Unicode)
            {
                console.WriteException(ex);
            }
            return 1;
        }
    }

    private void DisplayAnalysisSummary(ProjectAnalysisResult result)
    {
        Table table = new();
        table.AddColumn("Property");
        table.AddColumn("Value");

        table.AddRow("Type", result.Type.ToString());
        table.AddRow("Projects", result.Statistics.GetValueOrDefault("TotalProjects", 0).ToString());
        table.AddRow("Total Packages", result.Statistics.GetValueOrDefault("TotalPackages", 0).ToString());
        table.AddRow("Direct Packages", result.Statistics.GetValueOrDefault("DirectPackages", 0).ToString());

        if (result.Statistics.GetValueOrDefault("TransitivePackages", 0) > 0)
        {
            table.AddRow("Transitive Packages", result.Statistics["TransitivePackages"].ToString());
        }

        table.AddRow("Frameworks", string.Join(", ", result.Frameworks));

        if (result.Warnings.Any())
        {
            foreach (string warning in result.Warnings)
            {
                console.MarkupLine($"[yellow]Warning:[/] {warning}");
            }
        }

        console.Write(table);
        console.WriteLine();
    }

    private async Task<List<NuGetPackageInfo>> FindPackagesInCache(
        List<PackageReference> packages,
        string cachePath)
    {
        List<NuGetPackageInfo> packagesToIndex = [];

        await console.Status()
            .StartAsync("Locating packages in NuGet cache...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Star);
                ctx.SpinnerStyle(Style.Parse("yellow"));

                await Task.Run(() =>
                {
                    foreach (PackageReference package in packages)
                    {
                        if (string.IsNullOrEmpty(package.Version))
                        {
                            continue;
                        }

                        string packagePath = IOPath.Combine(
                            cachePath,
                            package.Id.ToLowerInvariant(),
                            package.Version.ToLowerInvariant());

                        if (!fileSystem.DirectoryExists(packagePath))
                        {
                            continue;
                        }

                        // Look for XML documentation files
                        List<string> xmlFiles = [.. fileSystem.GetFiles(packagePath, "*.xml", true).Where(f => !f.Contains("xmldocs", StringComparison.OrdinalIgnoreCase))];

                        foreach (string? xmlFile in xmlFiles)
                        {
                            // Extract target framework from path if possible
                            string targetFramework = ExtractTargetFramework(xmlFile) ?? "netstandard2.0";

                            packagesToIndex.Add(new NuGetPackageInfo
                            {
                                PackageId = package.Id,
                                Version = package.Version,
                                TargetFramework = targetFramework,
                                XmlDocumentationPath = xmlFile
                            });
                        }
                    }
                });
            });

        console.MarkupLine($"[green]Found {packagesToIndex.Count} XML documentation files[/]");
        return packagesToIndex;
    }

    private async Task IndexPackages(
        List<NuGetPackageInfo> packages,
        string indexPath,
        bool cleanIndex)
    {
        // Index path is already resolved by the caller
        using ILuceneIndexManager indexManager = indexFactory.Create(indexPath);

        if (cleanIndex)
        {
            console.MarkupLine("[yellow]Cleaning existing index...[/]");
            indexManager.DeleteAll();
            await indexManager.CommitAsync();
        }

        // Group XML files by path for batch processing
        List<string> xmlFiles = [.. packages.Select(p => p.XmlDocumentationPath).Distinct()];

        if (xmlFiles.Count == 0)
        {
            console.MarkupLine("[yellow]No XML documentation files to index.[/]");
            return;
        }

        // Use the high-performance batch indexing
        await console.Progress()
            .StartAsync(async ctx =>
            {
                ProgressTask task = ctx.AddTask("[green]Indexing documentation...[/]", maxValue: xmlFiles.Count);

                IndexingResult result = await indexManager.IndexXmlFilesAsync(
                    xmlFiles,
                    filesProcessed =>
                    {
                        task.Value = filesProcessed;
                        NuGetPackageInfo? currentPackage = packages.FirstOrDefault(p => p.XmlDocumentationPath == xmlFiles[Math.Min(filesProcessed - 1, xmlFiles.Count - 1)]);
                        if (currentPackage != null)
                        {
                            task.Description = $"Indexing {currentPackage.PackageId} {currentPackage.Version}";
                        }
                    });

                task.StopTask();

                console.MarkupLine($"[green]Successfully indexed {result.SuccessfulDocuments:N0} API members[/]");

                if (result.Errors.Length > 0)
                {
                    foreach (string? error in result.Errors.Take(5))
                    {
                        console.MarkupLine($"[yellow]Warning:[/] {error}");
                    }
                    if (result.Errors.Length > 5)
                    {
                        console.MarkupLine($"[yellow]... and {result.Errors.Length - 5} more warnings[/]");
                    }
                }
            });
    }

    private void DisplayResults(
        ProjectAnalysisResult analysisResult,
        List<NuGetPackageInfo> indexedPackages,
        OutputFormat format,
        TimeSpan elapsed)
    {
        switch (format)
        {
            case OutputFormat.Json:
                var grouped = indexedPackages.GroupBy(p => new { p.PackageId, p.Version })
                    .Select(g => new { g.Key.PackageId, g.Key.Version, Files = g.Count() });
                string json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Path = analysisResult.Path,
                    Type = analysisResult.Type.ToString(),
                    Projects = analysisResult.ProjectPaths,
                    TotalPackages = analysisResult.Packages.Count,
                    IndexedFiles = indexedPackages.Count,
                    Packages = grouped,
                    Frameworks = analysisResult.Frameworks,
                    ElapsedSeconds = elapsed.TotalSeconds
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                // Temporarily set unlimited width to prevent JSON wrapping
                var originalWidth = console.Profile.Width;
                console.Profile.Width = int.MaxValue;
                console.WriteLine(json);
                console.Profile.Width = originalWidth;
                break;

            case OutputFormat.Markdown:
                console.WriteLine($"# Analysis Results");
                console.WriteLine();
                console.WriteLine($"**File:** {analysisResult.Path}");
                console.WriteLine($"**Type:** {analysisResult.Type}");
                console.WriteLine($"**Total Packages:** {analysisResult.Packages.Count}");
                console.WriteLine($"**Indexed Files:** {indexedPackages.Count}");
                console.WriteLine();
                console.WriteLine("## Indexed Packages");
                IEnumerable<IGrouping<string, NuGetPackageInfo>> mdGrouped = indexedPackages.GroupBy(p => $"{p.PackageId} {p.Version}");
                foreach (IGrouping<string, NuGetPackageInfo>? group in mdGrouped.OrderBy(g => g.Key))
                {
                    console.WriteLine($"- {group.Key} ({group.Count()} files)");
                }
                break;

            default: // Table
                Table table = new();
                table.AddColumn("Package");
                table.AddColumn("Version");
                table.AddColumn("Indexed");

                foreach (PackageReference? pkg in analysisResult.Packages.OrderBy(p => p.Id))
                {
                    bool indexed = indexedPackages.Any(p =>
                        string.Equals(p.PackageId, pkg.Id, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(p.Version, pkg.Version, StringComparison.OrdinalIgnoreCase));

                    table.AddRow(
                        pkg.Id,
                        pkg.Version ?? "N/A",
                        indexed ? "[green]✓[/]" : "[red]✗[/]"
                    );
                }

                console.Write(table);
                break;
        }

        console.WriteLine();
        console.MarkupLine($"[dim]Analysis completed in {elapsed.TotalSeconds:F2} seconds[/]");
    }

    // GetDefaultIndexPath method removed - now using IIndexPathResolver

    private string? ExtractTargetFramework(string xmlPath)
    {
        // Try to extract target framework from path
        // e.g., /lib/net6.0/MyLib.xml -> net6.0
        string[] parts = xmlPath.Split(IOPath.DirectorySeparatorChar);
        for (int i = parts.Length - 2; i >= 0; i--)
        {
            string part = parts[i];
            if (part.StartsWith("net", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase))
            {
                return part;
            }
        }
        return null;
    }

    public enum OutputFormat
    {
        Table,
        Json,
        Markdown
    }
}