using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Shared.Infrastructure;

/// <summary>
/// Local disk implementation of IFileStorageService.
/// Stores files in wwwroot/{folder}/ and returns absolute URLs.
/// Suitable for development; replace with cloud storage (Azure Blob, S3) in production.
/// </summary>
internal sealed class LocalFileStorageService(
    IWebHostEnvironment env,
    IHttpContextAccessor httpContextAccessor) : IFileStorageService
{
    private const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public Task<string> SaveFileAsync(byte[] fileBytes, string fileName, string folder, CancellationToken cancellationToken = default)
    {
        if (fileBytes.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"File exceeds maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        string directoryPath = Path.Combine(env.ContentRootPath, "wwwroot", folder);

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string filePath = Path.Combine(directoryPath, fileName);
        File.WriteAllBytes(filePath, fileBytes);

        HttpContext? httpContext = httpContextAccessor.HttpContext;
        string url = httpContext is not null
            ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/{folder}/{fileName}"
            : $"/{folder}/{fileName}";

        return Task.FromResult(url);
    }

    public Task DeleteFileAsync(string fileUrl, string folder, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(fileUrl))
            return Task.CompletedTask;

        // Extract filename from URL patterns:
        // - "/screenshots/abc.png"
        // - "https://host/screenshots/abc.png"
        string[] parts = fileUrl.Split($"/{folder}/");
        if (parts.Length < 2)
            return Task.CompletedTask;

        string fileName = parts[^1];
        if (string.IsNullOrEmpty(fileName))
            return Task.CompletedTask;

        string filePath = Path.Combine(env.ContentRootPath, "wwwroot", folder, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }
}
