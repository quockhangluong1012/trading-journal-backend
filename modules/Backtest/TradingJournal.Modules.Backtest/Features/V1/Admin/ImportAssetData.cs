using System.Globalization;
using Microsoft.Extensions.Logging;

namespace TradingJournal.Modules.Backtest.Features.V1.Admin;

/// <summary>
/// Admin endpoint for bulk importing CSV historical data files into the database.
///
/// Supports two CSV formats:
///
/// 1. HistData.com M1 format (Generic ASCII):
///    DateTime;Open;High;Low;Close;Volume
///    20150101 000000;1.21010;1.21020;1.21010;1.21020;0
///
/// 2. Standard CSV format:
///    DateTime,Open,High,Low,Close,Volume
///    2015-01-01 00:00:00,1.21010,1.21020,1.21010,1.21020,0
///
/// Flow:
///   1. Admin uploads the file + specifies the asset ID
///   2. File is parsed, validated, and inserted in bulk chunks
///   3. Asset candle count and sync status are updated
/// </summary>
public static class ImportAssetData
{
    public sealed record Response(int ImportedCandles, int SkippedDuplicates, string Message);

    internal sealed class ImportHandler(
        IBacktestDbContext context,
        ILogger<ImportHandler> logger)
    {
        public async Task<Result<Response>> HandleAsync(
            int assetId,
            Stream fileStream,
            CancellationToken cancellationToken)
        {
            BacktestAsset? asset = await context.BacktestAssets
                .FindAsync([assetId], cancellationToken);

            if (asset is null)
                return Result<Response>.Failure(new Error("Asset.NotFound", "Asset not found."));

            logger.LogInformation("Starting CSV import for {Symbol} (Asset ID: {Id})", asset.Symbol, assetId);

            // Read and parse CSV
            List<OhlcvCandle> parsedCandles = [];
            int lineNumber = 0;
            int parseErrors = 0;

            using StreamReader reader = new(fileStream);
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Skip header lines
                if (lineNumber == 1 && (line.Contains("DateTime", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Date", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Time", StringComparison.OrdinalIgnoreCase)))
                    continue;

                OhlcvCandle? candle = ParseLine(line, asset.Symbol);
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
                return Result<Response>.Failure(new Error("Import.Empty",
                    $"No valid candles found in the file. {parseErrors} parse errors."));

            logger.LogInformation("Parsed {Count} candles from CSV, {Errors} parse errors",
                parsedCandles.Count, parseErrors);

            // Get existing timestamps to avoid duplicates
            DateTime minTime = parsedCandles.Min(c => c.Timestamp);
            DateTime maxTime = parsedCandles.Max(c => c.Timestamp);

            HashSet<DateTime> existingTimestamps = (await context.OhlcvCandles
                .Where(c => c.Asset == asset.Symbol && c.Timeframe == Timeframe.M1
                            && c.Timestamp >= minTime && c.Timestamp <= maxTime)
                .Select(c => c.Timestamp)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            List<OhlcvCandle> newCandles = parsedCandles
                .Where(c => !existingTimestamps.Contains(c.Timestamp))
                .ToList();

            int skipped = parsedCandles.Count - newCandles.Count;

            // Bulk insert in chunks
            const int chunkSize = 10000;
            int imported = 0;

            for (int i = 0; i < newCandles.Count; i += chunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<OhlcvCandle> chunk = newCandles.Skip(i).Take(chunkSize).ToList();
                await context.OhlcvCandles.AddRangeAsync(chunk, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                imported += chunk.Count;

                logger.LogDebug("Imported chunk: {Imported}/{Total}", imported, newCandles.Count);
            }

            // Update asset status
            asset.TotalCandles = await context.OhlcvCandles
                .Where(c => c.Asset == asset.Symbol && c.Timeframe == Timeframe.M1)
                .LongCountAsync(cancellationToken);

            asset.LastSyncedDate = maxTime;

            // If CSV import and all data is loaded, mark as Ready
            if (asset.DataProvider == "CSV")
                asset.SyncStatus = AssetSyncStatus.Ready;

            await context.SaveChangesAsync(cancellationToken);

            string message = $"Imported {imported} candles for {asset.Symbol}. " +
                             $"Skipped {skipped} duplicates. Range: {minTime:yyyy-MM-dd} to {maxTime:yyyy-MM-dd}.";

            logger.LogInformation(message);

            return Result<Response>.Success(new Response(imported, skipped, message));
        }

        /// <summary>
        /// Parses a single CSV line. Supports:
        /// - HistData semicolon format: 20150101 000000;1.21010;1.21020;1.21010;1.21020;0
        /// - Standard comma format: 2015-01-01 00:00:00,1.21010,1.21020,1.21010,1.21020,0
        /// - Standard comma format: 2015-01-01,1.21010,1.21020,1.21010,1.21020,0
        /// </summary>
        private static OhlcvCandle? ParseLine(string line, string symbol)
        {
            try
            {
                string[] parts;
                DateTime timestamp;

                if (line.Contains(';'))
                {
                    // HistData format: 20150101 000000;1.21010;1.21020;1.21010;1.21020;0
                    parts = line.Split(';');
                    if (parts.Length < 5) return null;

                    string dateStr = parts[0].Trim();
                    if (dateStr.Length == 15) // "20150101 000000"
                    {
                        timestamp = DateTime.ParseExact(dateStr, "yyyyMMdd HHmmss",
                            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                            .ToUniversalTime();
                    }
                    else
                    {
                        timestamp = DateTime.Parse(dateStr, CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal).ToUniversalTime();
                    }
                }
                else
                {
                    // Standard CSV: 2015-01-01 00:00:00,1.21010,1.21020,1.21010,1.21020,0
                    parts = line.Split(',');
                    if (parts.Length < 5) return null;

                    timestamp = DateTime.Parse(parts[0].Trim(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal).ToUniversalTime();
                }

                decimal open = decimal.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                decimal high = decimal.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
                decimal low = decimal.Parse(parts[3].Trim(), CultureInfo.InvariantCulture);
                decimal close = decimal.Parse(parts[4].Trim(), CultureInfo.InvariantCulture);
                decimal volume = parts.Length > 5
                    ? decimal.TryParse(parts[5].Trim(), CultureInfo.InvariantCulture, out decimal v) ? v : 0m
                    : 0m;

                return new OhlcvCandle
                {
                    Id = 0,
                    Asset = symbol,
                    Timeframe = Timeframe.M1,
                    Timestamp = timestamp,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume
                };
            }
            catch
            {
                return null;
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost(AdminApiGroup.V1.BacktestAdmin + "/{assetId:int}/import", async (
                int assetId,
                IFormFile file,
                IBacktestDbContext context,
                ILogger<ImportHandler> logger,
                CancellationToken cancellationToken) =>
            {
                if (file.Length == 0)
                    return Results.BadRequest("File is empty.");

                if (file.Length > 500_000_000) // 500MB limit
                    return Results.BadRequest("File exceeds 500MB limit.");

                ImportHandler handler = new(context, logger);
                using Stream stream = file.OpenReadStream();
                Result<Response> result = await handler.HandleAsync(assetId, stream, cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .WithTags(Tags.BacktestAdmin)
            .WithDescription("Import historical M1 candle data from a CSV file (HistData.com or standard format).")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<Result<Response>>()
            .Produces<Result<Response>>(StatusCodes.Status400BadRequest)
            .RequireAuthorization("AdminOnly")
            .DisableAntiforgery();
        }
    }
}
