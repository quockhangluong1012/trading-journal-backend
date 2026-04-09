using Microsoft.Extensions.Logging;

namespace TradingJournal.Modules.Backtest.Features.V1.Admin;

/// <summary>
/// Admin endpoint for uploading multiple CSV files at once for an asset.
/// Files are saved to disk and queued as CsvImportJob records.
/// The CsvImportBackgroundService processes them sequentially.
/// </summary>
public static class BulkUploadCsvFiles
{
    public sealed record Response(int QueuedFiles, List<int> JobIds);

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost(AdminApiGroup.V1.BacktestAdmin + "/{assetId:int}/bulk-import", async (
                int assetId,
                HttpRequest request,
                IBacktestDbContext context,
                ILogger<Endpoint> logger,
                CancellationToken cancellationToken) =>
            {
                // Validate asset exists
                BacktestAsset? asset = await context.BacktestAssets
                    .FindAsync([assetId], cancellationToken);

                if (asset is null)
                    return Results.NotFound(Result<Response>.Failure(
                        new Error("Asset.NotFound", "Asset not found.")));

                // Read files from multipart form
                IFormFileCollection files = request.Form.Files;

                if (files.Count == 0)
                    return Results.BadRequest(Result<Response>.Failure(
                        new Error("Import.NoFiles", "No files were uploaded.")));

                // Validate all files before saving any
                foreach (IFormFile file in files)
                {
                    if (file.Length == 0)
                        return Results.BadRequest(Result<Response>.Failure(
                            new Error("Import.EmptyFile", $"File '{file.FileName}' is empty.")));

                    if (file.Length > 500_000_000) // 500MB per file
                        return Results.BadRequest(Result<Response>.Failure(
                            new Error("Import.FileTooLarge", $"File '{file.FileName}' exceeds 500MB limit.")));

                    string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (ext is not ".csv" and not ".txt")
                        return Results.BadRequest(Result<Response>.Failure(
                            new Error("Import.InvalidFormat", $"File '{file.FileName}' must be .csv or .txt.")));
                }

                // Create storage directory
                string storageDir = Path.Combine(
                    AppContext.BaseDirectory, "csv-imports", assetId.ToString());
                Directory.CreateDirectory(storageDir);

                List<int> jobIds = [];

                foreach (IFormFile file in files)
                {
                    // Generate unique file path
                    string storedFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
                    string storedFilePath = Path.Combine(storageDir, storedFileName);

                    // Save file to disk
                    await using (FileStream fs = new(storedFilePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fs, cancellationToken);
                    }

                    // Create import job record
                    CsvImportJob job = new()
                    {
                        Id = 0,
                        AssetId = assetId,
                        FileName = file.FileName,
                        StoredFilePath = storedFilePath,
                        Status = CsvImportStatus.Pending,
                        ImportedCandles = 0,
                        SkippedDuplicates = 0
                    };

                    context.CsvImportJobs.Add(job);
                    await context.SaveChangesAsync(cancellationToken);
                    jobIds.Add(job.Id);

                    logger.LogInformation(
                        "Queued CSV import job #{JobId} for {Symbol}: {FileName} ({Size} bytes)",
                        job.Id, asset.Symbol, file.FileName, file.Length);
                }

                // Mark asset as syncing so the UI reflects the pending work
                if (asset.SyncStatus != AssetSyncStatus.Syncing)
                {
                    asset.SyncStatus = AssetSyncStatus.Syncing;
                    await context.SaveChangesAsync(cancellationToken);
                }

                return Results.Ok(Result<Response>.Success(
                    new Response(jobIds.Count, jobIds)));
            })
            .WithTags(Tags.BacktestAdmin)
            .WithDescription("Upload multiple CSV files for bulk import. Files are queued and processed by a background service.")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<Result<Response>>()
            .Produces<Result<Response>>(StatusCodes.Status400BadRequest)
            .RequireAuthorization("AdminOnly")
            .DisableAntiforgery();
        }
    }
}
