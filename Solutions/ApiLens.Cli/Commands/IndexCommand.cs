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

public class IndexCommand : Command<IndexCommand.Settings>
{
    private readonly IXmlDocumentParser parser;
    private readonly IDocumentBuilder documentBuilder;
    private readonly IFileSystemService fileSystem;
    private readonly ILuceneIndexManagerFactory indexManagerFactory;

    public IndexCommand(
        IXmlDocumentParser parser,
        IDocumentBuilder documentBuilder,
        IFileSystemService fileSystem,
        ILuceneIndexManagerFactory indexManagerFactory)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(documentBuilder);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(indexManagerFactory);

        this.parser = parser;
        this.documentBuilder = documentBuilder;
        this.fileSystem = fileSystem;
        this.indexManagerFactory = indexManagerFactory;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!fileSystem.FileExists(settings.Path) && !fileSystem.DirectoryExists(settings.Path))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Path '{settings.Path}' does not exist.");
            return 1;
        }

        try
        {
            // Create index manager with the specified path
            using ILuceneIndexManager indexManager = indexManagerFactory.Create(settings.IndexPath);

            if (settings.Clean)
            {
                AnsiConsole.MarkupLine("[yellow]Cleaning index...[/]");
                indexManager.DeleteAll();
                indexManager.Commit();
            }

            List<string> files = GetXmlFiles(settings.Path, settings.Pattern);

            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No XML files found to index.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[green]Found {files.Count} XML file(s) to index.[/]");

            int indexed = 0;
            int failed = 0;
            int totalMembers = 0;

            foreach (string file in files)
            {
                AnsiConsole.MarkupLine($"Processing: [blue]{fileSystem.GetFileName(file)}[/]");

                try
                {
                    XDocument document = XDocument.Load(file);
                    ApiAssemblyInfo assembly = parser.ParseAssembly(document);
                    ImmutableArray<MemberInfo> members = parser.ParseMembers(document, assembly.Name);

                    // Extract NuGet package info if this is from a NuGet cache
                    (string PackageId, string Version, string Framework)? nugetInfo = ExtractNuGetInfo(file);

                    foreach (MemberInfo member in members)
                    {
                        // If we have NuGet info, enhance the member with version information
                        MemberInfo enrichedMember = member;
                        if (nugetInfo.HasValue)
                        {
                            enrichedMember = member with
                            {
                                PackageId = nugetInfo.Value.PackageId,
                                PackageVersion = nugetInfo.Value.Version,
                                TargetFramework = nugetInfo.Value.Framework,
                                IsFromNuGetCache = true,
                                SourceFilePath = file
                            };
                        }

                        Document doc = documentBuilder.BuildDocument(enrichedMember);
                        indexManager.AddDocument(doc);
                        totalMembers++;
                    }

                    indexed++;
                }
                catch (System.Xml.XmlException ex)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to parse XML:[/] {file} - {ex.Message}");
                    failed++;
                }
                catch (IOException ex)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to read:[/] {file} - {ex.Message}");
                    failed++;
                }
            }

            indexManager.Commit();

            // Get index statistics
            IndexStatistics? stats = indexManager.GetIndexStatistics();

            AnsiConsole.MarkupLine("[green]Indexing complete![/]");
            AnsiConsole.MarkupLine($"  Files processed: {indexed}");
            AnsiConsole.MarkupLine($"  Members indexed: {totalMembers}");
            if (failed > 0)
            {
                AnsiConsole.MarkupLine($"  Failed: {failed} file(s)");
            }

            if (stats != null)
            {
                AnsiConsole.MarkupLine($"  Index size: {FormatSize(stats.TotalSizeInBytes)}");
                AnsiConsole.MarkupLine($"  Index location: {stats.IndexPath}");
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during indexing:[/] {ex.Message}");
            return 1;
        }
    }

    private List<string> GetXmlFiles(string path, string? pattern)
    {
        List<string> files = [];

        if (fileSystem.FileExists(path) && path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            files.Add(path);
        }
        else if (fileSystem.DirectoryExists(path))
        {
            pattern ??= "*.xml";
            bool recursive = pattern.Contains("**");
            string searchPattern = pattern.Replace("**/", "");

            files.AddRange(fileSystem.GetFiles(path, searchPattern, recursive));
        }

        return files;
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

    /// <summary>
    /// Extracts NuGet package information from a file path.
    /// </summary>
    public static (string PackageId, string Version, string Framework)? ExtractNuGetInfo(string filePath)
    {
        // Pattern to match NuGet cache paths: .../packageid/version/lib|ref/framework/*.xml
        Regex regex = new(@"[\\/](?<packageId>[^\\/]+)[\\/](?<version>[^\\/]+)[\\/](?:lib|ref)[\\/](?<framework>[^\\/]+)[\\/][^\\/]+\.xml$", RegexOptions.IgnoreCase);
        Match match = regex.Match(filePath);

        if (match.Success)
        {
            return (
                PackageId: match.Groups["packageId"].Value,
                Version: match.Groups["version"].Value,
                Framework: match.Groups["framework"].Value
            );
        }

        return null;
    }

    public sealed class Settings : CommandSettings
    {
        [Description("Path to XML documentation file or directory")]
        [CommandArgument(0, "<path>")]
        public string Path { get; init; } = string.Empty;

        [Description("Path to the Lucene index directory")]
        [CommandOption("-i|--index")]
        public string IndexPath { get; init; } = "./index";

        [Description("Clean the index before adding new documents")]
        [CommandOption("-c|--clean")]
        public bool Clean { get; init; }

        [Description("File pattern for matching files (when path is a directory)")]
        [CommandOption("-p|--pattern")]
        public string? Pattern { get; init; }
    }
}