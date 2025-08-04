namespace ApiLens.Core.Services;

/// <summary>
/// Provides cross-platform file system operations abstraction.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the file exists; otherwise, false.</returns>
    bool FileExists(string path);

    /// <summary>
    /// Checks if a directory exists at the specified path.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the directory exists; otherwise, false.</returns>
    bool DirectoryExists(string path);

    /// <summary>
    /// Gets files matching the specified pattern.
    /// </summary>
    /// <param name="path">The directory path to search in.</param>
    /// <param name="pattern">The search pattern (supports wildcards and globbing).</param>
    /// <param name="recursive">Whether to search subdirectories.</param>
    /// <returns>Collection of file paths matching the pattern.</returns>
    IEnumerable<string> GetFiles(string path, string pattern, bool recursive);

    /// <summary>
    /// Combines path segments in a cross-platform manner.
    /// </summary>
    /// <param name="paths">The path segments to combine.</param>
    /// <returns>The combined path.</returns>
    string CombinePath(params string[] paths);

    /// <summary>
    /// Gets file information for the specified path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>File information.</returns>
    FileInfo GetFileInfo(string path);

    /// <summary>
    /// Gets directory information for the specified path.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>Directory information.</returns>
    DirectoryInfo GetDirectoryInfo(string path);

    /// <summary>
    /// Gets the user's NuGet cache path for the current platform.
    /// </summary>
    /// <returns>The NuGet cache directory path.</returns>
    string GetUserNuGetCachePath();

    /// <summary>
    /// Gets the file name from a path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The file name.</returns>
    string GetFileName(string path);

    /// <summary>
    /// Gets the directory name from a path.
    /// </summary>
    /// <param name="path">The file or directory path.</param>
    /// <returns>The directory name.</returns>
    string? GetDirectoryName(string path);

    /// <summary>
    /// Enumerates all files in a directory with detailed information.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <param name="pattern">Optional search pattern.</param>
    /// <param name="recursive">Whether to search subdirectories.</param>
    /// <returns>Enumerable of file information.</returns>
    IEnumerable<FileInfo> EnumerateFiles(string path, string? pattern = null, bool recursive = false);

    /// <summary>
    /// Opens a file for reading.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>A stream for reading the file.</returns>
    Stream OpenRead(string path);

    /// <summary>
    /// Opens a file for reading asynchronously.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>A stream for reading the file.</returns>
    Task<Stream> OpenReadAsync(string path);

    /// <summary>
    /// Enumerates all directories in a directory.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>Enumerable of directory information.</returns>
    IEnumerable<DirectoryInfo> EnumerateDirectories(string path);
}