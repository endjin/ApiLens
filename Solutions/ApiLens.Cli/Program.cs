using ApiLens.Cli.Commands;
using ApiLens.Cli.Services;
using ApiLens.Core.Helpers;
using ApiLens.Core.Lucene;
using ApiLens.Core.Parsing;
using ApiLens.Core.Querying;
using ApiLens.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ApiLens.Cli;

internal class Program
{
    public static int Main(string[] args)
    {
        ServiceCollection services = new();

        // Register services
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IEnvironment, Spectre.IO.Environment>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IAsyncFileEnumerator, AsyncFileEnumerator>();
        services.AddSingleton<IFileHashHelper, FileHashHelper>();
        services.AddSingleton<IXmlDocumentParser, XmlDocumentParser>();
        services.AddSingleton<IDocumentBuilder, DocumentBuilder>();
        services.AddSingleton<INuGetCacheScanner, AsyncNuGetCacheScanner>();
        services.AddSingleton<IPackageDeduplicationService, PackageDeduplicationService>();
        services.AddSingleton<ILuceneIndexManagerFactory, LuceneIndexManagerFactory>();
        services.AddSingleton<IQueryEngineFactory, QueryEngineFactory>();

        TypeRegistrar registrar = new(services);
        CommandApp app = new(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("apilens");
            config.SetApplicationVersion("1.0.0");

            config.AddCommand<IndexCommand>("index")
                .WithDescription("Index XML documentation files")
                .WithExample("index", "./docs")
                .WithExample("index", "./docs", "--clean")
                .WithExample("index", "./MyLib.xml", "--index", "./custom-index");

            config.AddCommand<QueryCommand>("query")
                .WithDescription(@"Query the API documentation index.

QUERY SYNTAX:
  Name searches (default): Require exact matches, case-insensitive
  Content searches: Support full Lucene query syntax including:
    - Wildcards: * (multiple chars), ? (single char)
      Example: string* matches string, strings, stringify
    - Fuzzy: ~ for similar terms
      Example: roam~ matches foam, roams
    - Boolean: AND, OR, NOT (must be uppercase)
      Example: string AND utility
    - Phrases: Use quotes for exact phrases
      Example: ""strongly typed""
    
SEARCH TYPES:
  name      - Exact name match (default)
  content   - Full-text search in documentation
  namespace - Exact namespace match
  id        - Exact member ID (e.g., T:System.String)
  assembly  - Exact assembly name match")
                .WithExample("query", "String")
                .WithExample("query", "List<T>")
                .WithExample("query", "string*", "--type", "content")
                .WithExample("query", "utilit?", "--type", "content")
                .WithExample("query", "tokenze~", "--type", "content")
                .WithExample("query", "\"extension methods\"", "--type", "content")
                .WithExample("query", "string AND manipulation", "--type", "content")
                .WithExample("query", "System.Collections.Generic", "--type", "namespace")
                .WithExample("query", "T:System.String", "--type", "id", "--format", "json");

            config.AddCommand<ExamplesCommand>("examples")
                .WithDescription("Search for code examples or list methods with examples")
                .WithExample("examples")
                .WithExample("examples", "CalculateTotal")
                .WithExample("examples", "async", "--max", "20");

            config.AddCommand<ExceptionsCommand>("exceptions")
                .WithDescription("Find methods that throw specific exceptions")
                .WithExample("exceptions", "ArgumentNullException")
                .WithExample("exceptions", "System.IO.IOException", "--details")
                .WithExample("exceptions", "ValidationException", "--max", "50");

            config.AddCommand<ComplexityCommand>("complexity")
                .WithDescription("Analyze method complexity and parameter counts")
                .WithExample("complexity", "--min-params", "5")
                .WithExample("complexity", "--min-complexity", "10", "--stats")
                .WithExample("complexity", "--min-params", "2", "--max-params", "4", "--sort", "params");

            config.AddCommand<StatsCommand>("stats")
                .WithDescription("Display index statistics including size, document count, and field information")
                .WithExample("stats")
                .WithExample("stats", "--index", "./custom-index")
                .WithExample("stats", "--format", "json");

            config.AddCommand<NuGetCommand>("nuget")
                .WithDescription(@"Scan and index NuGet package cache.

Automatically discovers your NuGet cache location and indexes all packages
with XML documentation. Supports filtering, latest-version selection, and
listing packages without indexing.")
                .WithExample("nuget")
                .WithExample("nuget", "--clean", "--latest")
                .WithExample("nuget", "--filter", "microsoft.*", "--latest")
                .WithExample("nuget", "--list")
                .WithExample("nuget", "--list", "--filter", "system.*");
        });

        return app.Run(args);
    }
}