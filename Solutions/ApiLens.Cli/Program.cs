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
        services.AddSingleton<ISolutionParserService, SolutionParserService>();
        services.AddSingleton<IProjectParserService, ProjectParserService>();
        services.AddSingleton<IAssetFileParserService, AssetFileParserService>();
        services.AddSingleton<IProjectAnalysisService, ProjectAnalysisService>();
        services.AddSingleton<MetadataService>();

        TypeRegistrar registrar = new(services);
        CommandApp app = new(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("apilens");
            config.SetApplicationVersion("1.0.0");

            // Configure top-level examples to show the drill-down workflow
            config.AddExample("analyze", "./MySolution.sln");
            config.AddExample("nuget", "--list", "--filter", "Newtonsoft.*");
            config.AddExample("list-types", "--package", "Newtonsoft.Json");
            config.AddExample("query", "JObject");
            config.AddExample("hierarchy", "JObject", "--show-members");
            config.AddExample("query", "Parse", "--type", "method", "--min-params", "1");
            config.AddExample("examples", "Parse");

            config.AddCommand<IndexCommand>("index")
                .WithDescription(HelpText.IndexCommandDescription)
                .WithExample("index", "./docs")
                .WithExample("index", "./docs", "--clean")
                .WithExample("index", "./MyLib.xml", "--index", "./custom-index")
                .WithExample("index", "C:\\Program Files\\dotnet\\packs", "--pattern", "**/*.xml");

            config.AddCommand<QueryCommand>("query")
                .WithDescription(HelpText.QueryCommandDescription)
                .WithExample("query", "String")
                .WithExample("query", "List<T>")
                .WithExample("query", "Parse", "--type", "method")
                .WithExample("query", "Parse", "--type", "method", "--min-params", "1", "--max-params", "2")
                .WithExample("query", "string*", "--type", "content")
                .WithExample("query", "\"extension methods\"", "--type", "content")
                .WithExample("query", "async AND task", "--type", "content")
                .WithExample("query", "System.Collections.Generic", "--type", "namespace")
                .WithExample("query", "List", "--member-type", "Type", "--namespace", "System.Collections.*")
                .WithExample("query", "T:System.String", "--type", "id", "--format", "json");

            config.AddCommand<ExamplesCommand>("examples")
                .WithDescription(HelpText.ExamplesCommandDescription)
                .WithExample("examples")
                .WithExample("examples", "CalculateTotal")
                .WithExample("examples", "async", "--max", "20")
                .WithExample("examples", "Console.WriteLine")
                .WithExample("examples", "--format", "json", "--max", "10");

            config.AddCommand<ExceptionsCommand>("exceptions")
                .WithDescription(HelpText.ExceptionsCommandDescription)
                .WithExample("exceptions", "ArgumentNullException")
                .WithExample("exceptions", "System.IO.IOException", "--details")
                .WithExample("exceptions", "*ValidationException")
                .WithExample("exceptions", "*Exception", "--max", "10")
                .WithExample("exceptions", "IOException", "--format", "json");

            config.AddCommand<ComplexityCommand>("complexity")
                .WithDescription(HelpText.ComplexityCommandDescription)
                .WithExample("complexity", "--min-params", "5")
                .WithExample("complexity", "--max-params", "1")
                .WithExample("complexity", "--min-params", "2", "--max-params", "4")
                .WithExample("complexity", "--min-complexity", "10", "--stats")
                .WithExample("complexity", "--stats", "--format", "json");

            config.AddCommand<StatsCommand>("stats")
                .WithDescription(HelpText.StatsCommandDescription)
                .WithExample("stats")
                .WithExample("stats", "--doc-metrics")
                .WithExample("stats", "--doc-metrics", "--format", "json")
                .WithExample("stats", "--index", "./custom-index");

            config.AddCommand<HierarchyCommand>("hierarchy")
                .WithDescription(HelpText.HierarchyCommandDescription)
                .WithExample("hierarchy", "String")
                .WithExample("hierarchy", "List")
                .WithExample("hierarchy", "Dictionary", "--show-members")
                .WithExample("hierarchy", "IEnumerable", "--format", "json")
                .WithExample("hierarchy", "Exception", "--show-members", "--show-inherited");

            config.AddCommand<ListTypesCommand>("list-types")
                .WithDescription(HelpText.ListTypesCommandDescription)
                .WithExample("list-types", "--package", "Newtonsoft.Json")
                .WithExample("list-types", "--package", "Microsoft.*", "--max", "50")
                .WithExample("list-types", "--namespace", "System.Collections.*")
                .WithExample("list-types", "--package", "Newtonsoft.Json", "--namespace", "Newtonsoft.Json.Linq")
                .WithExample("list-types", "--assembly", "System.*", "--include-members")
                .WithExample("list-types", "--package", "Serilog.*", "--format", "json");

            config.AddCommand<NuGetCommand>("nuget")
                .WithDescription(HelpText.NuGetCommandDescription)
                .WithExample("nuget")
                .WithExample("nuget", "--clean", "--latest-only")
                .WithExample("nuget", "--filter", "Microsoft.*", "--latest-only")
                .WithExample("nuget", "--filter", "System.*", "--latest-only")
                .WithExample("nuget", "--list")
                .WithExample("nuget", "--list", "--filter", "Newtonsoft.*");

            config.AddCommand<AnalyzeCommand>("analyze")
                .WithDescription(HelpText.AnalyzeCommandDescription)
                .WithExample("analyze", "./MyProject.csproj")
                .WithExample("analyze", "./MySolution.sln")
                .WithExample("analyze", "./MyProject.csproj", "--include-transitive")
                .WithExample("analyze", "./MySolution.sln", "--clean")
                .WithExample("analyze", "./src/MyProject/MyProject.csproj", "--use-assets", "--format", "json");
        });

        return app.Run(args);
    }
}