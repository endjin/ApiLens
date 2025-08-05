using System.ComponentModel;
using System.Text.Json;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;

namespace ApiLens.Cli.Commands;

public class ExamplesCommand : Command<ExamplesCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ILuceneIndexManagerFactory indexManagerFactory;
    private readonly IQueryEngineFactory queryEngineFactory;

    public ExamplesCommand(
        ILuceneIndexManagerFactory indexManagerFactory,
        IQueryEngineFactory queryEngineFactory)
    {
        ArgumentNullException.ThrowIfNull(indexManagerFactory);
        ArgumentNullException.ThrowIfNull(queryEngineFactory);

        this.indexManagerFactory = indexManagerFactory;
        this.queryEngineFactory = queryEngineFactory;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        try
        {
            // Create index manager and query engine with the specified path
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(settings.IndexPath);
            using IQueryEngine queryEngine = queryEngineFactory.Create(indexManager);
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
                if (settings.Format != OutputFormat.Json)
                {
                    string message = searchPattern == null
                        ? "No methods with code examples found."
                        : $"No code examples found matching '{searchPattern}'.";
                    AnsiConsole.MarkupLine($"[yellow]{message}[/]");
                }
                else
                {
                    // Return empty JSON array
                    AnsiConsole.WriteLine("[]");
                }
                return 0;
            }

            switch (settings.Format)
            {
                case OutputFormat.Json:
                    OutputJson(results, searchPattern);
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
            AnsiConsole.MarkupLine($"[red]Error searching for code examples:[/] {ex.Message}");
            return 1;
        }
    }

    private static void OutputJson(List<MemberInfo> results, string? searchPattern)
    {
        var output = results.Select(member => new
        {
            memberInfo = new
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
        });

        string json = JsonSerializer.Serialize(output, JsonOptions);
        AnsiConsole.WriteLine(json);
    }

    private static void OutputMarkdown(List<MemberInfo> results, string? searchPattern)
    {
        AnsiConsole.WriteLine("# Code Examples");
        AnsiConsole.WriteLine();

        if (searchPattern != null)
        {
            AnsiConsole.WriteLine($"Search pattern: `{searchPattern}`");
            AnsiConsole.WriteLine();
        }

        AnsiConsole.WriteLine($"Found {results.Count} method(s) with code examples.");
        AnsiConsole.WriteLine();

        foreach (MemberInfo member in results)
        {
            AnsiConsole.WriteLine($"## {member.FullName}");
            AnsiConsole.WriteLine();

            if (!string.IsNullOrWhiteSpace(member.Summary))
            {
                AnsiConsole.WriteLine(member.Summary);
                AnsiConsole.WriteLine();
            }

            foreach (CodeExample example in member.CodeExamples)
            {
                if (!string.IsNullOrWhiteSpace(example.Description))
                {
                    AnsiConsole.WriteLine($"**{example.Description}**");
                    AnsiConsole.WriteLine();
                }

                AnsiConsole.WriteLine($"```{example.Language}");
                AnsiConsole.WriteLine(example.Code);
                AnsiConsole.WriteLine("```");
                AnsiConsole.WriteLine();
            }

            AnsiConsole.WriteLine("---");
            AnsiConsole.WriteLine();
        }
    }

    private static void OutputTable(List<MemberInfo> results, string? searchPattern)
    {
        string message = searchPattern == null
            ? $"Found {results.Count} method(s) with code examples:"
            : $"Found {results.Count} method(s) with code examples matching '{searchPattern}':";
        AnsiConsole.MarkupLine($"[green]{message}[/]");
        AnsiConsole.WriteLine();

        foreach (MemberInfo member in results)
        {
            // Display member info
            AnsiConsole.MarkupLine($"[bold cyan]{member.FullName}[/]");

            if (!string.IsNullOrWhiteSpace(member.Summary))
            {
                AnsiConsole.MarkupLine($"[dim]{member.Summary}[/]");
            }

            // Display code examples
            foreach (CodeExample example in member.CodeExamples)
            {
                AnsiConsole.WriteLine();

                if (!string.IsNullOrWhiteSpace(example.Description))
                {
                    AnsiConsole.MarkupLine($"[yellow]{example.Description}[/]");
                }

                Panel codePanel = new Panel(Markup.Escape(example.Code))
                    .Header("[bold]Code Example[/]")
                    .BorderColor(Color.Green)
                    .Padding(1, 1);

                AnsiConsole.Write(codePanel);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule().RuleStyle("dim"));
            AnsiConsole.WriteLine();
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