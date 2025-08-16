using System.Collections.Immutable;
using System.ComponentModel;
using System.Text.Json;
using ApiLens.Cli.Services;
using ApiLens.Core.Formatting;
using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;

namespace ApiLens.Cli.Commands;

/// <summary>
/// Command for exploring type hierarchies including base types, derived types, and interfaces.
/// </summary>
public class HierarchyCommand : Command<HierarchyCommand.Settings>
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

    public HierarchyCommand(
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
            // Resolve the actual index path
            string resolvedIndexPath = indexPathResolver.ResolveIndexPath(settings.IndexPath);

            // Create index manager
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(resolvedIndexPath);
            using IQueryEngine queryEngine = queryEngineFactory.Create(indexManager);

            MetadataService metadataService = new();
            metadataService.StartTiming();

            // Find the type
            var typeResults = queryEngine.SearchByName(settings.TypeName, 10);
            var targetType = typeResults.FirstOrDefault(m =>
                m.MemberType == MemberType.Type &&
                (m.Name.Equals(settings.TypeName, StringComparison.OrdinalIgnoreCase) ||
                 m.FullName.Equals(settings.TypeName, StringComparison.OrdinalIgnoreCase)));

            if (targetType == null)
            {
                AnsiConsole.MarkupLine($"[yellow]Type '{settings.TypeName}' not found.[/]");

                // Provide suggestions
                var suggestions = typeResults
                    .Where(m => m.MemberType == MemberType.Type)
                    .Take(5)
                    .Select(m => m.FullName)
                    .ToList();

                if (suggestions.Any())
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Did you mean one of these?[/]");
                    foreach (var suggestion in suggestions)
                    {
                        AnsiConsole.MarkupLine($"  [dim]- {Markup.Escape(suggestion)}[/]");
                    }
                }
                return 0;
            }

            // Build hierarchy information
            var hierarchy = BuildTypeHierarchy(queryEngine, targetType, settings);

            // Output results
            switch (settings.Format)
            {
                case OutputFormat.Json:
                    OutputJson(hierarchy, targetType, indexManager, metadataService, settings);
                    break;
                case OutputFormat.Table:
                    OutputTable(hierarchy, targetType, settings);
                    break;
                case OutputFormat.Markdown:
                    OutputMarkdown(hierarchy, targetType, settings);
                    break;
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private TypeHierarchy BuildTypeHierarchy(IQueryEngine queryEngine, MemberInfo targetType, Settings settings)
    {
        var baseTypes = new List<MemberInfo>();
        var interfaces = new List<MemberInfo>();
        var derivedTypes = new List<MemberInfo>();
        var members = new List<MemberInfo>();

        // Get related types from cross references
        if (targetType.CrossReferences.Any())
        {
            foreach (var crossRef in targetType.CrossReferences)
            {
                // Use Inheritance type for base classes and interfaces
                if (crossRef.Type == ReferenceType.Inheritance)
                {
                    var relatedType = queryEngine.GetById(crossRef.TargetId);
                    if (relatedType != null)
                    {
                        // Try to determine if it's an interface or base class by name convention
                        if (relatedType.Name.StartsWith("I") && relatedType.Name.Length > 1 && char.IsUpper(relatedType.Name[1]))
                            interfaces.Add(relatedType);
                        else
                            baseTypes.Add(relatedType);
                    }
                }
            }
        }

        // Search for derived types (types that might inherit from this type)
        // This is a heuristic approach since we don't have direct derived type info
        var allTypes = queryEngine.SearchByContent($"\"{targetType.FullName}\"", settings.MaxDerivedTypes * 2);
        derivedTypes = allTypes
            .Where(t => t.MemberType == MemberType.Type &&
                       t.Id != targetType.Id &&
                       (t.Summary?.Contains(targetType.Name) == true ||
                        t.CrossReferences.Any(cr => cr.TargetId == targetType.Id)))
            .Take(settings.MaxDerivedTypes)
            .ToList();

        // Get type members if requested
        if (settings.ShowMembers)
        {
            members = queryEngine.GetTypeMembers(targetType.FullName, settings.MaxMembers);
        }

        return new TypeHierarchy
        {
            Type = targetType,
            BaseTypes = [.. baseTypes],
            DerivedTypes = [.. derivedTypes],
            Interfaces = [.. interfaces],
            Members = [.. members]
        };
    }

    private static void OutputJson(TypeHierarchy hierarchy, MemberInfo targetType,
        ILuceneIndexManager indexManager, MetadataService metadataService, Settings settings)
    {
        var metadata = metadataService.BuildMetadata(
            results: [targetType],
            indexManager: indexManager,
            query: settings.TypeName,
            queryType: "hierarchy");

        var response = new
        {
            type = hierarchy.Type,
            baseTypes = hierarchy.BaseTypes,
            derivedTypes = hierarchy.DerivedTypes,
            interfaces = hierarchy.Interfaces,
            members = settings.ShowMembers ? hierarchy.Members : [],
            metadata = metadata
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);

        // Temporarily set unlimited width to prevent JSON wrapping
        var originalWidth = AnsiConsole.Profile.Width;
        AnsiConsole.Profile.Width = int.MaxValue;
        AnsiConsole.WriteLine(json);
        AnsiConsole.Profile.Width = originalWidth;
    }

    private static void OutputTable(TypeHierarchy hierarchy, MemberInfo targetType, Settings settings)
    {
        // Type information
        AnsiConsole.Write(new Rule($"[bold yellow]{GenericTypeFormatter.FormatFullName(targetType.FullName)}[/]"));
        AnsiConsole.WriteLine();

        if (!string.IsNullOrWhiteSpace(targetType.Summary))
        {
            AnsiConsole.MarkupLine("[dim]Summary:[/]");
            AnsiConsole.WriteLine(targetType.Summary);
            AnsiConsole.WriteLine();
        }

        // Base types
        if (hierarchy.BaseTypes.Any())
        {
            AnsiConsole.MarkupLine("[bold]Base Types:[/]");
            foreach (var baseType in hierarchy.BaseTypes)
            {
                AnsiConsole.MarkupLine($"  ← {Markup.Escape(GenericTypeFormatter.FormatFullName(baseType.FullName))}");
            }
            AnsiConsole.WriteLine();
        }

        // Interfaces
        if (hierarchy.Interfaces.Any())
        {
            AnsiConsole.MarkupLine("[bold]Implements:[/]");
            foreach (var iface in hierarchy.Interfaces)
            {
                AnsiConsole.MarkupLine($"  ◊ {Markup.Escape(GenericTypeFormatter.FormatFullName(iface.FullName))}");
            }
            AnsiConsole.WriteLine();
        }

        // Derived types
        if (hierarchy.DerivedTypes.Any())
        {
            AnsiConsole.MarkupLine("[bold]Derived Types:[/]");
            foreach (var derived in hierarchy.DerivedTypes)
            {
                AnsiConsole.MarkupLine($"  → {Markup.Escape(GenericTypeFormatter.FormatFullName(derived.FullName))}");
            }
            AnsiConsole.WriteLine();
        }

        // Members
        if (settings.ShowMembers && hierarchy.Members.Any())
        {
            AnsiConsole.MarkupLine("[bold]Members:[/]");

            var memberGroups = hierarchy.Members.GroupBy(m => m.MemberType);
            foreach (var group in memberGroups.OrderBy(g => g.Key))
            {
                AnsiConsole.MarkupLine($"  [dim]{group.Key}s:[/]");
                foreach (var member in group.OrderBy(m => m.Name))
                {
                    string memberDisplay = member.MemberType switch
                    {
                        MemberType.Method => $"    • {member.Name}({member.Parameters.Length} params)",
                        MemberType.Property => $"    • {member.Name}",
                        MemberType.Field => $"    • {member.Name}",
                        MemberType.Event => $"    • {member.Name}",
                        _ => $"    • {member.Name}"
                    };
                    AnsiConsole.MarkupLine(Markup.Escape(memberDisplay));
                }
            }
        }
    }

    private static void OutputMarkdown(TypeHierarchy hierarchy, MemberInfo targetType, Settings settings)
    {
        // Type header
        AnsiConsole.WriteLine($"# {GenericTypeFormatter.FormatFullName(targetType.FullName)}");
        AnsiConsole.WriteLine();

        if (!string.IsNullOrWhiteSpace(targetType.Summary))
        {
            AnsiConsole.WriteLine(targetType.Summary);
            AnsiConsole.WriteLine();
        }

        // Metadata
        AnsiConsole.WriteLine("## Type Information");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine($"- **Namespace**: {targetType.Namespace}");
        AnsiConsole.WriteLine($"- **Assembly**: {targetType.Assembly}");
        if (!string.IsNullOrWhiteSpace(targetType.PackageId))
        {
            AnsiConsole.WriteLine($"- **Package**: {targetType.PackageId} v{targetType.PackageVersion}");
        }
        AnsiConsole.WriteLine();

        // Base types
        if (hierarchy.BaseTypes.Any())
        {
            AnsiConsole.WriteLine("## Inheritance Hierarchy");
            AnsiConsole.WriteLine();
            foreach (var baseType in hierarchy.BaseTypes)
            {
                AnsiConsole.WriteLine($"- ← `{GenericTypeFormatter.FormatFullName(baseType.FullName)}`");
            }
            AnsiConsole.WriteLine();
        }

        // Interfaces
        if (hierarchy.Interfaces.Any())
        {
            AnsiConsole.WriteLine("## Implemented Interfaces");
            AnsiConsole.WriteLine();
            foreach (var iface in hierarchy.Interfaces)
            {
                AnsiConsole.WriteLine($"- `{GenericTypeFormatter.FormatFullName(iface.FullName)}`");
            }
            AnsiConsole.WriteLine();
        }

        // Derived types
        if (hierarchy.DerivedTypes.Any())
        {
            AnsiConsole.WriteLine("## Known Derived Types");
            AnsiConsole.WriteLine();
            foreach (var derived in hierarchy.DerivedTypes)
            {
                AnsiConsole.WriteLine($"- → `{GenericTypeFormatter.FormatFullName(derived.FullName)}`");
            }
            AnsiConsole.WriteLine();
        }

        // Members
        if (settings.ShowMembers && hierarchy.Members.Any())
        {
            AnsiConsole.WriteLine("## Members");
            AnsiConsole.WriteLine();

            var memberGroups = hierarchy.Members.GroupBy(m => m.MemberType);
            foreach (var group in memberGroups.OrderBy(g => g.Key))
            {
                AnsiConsole.WriteLine($"### {group.Key}s");
                AnsiConsole.WriteLine();

                foreach (var member in group.OrderBy(m => m.Name))
                {
                    string memberLine = member.MemberType switch
                    {
                        MemberType.Method => $"- `{member.Name}()` - {member.Parameters.Length} parameter(s)",
                        _ => $"- `{member.Name}`"
                    };
                    AnsiConsole.WriteLine(memberLine);

                    if (!string.IsNullOrWhiteSpace(member.Summary))
                    {
                        AnsiConsole.WriteLine($"  {member.Summary}");
                    }
                }
                AnsiConsole.WriteLine();
            }
        }
    }

    private class TypeHierarchy
    {
        public required MemberInfo Type { get; init; }
        public required ImmutableArray<MemberInfo> BaseTypes { get; init; }
        public required ImmutableArray<MemberInfo> DerivedTypes { get; init; }
        public required ImmutableArray<MemberInfo> Interfaces { get; init; }
        public required ImmutableArray<MemberInfo> Members { get; init; }
    }

    public sealed class Settings : CommandSettings
    {
        [Description("The type name to explore (can be simple name or fully qualified)")]
        [CommandArgument(0, "<type>")]
        public string TypeName { get; init; } = string.Empty;

        [Description("Show type members (methods, properties, etc.)")]
        [CommandOption("-m|--members")]
        public bool ShowMembers { get; init; }

        [Description("Maximum number of derived types to show")]
        [CommandOption("--max-derived")]
        public int MaxDerivedTypes { get; init; } = 20;

        [Description("Maximum number of members to show per type")]
        [CommandOption("--max-members")]
        public int MaxMembers { get; init; } = 50;

        [Description("Path to the Lucene index directory")]
        [CommandOption("-i|--index")]
        public string IndexPath { get; init; } = "./index";

        [Description("Output format")]
        [CommandOption("-f|--format")]
        public OutputFormat Format { get; init; } = OutputFormat.Table;
    }
}