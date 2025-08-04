namespace ApiLens.Core.Helpers;

/// <summary>
/// Interface for computing file hashes for change detection.
/// </summary>
public interface IFileHashHelper
{
    /// <summary>
    /// Computes a SHA256 hash of the file content.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>The hex-encoded SHA256 hash of the file content.</returns>
    Task<string> ComputeFileHashAsync(string filePath);

    /// <summary>
    /// Computes a SHA256 hash of the file content synchronously.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>The hex-encoded SHA256 hash of the file content.</returns>
    string ComputeFileHash(string filePath);
}