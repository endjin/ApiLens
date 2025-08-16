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
    /// 1. Explicitly provided path (if not null or default)
    /// 2. APILENS_INDEX environment variable
    /// 3. ~/.apilens/index (user home directory)
    /// </summary>
    public string ResolveIndexPath(string? providedPath)
    {
        // 1. If an explicit path is provided and it's not the default, use it
        // Note: We check if it's exactly the default value from Settings
        if (!string.IsNullOrWhiteSpace(providedPath))
        {
            // Check if this is the default value from Settings (not user-provided)
            // The default value in Settings is "./index" - if user explicitly provides
            // a path (even if it's "./index"), we should honor it
            // We can detect this by checking if the path exists or was explicitly set
            DirectoryPath dirPath = new(providedPath);
            DirectoryPath absolutePath = dirPath.MakeAbsolute(environment);

            // If the path exists or is not the exact default, use it
            if (fileSystem.Exist(absolutePath) || providedPath != "./index")
            {
                return absolutePath.FullPath;
            }
        }

        // 2. Check for environment variable
        string? envIndexPath = environment.GetEnvironmentVariable("APILENS_INDEX");
        if (!string.IsNullOrWhiteSpace(envIndexPath))
        {
            DirectoryPath dirPath = new(envIndexPath);
            return dirPath.MakeAbsolute(environment).FullPath;
        }

        // 3. Use default location in user's home directory
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