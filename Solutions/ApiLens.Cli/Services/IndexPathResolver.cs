using ApiLens.Core.Services;

namespace ApiLens.Cli.Services;

/// <summary>
/// Resolves the index path based on configuration, environment variables, or defaults.
/// </summary>
public class IndexPathResolver : IIndexPathResolver
{
    private readonly IFileSystemService fileSystemService;
    private readonly IFileSystem fileSystem;
    private readonly IEnvironment environment;
    private const string DefaultIndexDirectoryName = ".apilens";
    private const string DefaultIndexSubdirectory = "index";

    public IndexPathResolver(IFileSystemService fileSystemService, IFileSystem fileSystem, IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(fileSystemService);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(environment);

        this.fileSystemService = fileSystemService;
        this.fileSystem = fileSystem;
        this.environment = environment;
    }

    /// <summary>
    /// Resolves the index path using the following priority:
    /// 1. Explicitly provided path (if not null/empty)
    /// 2. APILENS_INDEX environment variable
    /// 3. ~/.apilens/index (user home directory) - the unconditional default
    /// </summary>
    /// <remarks>
    /// This method always returns a consistent path regardless of the current working directory.
    /// The default location (~/.apilens/index) ensures Claude Code and other tools always use
    /// the same index regardless of which directory they invoke commands from.
    /// </remarks>
    public string ResolveIndexPath(string? providedPath)
    {
        // 1. If an explicit path is provided (not null/empty), use it
        if (!string.IsNullOrWhiteSpace(providedPath))
        {
            DirectoryPath dirPath = new(providedPath);
            DirectoryPath absolutePath = dirPath.MakeAbsolute(environment);
            return absolutePath.FullPath;
        }

        // 2. Check for environment variable
        string? envIndexPath = environment.GetEnvironmentVariable("APILENS_INDEX");
        if (!string.IsNullOrWhiteSpace(envIndexPath))
        {
            DirectoryPath dirPath = new(envIndexPath);
            return dirPath.MakeAbsolute(environment).FullPath;
        }

        // 3. Use default location in user's home directory (unconditional default)
        DirectoryPath homeDir = environment.HomeDirectory;
        DirectoryPath defaultPath = homeDir.Combine(DefaultIndexDirectoryName).Combine(DefaultIndexSubdirectory);

        // Ensure the directory exists
        if (!fileSystem.Exist(defaultPath))
        {
            IDirectory dir = fileSystem.GetDirectory(defaultPath);
            dir.Create();
        }

        return defaultPath.FullPath;
    }

    /// <summary>
    /// Gets the default index path for display purposes.
    /// </summary>
    public string GetDefaultIndexPath()
    {
        string? envIndexPath = environment.GetEnvironmentVariable("APILENS_INDEX");
        if (!string.IsNullOrWhiteSpace(envIndexPath))
        {
            return envIndexPath;
        }

        DirectoryPath homeDir = environment.HomeDirectory;
        DirectoryPath defaultPath = homeDir.Combine(DefaultIndexDirectoryName).Combine(DefaultIndexSubdirectory);
        return defaultPath.FullPath;
    }
}

/// <summary>
/// Interface for resolving index paths.
/// </summary>
public interface IIndexPathResolver
{
    /// <summary>
    /// Resolves the index path based on configuration.
    /// </summary>
    string ResolveIndexPath(string? providedPath);

    /// <summary>
    /// Gets the default index path for display purposes.
    /// </summary>
    string GetDefaultIndexPath();
}