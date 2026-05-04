namespace TradingJournal.Shared.Interfaces;

/// <summary>
/// Abstraction for file storage operations. Enables swapping between local disk (development)
/// and cloud storage (Azure Blob, S3) without changing consuming code.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Saves a file from raw bytes and returns the public URL.
    /// </summary>
    /// <param name="fileBytes">The file content as bytes.</param>
    /// <param name="fileName">The desired file name (e.g., "abc123.png").</param>
    /// <param name="folder">The logical folder/container (e.g., "screenshots").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The public URL of the saved file.</returns>
    Task<string> SaveFileAsync(byte[] fileBytes, string fileName, string folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file by its URL or path.
    /// </summary>
    /// <param name="fileUrl">The URL or path of the file to delete.</param>
    /// <param name="folder">The logical folder/container (e.g., "screenshots").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteFileAsync(string fileUrl, string folder, CancellationToken cancellationToken = default);
}
