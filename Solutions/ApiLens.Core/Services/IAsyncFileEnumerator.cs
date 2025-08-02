namespace ApiLens.Core.Services;

/// <summary>
/// Provides async enumeration of files with parallel directory scanning support.
/// </summary>
public interface IAsyncFileEnumerator
{
    /// <summary>
    /// Asynchronously enumerates files matching the specified pattern.
    /// </summary>
    /// <param name="path">The root path to search.</param>
    /// <param name="searchPattern">The search pattern (e.g., "*.xml").</param>
    /// <param name="recursive">Whether to search subdirectories.</param>
    /// <param name="maxConcurrency">Maximum number of parallel directory scans.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of file information.</returns>
    IAsyncEnumerable<FileInfo> EnumerateFilesAsync(
        string path,
        string searchPattern,
        bool recursive = false,
        int? maxConcurrency = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously enumerates files in batches for better performance.
    /// </summary>
    /// <param name="path">The root path to search.</param>
    /// <param name="searchPattern">The search pattern (e.g., "*.xml").</param>
    /// <param name="batchSize">Number of files per batch.</param>
    /// <param name="recursive">Whether to search subdirectories.</param>
    /// <param name="maxConcurrency">Maximum number of parallel directory scans.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of file batches.</returns>
    IAsyncEnumerable<IReadOnlyList<FileInfo>> EnumerateFilesBatchedAsync(
        string path,
        string searchPattern,
        int batchSize = 100,
        bool recursive = false,
        int? maxConcurrency = null,
        CancellationToken cancellationToken = default);
}