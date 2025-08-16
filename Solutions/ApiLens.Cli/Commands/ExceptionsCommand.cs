using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApiLens.Cli.Services;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;

namespace ApiLens.Cli.Commands;

public class ExceptionsCommand : Command<ExceptionsCommand.Settings>
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

    public ExceptionsCommand(
        ILuceneIndexManagerFactory indexManagerFactory,
        IIndexPathResolver indexPathResolver,
        IQueryEngineFactory queryEngineFactory)
    {
        ArgumentNullException.ThrowIfNull(indexManagerFactory);
        ArgumentNullException.ThrowIfNull(indexPathResolver);
        ArgumentNullException.ThrowIfNull(queryEngineFactory);

        this.indexManagerFactory = indexManagerFactory;
        this.indexPathResolver = indexPathResolver;
        this.queryEngineFactory = queryEngineFactory;
    }

    public override int Execute(CommandContext context, Settings settings)
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

            List<MemberInfo> results = queryEngine.GetByExceptionType(settings.ExceptionType, settings.MaxResults);

            if (results.Count == 0)
            {
                if (settings.Format == OutputFormat.Json)
                {
                    OutputJson(results, settings.ExceptionType, indexManager, metadataService);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]No methods found that throw {settings.ExceptionType}.[/]");
                }

                return 0;
            }

            switch (settings.Format)
            {
                case OutputFormat.Json:
                    OutputJson(results, settings.ExceptionType, indexManager, metadataService);
                    break;
                case OutputFormat.Markdown:
                    OutputMarkdown(results, settings.ExceptionType, settings.ShowDetails);
                    break;
                case OutputFormat.Table:
                default:
                    OutputTable(results, settings.ExceptionType, settings.ShowDetails);
                    break;
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error searching for exceptions:[/] {ex.Message}");
            return 1;
        }
    }

    private static void OutputJson(List<MemberInfo> results, string exceptionType,
        ILuceneIndexManager indexManager, MetadataService metadataService)
    {
        var output = results.SelectMany(member =>
            member.Exceptions
                .Where(ex => ExceptionTypeMatches(ex.Type, exceptionType))
                .Select(ex => new
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
                    exception = new { type = ex.Type, condition = ex.Condition },
                    searchedType = exceptionType
                })
        ).ToList();

        ResponseMetadata metadata = metadataService.BuildMetadata(results, indexManager,
            exceptionType, "exception",
            new Dictionary<string, object> { ["exceptionType"] = exceptionType });

        JsonResponse<object> response = new()
        {
            Results = output,
            Metadata = metadata
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);

        // Temporarily set unlimited width to prevent JSON wrapping
        var originalWidth = AnsiConsole.Profile.Width;
        AnsiConsole.Profile.Width = int.MaxValue;
        AnsiConsole.WriteLine(json);
        AnsiConsole.Profile.Width = originalWidth;
    }

    private static void OutputMarkdown(List<MemberInfo> results, string exceptionType, bool showDetails)
    {
        AnsiConsole.WriteLine($"# Methods Throwing {exceptionType}");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine($"Found {results.Count} method(s) that throw {exceptionType}.");
        AnsiConsole.WriteLine();

        if (!showDetails)
        {
            AnsiConsole.WriteLine("| Member | Exception | Condition |");
            AnsiConsole.WriteLine("|--------|-----------|-----------|");

            foreach (MemberInfo member in results)
            {
                foreach (ExceptionInfo exception in member.Exceptions)
                {
                    if (ExceptionTypeMatches(exception.Type, exceptionType))
                    {
                        AnsiConsole.WriteLine(
                            $"| {member.FullName} | {exception.Type} | {exception.Condition ?? "No condition specified"} |");
                    }
                }
            }
        }
        else
        {
            foreach (MemberInfo member in results)
            {
                AnsiConsole.WriteLine($"## {member.FullName}");
                AnsiConsole.WriteLine();

                if (!string.IsNullOrWhiteSpace(member.Summary))
                {
                    AnsiConsole.WriteLine(member.Summary);
                    AnsiConsole.WriteLine();
                }

                AnsiConsole.WriteLine("**Exceptions:**");
                foreach (ExceptionInfo exception in member.Exceptions)
                {
                    AnsiConsole.WriteLine($"- **{exception.Type}**");
                    if (!string.IsNullOrWhiteSpace(exception.Condition))
                    {
                        AnsiConsole.WriteLine($"  - {exception.Condition}");
                    }
                }

                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine("---");
                AnsiConsole.WriteLine();
            }
        }
    }

    private static void OutputTable(List<MemberInfo> results, string exceptionType, bool showDetails)
    {
        AnsiConsole.MarkupLine($"[green]Found {results.Count} method(s) that throw {exceptionType}:[/]");
        AnsiConsole.WriteLine();

        Table table = new();
        table.AddColumn("Member");
        table.AddColumn("Exceptions");
        table.AddColumn("Condition");

        foreach (MemberInfo member in results)
        {
            foreach (ExceptionInfo exception in member.Exceptions)
            {
                if (ExceptionTypeMatches(exception.Type, exceptionType))
                {
                    table.AddRow(
                        Markup.Escape(member.FullName),
                        Markup.Escape(exception.Type),
                        Markup.Escape(exception.Condition ?? "No condition specified")
                    );
                }
            }
        }

        AnsiConsole.Write(table);

        // Show detailed information if requested
        if (showDetails)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold]Detailed Information[/]").RuleStyle("dim"));
            AnsiConsole.WriteLine();

            foreach (MemberInfo member in results)
            {
                AnsiConsole.MarkupLine($"[bold cyan]{member.FullName}[/]");

                if (!string.IsNullOrWhiteSpace(member.Summary))
                {
                    AnsiConsole.MarkupLine($"[dim]{member.Summary}[/]");
                }

                AnsiConsole.MarkupLine("[yellow]Exceptions:[/]");
                foreach (ExceptionInfo exception in member.Exceptions)
                {
                    AnsiConsole.MarkupLine($"  • [red]{exception.Type}[/]");
                    if (!string.IsNullOrWhiteSpace(exception.Condition))
                    {
                        AnsiConsole.MarkupLine($"    [dim]{exception.Condition}[/]");
                    }
                }

                AnsiConsole.WriteLine();
            }
        }
    }

    public sealed class Settings : CommandSettings
    {
        [Description("Exception type to search for (e.g., ArgumentNullException)")]
        [CommandArgument(0, "<exception-type>")]
        public string ExceptionType { get; init; } = string.Empty;

        [Description("Path to the Lucene index directory")]
        [CommandOption("-i|--index")]
        public string IndexPath { get; init; } = "./index";

        [Description("Maximum number of results to return")]
        [CommandOption("-m|--max")]
        public int MaxResults { get; init; } = 10;

        [Description("Show detailed information for each result")]
        [CommandOption("-d|--details")]
        public bool ShowDetails { get; init; }

        [Description("Output format")]
        [CommandOption("-f|--format")]
        public OutputFormat Format { get; init; } = OutputFormat.Table;
    }

    /// <summary>
    /// Determines if an exception type matches a search pattern, supporting wildcards.
    /// </summary>
    /// <param name="exceptionType">The actual exception type</param>
    /// <param name="searchPattern">The search pattern, potentially with wildcards</param>
    /// <returns>True if the exception type matches the search pattern</returns>
    private static bool ExceptionTypeMatches(string exceptionType, string searchPattern)
    {
        // Since the search logic now handles all matching strategies,
        // this method only needs to verify that the exception was found
        // by the search. The main check is ensuring the display matches
        // what the user searched for.

        if (searchPattern.Contains('*') || searchPattern.Contains('?'))
        {
            // For wildcard patterns, verify the match
            string regexPattern = "^" +
                                  Regex.Escape(searchPattern)
                                      .Replace("\\*", ".*")
                                      .Replace("\\?", ".") +
                                  "$";

            // Check full type or simple name
            if (Regex.IsMatch(exceptionType, regexPattern, RegexOptions.IgnoreCase))
            {
                return true;
            }

            if (!searchPattern.Contains('.'))
            {
                string simpleName = exceptionType.Contains('.')
                    ? exceptionType.Substring(exceptionType.LastIndexOf('.') + 1)
                    : exceptionType;
                return Regex.IsMatch(simpleName, regexPattern, RegexOptions.IgnoreCase);
            }

            return false;
        }

        // For non-wildcard searches, check various matching strategies
        // 1. Contains check (handles partial matches)
        if (exceptionType.Contains(searchPattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. If search has namespace, check if exception name matches
        if (searchPattern.Contains('.') && exceptionType.Contains('.'))
        {
            string searchName = searchPattern.Substring(searchPattern.LastIndexOf('.') + 1);
            string exceptionName = exceptionType.Substring(exceptionType.LastIndexOf('.') + 1);
            return string.Equals(searchName, exceptionName, StringComparison.OrdinalIgnoreCase);
        }

        // 3. Simple name match (search for "IOException" matches "System.IO.IOException")
        if (!searchPattern.Contains('.') && exceptionType.Contains('.'))
        {
            string exceptionName = exceptionType.Substring(exceptionType.LastIndexOf('.') + 1);
            return string.Equals(searchPattern, exceptionName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}