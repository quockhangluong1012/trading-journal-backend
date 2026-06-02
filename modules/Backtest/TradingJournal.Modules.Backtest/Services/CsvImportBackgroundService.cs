using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Backtest.Hubs;

namespace TradingJournal.Modules.Backtest.Services;

/// <summary>
/// Background service that processes queued CSV import jobs one at a time.
///
/// Flow:
///   1. Polls every 5 seconds for CsvImportJob with Status == Pending
///   2. Picks the oldest one, sets Status = Processing
///   3. Reads the stored CSV file, parses it, and bulk-inserts candles
///   4. Updates job status to Completed/Failed with counts
///   5. Deletes the temp file after successful processing
///   6. When all jobs for an asset are done, marks asset as Ready
/// </summary>
internal sealed class CsvImportBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<CsvImportBackgroundService> logger,
    IHubContext<BacktestHub> hubContext) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CsvImportBackgroundService started.");

        try
        {
            await RecoverInterruptedJobs(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("CsvImportBackgroundService stopped during startup recovery.");
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to recover interrupted CSV import jobs.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextJob(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in CsvImportBackgroundService loop.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        logger.LogInformation("CsvImportBackgroundService stopped.");
    }

    private async Task RecoverInterruptedJobs(CancellationToken ct)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IBacktestDbContext db = scope.ServiceProvider.GetRequiredService<IBacktestDbContext>();

        List<CsvImportJob> interruptedJobs = await db.CsvImportJobs
            .Include(j => j.Asset)
            .Where(j => j.Status == CsvImportStatus.Processing)
            .OrderBy(j => j.CreatedDate)
            .ToListAsync(ct);

        if (interruptedJobs.Count == 0) return;

        logger.LogWarning(
            "Found {Count} interrupted CSV import job(s) still marked as Processing. Recovering them.",
            interruptedJobs.Count);

        foreach (CsvImportJob job in interruptedJobs)
        {
            if (File.Exists(job.StoredFilePath))
            {
                job.Status = CsvImportStatus.Pending;
                job.ErrorMessage = "Previous import attempt was interrupted and will be retried.";
                job.ProcessedDate = null;

                logger.LogInformation(
                    "Reset interrupted CSV import job #{JobId} to Pending: {FileName}",
                    job.Id, job.FileName);
            }
            else
            {
                job.Status = CsvImportStatus.Failed;
                job.ErrorMessage = $"Stored CSV file not found: {job.StoredFilePath}";
                job.ProcessedDate = DateTime.UtcNow;

                logger.LogWarning(
                    "Marked interrupted CSV import job #{JobId} as Failed because the stored file is missing: {Path}",
                    job.Id, job.StoredFilePath);
            }
        }

        await db.SaveChangesAsync(ct);

        foreach (BacktestAsset asset in interruptedJobs
            .Select(j => j.Asset)
            .Where(a => a is not null)
            .DistinctBy(a => a!.Id)
            .Cast<BacktestAsset>())
        {
            await TryMarkAssetReady(db, asset, ct);
        }
    }

    private async Task ProcessNextJob(CancellationToken ct)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IBacktestDbContext db = scope.ServiceProvider.GetRequiredService<IBacktestDbContext>();

        // Pick the oldest pending job
        CsvImportJob? job = await db.CsvImportJobs
            .Include(j => j.Asset)
            .Where(j => j.Status == CsvImportStatus.Pending)
            .OrderBy(j => j.CreatedDate)
            .FirstOrDefaultAsync(ct);

        if (job is null) return;

        BacktestAsset? asset = job.Asset;
        if (asset is null)
        {
            logger.LogWarning("Import job #{JobId} references missing asset {AssetId}. Marking as failed.",
                job.Id, job.AssetId);
            job.Status = CsvImportStatus.Failed;
            job.ErrorMessage = "Asset not found.";
            job.ProcessedDate = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        logger.LogInformation(
            "Processing CSV import job #{JobId}: {FileName} for {Symbol}",
            job.Id, job.FileName, asset.Symbol);

        // Mark as processing
        job.Status = CsvImportStatus.Processing;
        await db.SaveChangesAsync(ct);

        try
        {
            // Verify file exists
            if (!File.Exists(job.StoredFilePath))
            {
                throw new FileNotFoundException($"Stored CSV file not found: {job.StoredFilePath}");
            }

            // Parse and import
            await using FileStream fileStream = new(job.StoredFilePath, FileMode.Open, FileAccess.Read);
            (int imported, int skipped) = await ParseAndImportCsv(db, asset, job, fileStream, ct);

            // Update job status
            job.Status = CsvImportStatus.Completed;
            job.ImportedCandles = imported;
            job.SkippedDuplicates = skipped;
            job.ProcessedDate = DateTime.UtcNow;

            // Update asset candle count
            asset.TotalCandles = await db.OhlcvCandles
                .Where(c => c.Asset == asset.Symbol && c.Timeframe == Timeframe.M1)
                .LongCountAsync(ct);

            await db.SaveChangesAsync(ct);

            // Delete temp file
            try { File.Delete(job.StoredFilePath); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete temp file: {Path}", job.StoredFilePath);
            }

            logger.LogInformation(
                "Completed import job #{JobId}: {Imported} imported, {Skipped} skipped for {Symbol}",
                job.Id, imported, skipped, asset.Symbol);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to process import job #{JobId}: {Error}", job.Id, ex.Message);

            job.Status = CsvImportStatus.Failed;
            job.ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            job.ProcessedDate = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        // Check if all jobs for this asset are now done
        await TryMarkAssetReady(db, asset, ct);
    }

    /// <summary>
    /// If no more Pending/Processing jobs remain for this asset, mark it as Ready.
    /// </summary>
    private async Task TryMarkAssetReady(IBacktestDbContext db, BacktestAsset asset, CancellationToken ct)
    {
        bool hasRemainingJobs = await db.CsvImportJobs
            .AnyAsync(j => j.AssetId == asset.Id
                           && (j.Status == CsvImportStatus.Pending || j.Status == CsvImportStatus.Processing), ct);

        if (!hasRemainingJobs && asset.SyncStatus == AssetSyncStatus.Syncing)
        {
            asset.SyncStatus = AssetSyncStatus.Ready;
            asset.LastError = null;

            // Update last synced date to the max timestamp in candles
            DateTime? maxTimestamp = await db.OhlcvCandles
                .Where(c => c.Asset == asset.Symbol && c.Timeframe == Timeframe.M1)
                .MaxAsync(c => (DateTime?)c.Timestamp, ct);

            if (maxTimestamp.HasValue)
                asset.LastSyncedDate = maxTimestamp.Value;

            await db.SaveChangesAsync(ct);

            logger.LogInformation("All import jobs completed for {Symbol}. Asset marked as Ready.", asset.Symbol);
        }
    }

    /// <summary>
    /// Parses a CSV file and bulk-inserts candles. Returns (imported, skipped) counts.
    /// Supports HistData semicolon format and standard comma-delimited format.
    /// </summary>
    private async Task<(int imported, int skipped)> ParseAndImportCsv(
        IBacktestDbContext db, BacktestAsset asset, CsvImportJob job, Stream fileStream, CancellationToken ct)
    {
        List<OhlcvCandle> parsedCandles = [];
        int lineNumber = 0;
        int parseErrors = 0;

        using StreamReader reader = new(fileStream);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Skip header lines
            if (lineNumber == 1 && (line.Contains("DateTime", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Date", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Time", StringComparison.OrdinalIgnoreCase)))
                continue;

            OhlcvCandle? candle = CsvCandleParser.ParseLine(line, asset.Symbol);
            if (candle != null)
            {
                parsedCandles.Add(candle);
            }
            else
            {
                parseErrors++;
                if (parseErrors <= 5)
                    logger.LogWarning("Failed to parse line {LineNumber}: {Line}", lineNumber, line);
            }
        }

        if (parsedCandles.Count == 0)
        {
            throw new InvalidOperationException(
                $"No valid candles found in the file. {parseErrors} parse errors.");
        }

        logger.LogInformation("Parsed {Count} candles from CSV, {Errors} parse errors",
            parsedCandles.Count, parseErrors);

        // Get existing timestamps to avoid duplicates
        DateTime minTime = parsedCandles.Min(c => c.Timestamp);
        DateTime maxTime = parsedCandles.Max(c => c.Timestamp);

        HashSet<DateTime> existingTimestamps = (await db.OhlcvCandles
            .Where(c => c.Asset == asset.Symbol && c.Timeframe == Timeframe.M1
                        && c.Timestamp >= minTime && c.Timestamp <= maxTime)
            .Select(c => c.Timestamp)
            .ToListAsync(ct))
            .ToHashSet();

        List<OhlcvCandle> newCandles = CsvCandleDeduplicator.KeepOnlyNewCandles(
            parsedCandles,
            existingTimestamps);

        int skipped = parsedCandles.Count - newCandles.Count;

        // Bulk insert in chunks
        const int chunkSize = 10000;
        int imported = 0;

        for (int i = 0; i < newCandles.Count; i += chunkSize)
        {
            ct.ThrowIfCancellationRequested();

            List<OhlcvCandle> chunk = newCandles.Skip(i).Take(chunkSize).ToList();
            await db.OhlcvCandles.AddRangeAsync(chunk, ct);
            await db.SaveChangesAsync(ct);
            imported += chunk.Count;

            // Update stats in real-time per chunk
            job.ImportedCandles = imported;
            
            asset.TotalCandles = await db.OhlcvCandles
                .Where(c => c.Asset == asset.Symbol && c.Timeframe == Timeframe.M1)
                .LongCountAsync(ct);
                
            await db.SaveChangesAsync(ct);

            logger.LogDebug("Imported chunk: {Imported}/{Total}", imported, newCandles.Count);
            
            // Broadcast progress to any UI listening (e.g. for toast or Asset list updates)
            await hubContext.Clients.All.SendAsync("DataProgress", new { 
                Asset = asset.Symbol,
                TotalCandles = asset.TotalCandles,
                ImportedCandles = imported,
                TotalExpected = newCandles.Count
            }, ct);
        }

        // Update last synced date
        if (maxTime > (asset.LastSyncedDate ?? DateTime.MinValue))
            asset.LastSyncedDate = maxTime;

        return (imported, skipped);
    }
}
