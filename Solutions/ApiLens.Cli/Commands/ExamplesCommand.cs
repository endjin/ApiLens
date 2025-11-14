using System.ComponentModel;
using System.Text.Json;
using ApiLens.Cli.Services;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;
using Spectre.Console;

namespace ApiLens.Cli.Commands;

public class ExamplesCommand : Command<ExamplesCommand.Settings>
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

    public ExamplesCommand(
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
            // Create index manager and query engine with the specified path
            // Resolve the actual index path
            string resolvedIndexPath = indexPathResolver.ResolveIndexPath(settings.IndexPath);

            // Create index manager
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(resolvedIndexPath);
            using IQueryEngine queryEngine = queryEngineFactory.Create(indexManager);

            MetadataService metadataService = new();
            metadataService.StartTiming();

            List<MemberInfo> results;
            string? searchPattern = null;

            if (string.IsNullOrWhiteSpace(settings.Pattern))
            {
                // No pattern - get all methods with examples
                results = queryEngine.GetMethodsWithExamples(settings.MaxResults);
            }
            else
            {
                // Search for specific pattern in code examples
                searchPattern = settings.Pattern;
                results = queryEngine.SearchByCodeExample(settings.Pattern, settings.MaxResults);
            }

            if (results.Count == 0)
            {
                if (settings.Format == OutputFormat.Json)
                {
                    OutputJson(results, searchPattern, indexManager, metadataService);
                }
                else
                {
                    string message = searchPattern == null
                        ? "No methods with code examples found."
                        : $"No code examples found matching '{searchPattern}'.";
                    console.MarkupLine($"[yellow]{message}[/]");
                }

                return 0;
            }

            switch (settings.Format)
            {
                case OutputFormat.Json:
                    OutputJson(results, searchPattern, indexManager, metadataService);
                    break;
                case OutputFormat.Markdown:
                    OutputMarkdown(results, searchPattern);
                    break;
                case OutputFormat.Table:
                default:
                    OutputTable(results, searchPattern);
                    break;
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            console.MarkupLine($"[red]Error searching for code examples:[/] {ex.Message}");
            return 1;
        }
    }

    private void OutputJson(List<MemberInfo> results, string? searchPattern,
        ILuceneIndexManager indexManager, MetadataService metadataService)
    {
        var output = results.Select(member => new
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
            codeExamples = member.CodeExamples.Select(ex => new
            {
                description = ex.Description,
                code = ex.Code,
                language = ex.Language
            }),
            matchedPattern = searchPattern
        }).ToList();

        ResponseMetadata metadata = metadataService.BuildMetadata(results, indexManager,
            query: searchPattern,
            queryType: "code-examples",
            commandMetadata: searchPattern != null
                ? new Dictionary<string, object> { ["pattern"] = searchPattern }
                : null);

        JsonResponse<object> response = new()
        {
            Results = output,
            Metadata = metadata
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);

        // Temporarily set unlimited width to prevent JSON wrapping
        var originalWidth = console.Profile.Width;
        console.Profile.Width = int.MaxValue;
        console.WriteLine(json);
        console.Profile.Width = originalWidth;
    }

    private void OutputMarkdown(List<MemberInfo> results, string? searchPattern)
    {
        console.WriteLine("# Code Examples");
        console.WriteLine();

        if (searchPattern != null)
        {
            console.WriteLine($"Search pattern: `{searchPattern}`");
            console.WriteLine();
        }

        console.WriteLine($"Found {results.Count} method(s) with code examples.");
        console.WriteLine();

        foreach (MemberInfo member in results)
        {
            console.WriteLine($"## {member.FullName}");
            console.WriteLine();

            if (!string.IsNullOrWhiteSpace(member.Summary))
            {
                console.WriteLine(member.Summary);
                console.WriteLine();
            }

            foreach (CodeExample example in member.CodeExamples)
            {
                if (!string.IsNullOrWhiteSpace(example.Description))
                {
                    console.WriteLine($"**{example.Description}**");
                    console.WriteLine();
                }

                console.WriteLine($"```{example.Language}");
                console.WriteLine(example.Code);
                console.WriteLine("```");
                console.WriteLine();
            }

            console.WriteLine("---");
            console.WriteLine();
        }
    }

    private void OutputTable(List<MemberInfo> results, string? searchPattern)
    {
        string message = searchPattern == null
            ? $"Found {results.Count} method(s) with code examples:"
            : $"Found {results.Count} method(s) with code examples matching '{searchPattern}':";
        console.MarkupLine($"[green]{message}[/]");
        console.WriteLine();

        foreach (MemberInfo member in results)
        {
            // Display member info
            console.MarkupLine($"[bold cyan]{member.FullName}[/]");

            if (!string.IsNullOrWhiteSpace(member.Summary))
            {
                console.MarkupLine($"[dim]{member.Summary}[/]");
            }

            // Display code examples
            foreach (CodeExample example in member.CodeExamples)
            {
                console.WriteLine();

                if (!string.IsNullOrWhiteSpace(example.Description))
                {
                    console.MarkupLine($"[yellow]{example.Description}[/]");
                }

                Panel codePanel = new Panel(Markup.Escape(example.Code))
                    .Header("[bold]Code Example[/]")
                    .BorderColor(Color.Green)
                    .Padding(1, 1);

                console.Write(codePanel);
            }

            console.WriteLine();
            console.Write(new Rule().RuleStyle("dim"));
            console.WriteLine();
        }
    }

    public sealed class Settings : CommandSettings
    {
        [Description("Pattern to search for in code examples (optional)")]
        [CommandArgument(0, "[pattern]")]
        public string? Pattern { get; init; }

        [Description("Path to the Lucene index directory")]
        [CommandOption("-i|--index")]
        public string IndexPath { get; init; } = "./index";

        [Description("Maximum number of results to return")]
        [CommandOption("-m|--max")]
        public int MaxResults { get; init; } = 10;

        [Description("Output format")]
        [CommandOption("-f|--format")]
        public OutputFormat Format { get; init; } = OutputFormat.Table;
    }
}