using ApiLens.Core.Services;

namespace ApiLens.Cli.Services;

/// <summary>
/// Cross-platform file system service implementation using Spectre.IO.
/// </summary>
public class FileSystemService : IFileSystemService
{
    private readonly IFileSystem fileSystem;
    private readonly IEnvironment environment;

    public FileSystemService(IFileSystem fileSystem, IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(environment);

        this.fileSystem = fileSystem;
        this.environment = environment;
    }

    public bool FileExists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FilePath filePath = new(path);
        return fileSystem.Exist(filePath);
    }

    public bool DirectoryExists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        DirectoryPath dirPath = new(path);
        return fileSystem.Exist(dirPath);
    }

    public IEnumerable<string> GetFiles(string path, string pattern, bool recursive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        DirectoryPath dirPath = new(path);

        if (!fileSystem.Exist(dirPath))
        {
            return [];
        }

        IDirectory directory = fileSystem.GetDirectory(dirPath);
        SearchScope scope = recursive ? SearchScope.Recursive : SearchScope.Current;
        return directory.GetFiles(pattern, scope).Select(f => f.Path.FullPath);
    }

    public string CombinePath(params string[] paths)
    {
        if (paths == null || paths.Length == 0)
            throw new ArgumentException("At least one path segment is required", nameof(paths));

        DirectoryPath result = new(paths[0]);
        for (int i = 1; i < paths.Length; i++)
        {
            result = result.Combine(paths[i]);
        }

        return result.FullPath;
    }

    public FileInfo GetFileInfo(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FilePath filePath = new(path);
        IFile file = fileSystem.GetFile(filePath);

        // Convert Spectre.IO file to System.IO.FileInfo
        return new FileInfo(file.Path.FullPath);
    }

    public DirectoryInfo GetDirectoryInfo(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        DirectoryPath dirPath = new(path);
        IDirectory directory = fileSystem.GetDirectory(dirPath);

        // Convert Spectre.IO directory to System.IO.DirectoryInfo
        return new DirectoryInfo(directory.Path.FullPath);
    }

    public string GetUserNuGetCachePath()
    {
        // Check for environment variable override first
        string? nugetPackages = environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(nugetPackages))
        {
            return nugetPackages;
        }

        // Use platform-specific default location
        string homeDirectory = environment.HomeDirectory.FullPath;
        return CombinePath(homeDirectory, ".nuget", "packages");
    }

    public string GetFileName(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FilePath filePath = new(path);
        return filePath.GetFilename().ToString();
    }

    public string? GetDirectoryName(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FilePath filePath = new(path);
        DirectoryPath? parent = filePath.GetDirectory();
        return parent?.FullPath;
    }

    public IEnumerable<FileInfo> EnumerateFiles(string path, string? pattern = null, bool recursive = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        DirectoryPath dirPath = new(path);

        if (!fileSystem.Exist(dirPath))
        {
            return [];
        }

        IDirectory directory = fileSystem.GetDirectory(dirPath);
        string searchPattern = pattern ?? "*";
        SearchScope scope = recursive ? SearchScope.Recursive : SearchScope.Current;

        return directory.GetFiles(searchPattern, scope)
            .Select(f => new FileInfo(f.Path.FullPath));
    }

    public Stream OpenRead(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FilePath filePath = new(path);
        IFile file = fileSystem.GetFile(filePath);
        return file.OpenRead();
    }

    public Task<Stream> OpenReadAsync(string path)
    {
        // FIXED: Use FileStream with async support for better I/O performance
        // The async benefit comes from the stream operations, not opening it
        FileStream fileStream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, useAsync: true);
        return Task.FromResult<Stream>(fileStream);
    }

    public IEnumerable<DirectoryInfo> EnumerateDirectories(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        DirectoryPath dirPath = new(path);

        if (!fileSystem.Exist(dirPath))
        {
            return [];
        }

        IDirectory directory = fileSystem.GetDirectory(dirPath);

        return directory.GetDirectories("*", SearchScope.Current)
            .Select(d => new DirectoryInfo(d.Path.FullPath));
    }
}