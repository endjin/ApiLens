using System.Security.Cryptography;
using ApiLens.Core.Services;

namespace ApiLens.Core.Helpers;

/// <summary>
/// Helper methods for computing file hashes for change detection.
/// </summary>
public class FileHashHelper : IFileHashHelper
{
    private readonly IFileSystemService fileSystem;

    public FileHashHelper(IFileSystemService fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        this.fileSystem = fileSystem;
    }

    /// <summary>
    /// Computes a SHA256 hash of the file content.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>The hex-encoded SHA256 hash of the file content.</returns>
    public async Task<string> ComputeFileHashAsync(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        using Stream stream = await fileSystem.OpenReadAsync(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream);

        // Convert to hex string (first 16 bytes for shorter identifier)
        return Convert.ToHexString(hash[..16]).ToLowerInvariant();
    }

    /// <summary>
    /// Computes a SHA256 hash of the file content synchronously.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>The hex-encoded SHA256 hash of the file content.</returns>
    public string ComputeFileHash(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        using Stream stream = fileSystem.OpenRead(filePath);
        byte[] hash = SHA256.HashData(stream);

        // Convert to hex string (first 16 bytes for shorter identifier)
        return Convert.ToHexString(hash[..16]).ToLowerInvariant();
    }
}