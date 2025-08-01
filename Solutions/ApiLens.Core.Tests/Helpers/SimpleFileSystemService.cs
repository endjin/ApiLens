using ApiLens.Core.Services;

namespace ApiLens.Core.Tests.Helpers;

/// <summary>
/// Simple file system service implementation for testing.
/// </summary>
internal class SimpleFileSystemService : IFileSystemService
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public IEnumerable<string> GetFiles(string path, string pattern, bool recursive) =>
        Directory.GetFiles(path, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    public string CombinePath(params string[] paths) => Path.Combine(paths);
    public FileInfo GetFileInfo(string path) => new(path);
    public DirectoryInfo GetDirectoryInfo(string path) => new(path);
    public string GetUserNuGetCachePath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
    public string GetFileName(string path) => Path.GetFileName(path);
    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);
    public IEnumerable<FileInfo> EnumerateFiles(string path, string? pattern = null, bool recursive = false) =>
        new DirectoryInfo(path).EnumerateFiles(pattern ?? "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    public Stream OpenRead(string path) => File.OpenRead(path);
    public Task<Stream> OpenReadAsync(string path) => Task.FromResult<Stream>(File.OpenRead(path));
}