using System.ComponentModel;
using System.Text.Json;
using ApiLens.Core.Formatting;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;

namespace ApiLens.Cli.Commands;

public class QueryCommand : Command<QueryCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILuceneIndexManagerFactory indexManagerFactory;
    private readonly IQueryEngineFactory queryEngineFactory;

    public QueryCommand(
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

            List<MemberInfo> results = settings.QueryType switch
            {
                QueryType.Name => queryEngine.SearchByName(settings.Query, settings.MaxResults),
                QueryType.Content => queryEngine.SearchByContent(settings.Query, settings.MaxResults),
                QueryType.Namespace => queryEngine.SearchByNamespace(settings.Query, settings.MaxResults),
                QueryType.Id => queryEngine.GetById(settings.Query) is { } member ? [member] : [],
                QueryType.Assembly => queryEngine.SearchByAssembly(settings.Query, settings.MaxResults),
                _ => []
            };

            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No results found.[/]");
                return 0;
            }

            switch (settings.Format)
            {
                case OutputFormat.Json:
                    OutputJson(results);
                    break;
                case OutputFormat.Table:
                    OutputTable(results);
                    break;
                case OutputFormat.Markdown:
                    OutputMarkdown(results);
                    break;
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during query:[/] {ex.Message}");
            return 1;
        }
    }

    private static void OutputJson(List<MemberInfo> results)
    {
        string json = JsonSerializer.Serialize(results, JsonOptions);
        AnsiConsole.WriteLine(json);
    }

    private static void OutputTable(List<MemberInfo> results)
    {
        Table table = new();
        table.AddColumn("Type");
        table.AddColumn("Name");
        table.AddColumn("Namespace");
        table.AddColumn("Assembly");
        table.AddColumn("Version");

        foreach (MemberInfo result in results)
        {
            string versionInfo = FormatVersionInfo(result);

            table.AddRow(
                result.MemberType.ToString(),
                Markup.Escape(GenericTypeFormatter.FormatTypeName(result.Name)),
                Markup.Escape(GenericTypeFormatter.FormatTypeName(result.Namespace)),
                Markup.Escape(result.Assembly),
                Markup.Escape(versionInfo)
            );
        }

        AnsiConsole.Write(table);
    }

    private static string FormatVersionInfo(MemberInfo member)
    {
        if (string.IsNullOrWhiteSpace(member.PackageVersion))
        {
            return "N/A";
        }

        List<string> parts = [];

        if (!string.IsNullOrWhiteSpace(member.PackageVersion))
        {
            parts.Add(member.PackageVersion);
        }

        if (!string.IsNullOrWhiteSpace(member.TargetFramework))
        {
            parts.Add($"[{member.TargetFramework}]");
        }

        return string.Join(" ", parts);
    }

    private static void OutputMarkdown(List<MemberInfo> results)
    {
        AnsiConsole.WriteLine("# Query Results");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine($"Found {results.Count} result(s)");
        AnsiConsole.WriteLine();

        foreach (MemberInfo result in results)
        {
            AnsiConsole.WriteLine($"## {GenericTypeFormatter.FormatFullName(result.FullName)}");
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine($"- **Type**: {result.MemberType}");
            AnsiConsole.WriteLine($"- **Namespace**: {GenericTypeFormatter.FormatTypeName(result.Namespace)}");
            AnsiConsole.WriteLine($"- **Assembly**: {result.Assembly}");

            // Add version information if available
            if (!string.IsNullOrWhiteSpace(result.PackageId) || result.IsFromNuGetCache)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine("### Version Information");

                if (!string.IsNullOrWhiteSpace(result.PackageId))
                {
                    string packageDisplay = result.PackageId;
                    if (!string.IsNullOrWhiteSpace(result.PackageVersion))
                    {
                        packageDisplay += $" v{result.PackageVersion}";
                    }
                    AnsiConsole.WriteLine($"- **Package**: {packageDisplay}");
                }

                if (!string.IsNullOrWhiteSpace(result.TargetFramework))
                {
                    AnsiConsole.WriteLine($"- **Framework**: {result.TargetFramework}");
                }

                if (result.IsFromNuGetCache)
                {
                    AnsiConsole.WriteLine($"- **Source**: NuGet Cache");
                }
            }

            if (!string.IsNullOrWhiteSpace(result.Summary))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine("### Summary");
                AnsiConsole.WriteLine(result.Summary);
            }

            AnsiConsole.WriteLine();
        }
    }

    public sealed class Settings : CommandSettings
    {
        [Description("Search query. For content searches, supports Lucene syntax: wildcards (*,?), fuzzy (~), boolean (AND,OR,NOT)")]
        [CommandArgument(0, "<query>")]
        public string Query { get; init; } = string.Empty;

        [Description("Path to the Lucene index directory")]
        [CommandOption("-i|--index")]
        public string IndexPath { get; init; } = "./index";

        [Description("Maximum number of results to return")]
        [CommandOption("-m|--max")]
        public int MaxResults { get; init; } = 10;

        [Description("Type of query to perform")]
        [CommandOption("-t|--type")]
        public QueryType QueryType { get; init; } = QueryType.Name;

        [Description("Output format")]
        [CommandOption("-f|--format")]
        public OutputFormat Format { get; init; } = OutputFormat.Table;
    }

    public enum QueryType
    {
        [Description("Search by exact name match (case-insensitive, no wildcards)")]
        Name,

        [Description("Search in content/documentation (supports Lucene query syntax)")]
        Content,

        [Description("Search by namespace (exact match)")]
        Namespace,

        [Description("Get by exact member ID (e.g., T:System.String, M:System.String.Split)")]
        Id,

        [Description("Search by assembly name (exact match)")]
        Assembly
    }
}