using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ApiLens.Core.Services;

/// <summary>
/// High-performance async file enumerator with parallel directory scanning.
/// </summary>
public class AsyncFileEnumerator : IAsyncFileEnumerator
{
    private readonly IFileSystemService fileSystem;

    private sealed class ScanState
    {
        private int activeDirectories;
        private int activeScanners;

        public TaskCompletionSource ScanningComplete { get; } = new();

        public void IncrementDirectories(int count) => Interlocked.Add(ref activeDirectories, count);
        public bool DecrementDirectory() => Interlocked.Decrement(ref activeDirectories) == 0;
        public void IncrementScanner() => Interlocked.Increment(ref activeScanners);
        public void DecrementScanner() => Interlocked.Decrement(ref activeScanners);
    }

    public AsyncFileEnumerator(IFileSystemService fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        this.fileSystem = fileSystem;
    }

    public async IAsyncEnumerable<FileInfo> EnumerateFilesAsync(
        string path,
        string searchPattern,
        bool recursive = false,
        int? maxConcurrency = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!fileSystem.DirectoryExists(path))
        {
            yield break;
        }

        // Use bounded concurrency for parallel directory scanning
        int concurrency = maxConcurrency ?? Math.Min(Environment.ProcessorCount, 8);

        if (!recursive)
        {
            // Non-recursive case: simple enumeration
            foreach (FileInfo file in fileSystem.EnumerateFiles(path, searchPattern, false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
            }

            yield break;
        }

        // Recursive case: use parallel scanning with channels
        Channel<FileInfo> fileChannel = Channel.CreateUnbounded<FileInfo>(new UnboundedChannelOptions
        {
            SingleWriter = false, SingleReader = true
        });

        Channel<string> directoryChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleWriter = false, SingleReader = false
        });

        // Start with the root directory
        await directoryChannel.Writer.WriteAsync(path, cancellationToken);

        // Track active directories to process
        ScanState scanState = new();
        scanState.IncrementDirectories(1); // Starting with root directory

        // Create scanner tasks
        Task[] scanners = new Task[concurrency];
        for (int i = 0; i < concurrency; i++)
        {
            scanners[i] = ScanDirectoriesAsync(
                directoryChannel.Reader,
                directoryChannel.Writer,
                fileChannel.Writer,
                searchPattern,
                scanState,
                cancellationToken);
        }

        // Monitor completion
        _ = Task.Run(async () =>
        {
            await scanState.ScanningComplete.Task;
            directoryChannel.Writer.Complete();
            await Task.WhenAll(scanners);
            fileChannel.Writer.Complete();
        }, cancellationToken);

        // Yield results as they come
        await foreach (FileInfo file in fileChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return file;
        }
    }

    public async IAsyncEnumerable<IReadOnlyList<FileInfo>> EnumerateFilesBatchedAsync(
        string path,
        string searchPattern,
        int batchSize = 100,
        bool recursive = false,
        int? maxConcurrency = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<FileInfo> batch = new(batchSize);

        await foreach (FileInfo file in EnumerateFilesAsync(path, searchPattern, recursive, maxConcurrency,
                           cancellationToken))
        {
            batch.Add(file);

            if (batch.Count >= batchSize)
            {
                yield return batch.ToArray();
                batch.Clear();
            }
        }

        // Yield any remaining files
        if (batch.Count > 0)
        {
            yield return batch.ToArray();
        }
    }

    private async Task ScanDirectoriesAsync(
        ChannelReader<string> directoryReader,
        ChannelWriter<string> directoryWriter,
        ChannelWriter<FileInfo> fileWriter,
        string searchPattern,
        ScanState scanState,
        CancellationToken cancellationToken)
    {
        scanState.IncrementScanner();

        try
        {
            while (await directoryReader.WaitToReadAsync(cancellationToken))
            {
                while (directoryReader.TryRead(out string? directory))
                {
                    if (directory == null) continue;

                    try
                    {
                        // Process files in current directory
                        foreach (FileInfo file in fileSystem.EnumerateFiles(directory, searchPattern, false))
                        {
                            await fileWriter.WriteAsync(file, cancellationToken);
                        }

                        // Queue subdirectories
                        List<DirectoryInfo> subdirs = fileSystem.EnumerateDirectories(directory).ToList();
                        if (subdirs.Count > 0)
                        {
                            // Increment active directories before writing
                            scanState.IncrementDirectories(subdirs.Count);

                            foreach (DirectoryInfo subDir in subdirs)
                            {
                                await directoryWriter.WriteAsync(subDir.FullName, cancellationToken);
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip directories we can't access
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // Skip directories that no longer exist
                    }
                    finally
                    {
                        // Mark this directory as processed
                        if (scanState.DecrementDirectory())
                        {
                            // No more directories to process
                            scanState.ScanningComplete.TrySetResult();
                        }
                    }
                }
            }
        }
        finally
        {
            scanState.DecrementScanner();
        }
    }
}