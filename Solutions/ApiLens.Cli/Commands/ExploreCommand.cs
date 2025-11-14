using System.ComponentModel;
using System.Text.Json;
using ApiLens.Cli.Services;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;
using Spectre.Console;

namespace ApiLens.Cli.Commands;

/// <summary>
/// Interactive package exploration command that provides guided navigation through a package's API.
/// </summary>
public class ExploreCommand : Command<ExploreCommand.Settings>
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
    private readonly IAnsiConsole console;

    public ExploreCommand(
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
            // Resolve the actual index path
            string resolvedIndexPath = indexPathResolver.ResolveIndexPath(settings.IndexPath);

            // Create index manager
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(resolvedIndexPath);
            using IQueryEngine queryEngine = queryEngineFactory.Create(indexManager);

            MetadataService metadataService = new();
            metadataService.StartTiming();

            // Get all types from the package
            var packageTypes = queryEngine.SearchByPackage(settings.PackageName, 10000);

            if (packageTypes.Count == 0)
            {
                console.MarkupLine($"[yellow]Package '{settings.PackageName}' not found or has no indexed types.[/]");

                // Suggest similar packages
                var allPackages = queryEngine.SearchByPackage("*", 100)
                    .Select(m => m.PackageId)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct()
                    .Where(p => p != null && p.Contains(settings.PackageName.Replace("*", ""), StringComparison.OrdinalIgnoreCase))
                    .Take(5)
                    .ToList();

                if (allPackages.Any())
                {
                    console.WriteLine();
                    console.MarkupLine("[dim]Did you mean one of these packages?[/]");
                    foreach (var pkg in allPackages)
                    {
                        console.MarkupLine($"  [dim]- {Markup.Escape(pkg ?? "Unknown")}[/]");
                    }
                }
                return 0;
            }

            // Analyze the package
            var analysis = AnalyzePackage(packageTypes);

            // Output results
            switch (settings.Format)
            {
                case OutputFormat.Json:
                    OutputJson(analysis, indexManager, metadataService);
                    break;
                default:
                    OutputInteractive(analysis, settings);
                    break;
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private PackageAnalysis AnalyzePackage(List<MemberInfo> packageTypes)
    {
        // Group by namespace
        var namespaces = packageTypes
            .Where(t => t.MemberType == MemberType.Type)
            .GroupBy(t => t.Namespace)
            .Select(g => new NamespaceInfo
            {
                Name = g.Key,
                TypeCount = g.Count(),
                Types = g.ToList()
            })
            .OrderBy(n => n.Name)
            .ToList();

        // Identify key types (interfaces, main classes, exceptions)
        var interfaces = packageTypes.Where(t => t.MemberType == MemberType.Type && t.Name.StartsWith("I") && t.Name.Length > 1 && char.IsUpper(t.Name[1])).ToList();
        var exceptions = packageTypes.Where(t => t.MemberType == MemberType.Type && t.Name.EndsWith("Exception")).ToList();
        var staticClasses = packageTypes.Where(t => t.MemberType == MemberType.Type && t.Summary?.Contains("static") == true).ToList();

        // Find entry point types (types with static methods like Create, Parse, etc.)
        var entryPointTypes = packageTypes
            .Where(t => t.MemberType == MemberType.Type)
            .Where(t => packageTypes.Any(m =>
                m.MemberType == MemberType.Method &&
                m.FullName.StartsWith(t.FullName + ".") &&
                (m.Name.Equals("Create", StringComparison.OrdinalIgnoreCase) ||
                 m.Name.Equals("Parse", StringComparison.OrdinalIgnoreCase) ||
                 m.Name.Equals("Load", StringComparison.OrdinalIgnoreCase) ||
                 m.Name.Equals("Open", StringComparison.OrdinalIgnoreCase) ||
                 m.Name.Equals("Build", StringComparison.OrdinalIgnoreCase) ||
                 m.Name.Equals("From", StringComparison.OrdinalIgnoreCase))))
            .Take(10)
            .ToList();

        // Calculate documentation metrics
        var totalTypes = packageTypes.Count(t => t.MemberType == MemberType.Type);
        var typesWithDocs = packageTypes.Count(t => t.MemberType == MemberType.Type && !string.IsNullOrWhiteSpace(t.Summary));
        var methodsWithExamples = packageTypes.Count(m => m.MemberType == MemberType.Method && m.CodeExamples.Any());

        // Find most complex types (by member count)
        var typeComplexity = packageTypes
            .Where(t => t.MemberType == MemberType.Type)
            .Select(t => new
            {
                Type = t,
                MemberCount = packageTypes.Count(m => m.FullName.StartsWith(t.FullName + "."))
            })
            .OrderByDescending(t => t.MemberCount)
            .Take(10)
            .Select(t => new TypeComplexity
            {
                Type = t.Type,
                MemberCount = t.MemberCount
            })
            .ToList();

        return new PackageAnalysis
        {
            PackageName = packageTypes.FirstOrDefault()?.PackageId ?? "Unknown",
            PackageVersion = packageTypes.FirstOrDefault()?.PackageVersion ?? "Unknown",
            Namespaces = namespaces,
            TotalTypes = totalTypes,
            TotalMembers = packageTypes.Count,
            Interfaces = interfaces,
            Exceptions = exceptions,
            StaticClasses = staticClasses,
            EntryPointTypes = entryPointTypes,
            DocumentationCoverage = totalTypes > 0 ? (double)typesWithDocs / totalTypes : 0,
            MethodsWithExamples = methodsWithExamples,
            MostComplexTypes = typeComplexity
        };
    }

    private void OutputInteractive(PackageAnalysis analysis, Settings settings)
    {
        // Package header
        var rule = new Rule($"[bold yellow]{analysis.PackageName} v{analysis.PackageVersion}[/]")
        {
            Justification = Justify.Left
        };
        console.Write(rule);
        console.WriteLine();

        // Overview statistics
        var overviewTable = new Table()
        {
            Border = TableBorder.Rounded
        };
        overviewTable.AddColumn("[bold]Metric[/]");
        overviewTable.AddColumn("[bold]Value[/]");

        overviewTable.AddRow("Total Types", analysis.TotalTypes.ToString());
        overviewTable.AddRow("Total Members", analysis.TotalMembers.ToString());
        overviewTable.AddRow("Namespaces", analysis.Namespaces.Count.ToString());
        overviewTable.AddRow("Documentation Coverage", $"{analysis.DocumentationCoverage:P0}");
        overviewTable.AddRow("Methods with Examples", analysis.MethodsWithExamples.ToString());

        console.Write(overviewTable);
        console.WriteLine();

        // Key namespaces
        if (analysis.Namespaces.Any())
        {
            console.Write(new Rule("[bold]Namespaces[/]") { Justification = Justify.Left });

            var namespaceTable = new Table()
            {
                Border = TableBorder.Simple
            };
            namespaceTable.AddColumn("Namespace");
            namespaceTable.AddColumn("Types", column => column.RightAligned());
            namespaceTable.AddColumn("Key Types");

            foreach (var ns in analysis.Namespaces.OrderByDescending(n => n.TypeCount).Take(settings.MaxNamespaces))
            {
                var keyTypes = ns.Types
                    .Where(t => t.Name.Length > 2 && !t.Name.Contains("<") && !t.Name.Contains("`"))
                    .OrderByDescending(t => analysis.EntryPointTypes.Contains(t) ? 1 : 0)
                    .ThenBy(t => t.Name)
                    .Take(3)
                    .Select(t => Markup.Escape(t.Name ?? "Unknown"));

                namespaceTable.AddRow(
                    Markup.Escape(ns.Name ?? "Unknown"),
                    ns.TypeCount.ToString(),
                    string.Join(", ", keyTypes)
                );
            }

            console.Write(namespaceTable);
            console.WriteLine();
        }

        // Entry point types
        if (analysis.EntryPointTypes.Any())
        {
            console.Write(new Rule("[bold]Entry Point Types[/] [dim](Good starting points)[/]") { Justification = Justify.Left });

            foreach (var type in analysis.EntryPointTypes.Take(settings.MaxEntryPoints))
            {
                console.MarkupLine($"  [green]→[/] [bold]{Markup.Escape(type.Name)}[/]");
                if (!string.IsNullOrWhiteSpace(type.Summary))
                {
                    var summary = type.Summary.Length > 100 ? type.Summary.Substring(0, 100) + "..." : type.Summary;
                    console.MarkupLine($"    [dim]{Markup.Escape(summary)}[/]");
                }
            }
            console.WriteLine();
        }

        // Main interfaces
        if (analysis.Interfaces.Any())
        {
            console.Write(new Rule("[bold]Key Interfaces[/]") { Justification = Justify.Left });

            foreach (var iface in analysis.Interfaces.OrderBy(i => i.Name).Take(settings.MaxInterfaces))
            {
                console.MarkupLine($"  [blue]◊[/] {Markup.Escape(iface.Name)}");
            }
            console.WriteLine();
        }

        // Complex types (might need attention)
        if (settings.ShowComplexity && analysis.MostComplexTypes.Any())
        {
            console.Write(new Rule("[bold]Most Complex Types[/] [dim](By member count)[/]") { Justification = Justify.Left });

            var complexTable = new Table()
            {
                Border = TableBorder.Simple
            };
            complexTable.AddColumn("Type");
            complexTable.AddColumn("Members", column => column.RightAligned());

            foreach (var complex in analysis.MostComplexTypes.Take(5))
            {
                complexTable.AddRow(
                    Markup.Escape(complex.Type.Name),
                    complex.MemberCount.ToString()
                );
            }

            console.Write(complexTable);
            console.WriteLine();
        }

        // Suggested next steps
        console.Write(new Rule("[bold]Suggested Next Steps[/]") { Justification = Justify.Left });
        console.WriteLine();

        if (analysis.EntryPointTypes.Any())
        {
            var firstEntry = analysis.EntryPointTypes.First();
            console.MarkupLine($"1. Explore the main type: [cyan]apilens hierarchy \"{firstEntry.Name}\" --show-members[/]");
        }

        if (analysis.Namespaces.Any())
        {
            var mainNamespace = analysis.Namespaces.OrderByDescending(n => n.TypeCount).First();
            console.MarkupLine($"2. Browse namespace types: [cyan]apilens list-types --namespace \"{mainNamespace.Name}\"[/]");
        }

        console.MarkupLine($"3. Search for examples: [cyan]apilens examples --max 10[/]");

        if (analysis.Interfaces.Any())
        {
            var firstInterface = analysis.Interfaces.First();
            console.MarkupLine($"4. Find implementations: [cyan]apilens hierarchy \"{firstInterface.Name}\"[/]");
        }
    }

    private void OutputJson(PackageAnalysis analysis, ILuceneIndexManager indexManager, MetadataService metadataService)
    {
        var metadata = metadataService.BuildMetadata(
            results: [],
            indexManager: indexManager,
            query: analysis.PackageName,
            queryType: "explore");

        var response = new
        {
            package = new
            {
                name = analysis.PackageName,
                version = analysis.PackageVersion,
                totalTypes = analysis.TotalTypes,
                totalMembers = analysis.TotalMembers,
                documentationCoverage = analysis.DocumentationCoverage,
                methodsWithExamples = analysis.MethodsWithExamples
            },
            namespaces = analysis.Namespaces.Select(n => new
            {
                name = n.Name,
                typeCount = n.TypeCount,
                keyTypes = n.Types.Take(5).Select(t => t.Name)
            }),
            entryPointTypes = analysis.EntryPointTypes.Select(t => new
            {
                name = t.Name,
                fullName = t.FullName,
                summary = t.Summary
            }),
            interfaces = analysis.Interfaces.Select(i => new
            {
                name = i.Name,
                fullName = i.FullName
            }),
            exceptions = analysis.Exceptions.Select(e => new
            {
                name = e.Name,
                fullName = e.FullName
            }),
            mostComplexTypes = analysis.MostComplexTypes.Select(c => new
            {
                name = c.Type.Name,
                memberCount = c.MemberCount
            }),
            metadata = metadata
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);

        // Temporarily set unlimited width to prevent JSON wrapping
        var originalWidth = console.Profile.Width;
        console.Profile.Width = int.MaxValue;
        console.WriteLine(json);
        console.Profile.Width = originalWidth;
    }

    private class PackageAnalysis
    {
        public required string PackageName { get; init; }
        public required string PackageVersion { get; init; }
        public required List<NamespaceInfo> Namespaces { get; init; }
        public required int TotalTypes { get; init; }
        public required int TotalMembers { get; init; }
        public required List<MemberInfo> Interfaces { get; init; }
        public required List<MemberInfo> Exceptions { get; init; }
        public required List<MemberInfo> StaticClasses { get; init; }
        public required List<MemberInfo> EntryPointTypes { get; init; }
        public required double DocumentationCoverage { get; init; }
        public required int MethodsWithExamples { get; init; }
        public required List<TypeComplexity> MostComplexTypes { get; init; }
    }

    private class NamespaceInfo
    {
        public required string Name { get; init; }
        public required int TypeCount { get; init; }
        public required List<MemberInfo> Types { get; init; }
    }

    private class TypeComplexity
    {
        public required MemberInfo Type { get; init; }
        public required int MemberCount { get; init; }
    }

    public sealed class Settings : CommandSettings
    {
        [Description("The package name to explore (supports wildcards)")]
        [CommandArgument(0, "<package>")]
        public string PackageName { get; init; } = string.Empty;

        [Description("Path to the Lucene index directory")]
        [CommandOption("-i|--index")]
        public string IndexPath { get; init; } = "./index";

        [Description("Output format")]
        [CommandOption("-f|--format")]
        public OutputFormat Format { get; init; } = OutputFormat.Table;

        [Description("Maximum namespaces to show")]
        [CommandOption("--max-namespaces")]
        public int MaxNamespaces { get; init; } = 10;

        [Description("Maximum entry point types to show")]
        [CommandOption("--max-entry-points")]
        public int MaxEntryPoints { get; init; } = 5;

        [Description("Maximum interfaces to show")]
        [CommandOption("--max-interfaces")]
        public int MaxInterfaces { get; init; } = 10;

        [Description("Show type complexity analysis")]
        [CommandOption("--show-complexity")]
        public bool ShowComplexity { get; init; }
    }
}