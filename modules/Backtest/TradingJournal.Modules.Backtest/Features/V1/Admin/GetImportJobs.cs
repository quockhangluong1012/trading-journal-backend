namespace TradingJournal.Modules.Backtest.Features.V1.Admin;

/// <summary>
/// Admin endpoint to get the list of CSV import jobs for an asset.
/// Used by the frontend to poll and display per-file processing progress.
/// </summary>
public static class GetImportJobs
{
    public sealed record JobDto(
        int Id,
        string FileName,
        string Status,
        int ImportedCandles,
        int SkippedDuplicates,
        string? ErrorMessage,
        DateTime? ProcessedDate,
        DateTime CreatedDate);

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(AdminApiGroup.V1.BacktestAdmin + "/{assetId:int}/import-jobs", async (
                int assetId,
                IBacktestDbContext context,
                CancellationToken cancellationToken) =>
            {
                // Verify asset exists
                bool exists = await context.BacktestAssets
                    .AnyAsync(a => a.Id == assetId, cancellationToken);

                if (!exists)
                    return Results.NotFound(Result<List<JobDto>>.Failure(
                        new Error("Asset.NotFound", "Asset not found.")));

                List<JobDto> jobs = await context.CsvImportJobs
                    .Where(j => j.AssetId == assetId)
                    .OrderByDescending(j => j.CreatedDate)
                    .Select(j => new JobDto(
                        j.Id,
                        j.FileName,
                        j.Status.ToString(),
                        j.ImportedCandles,
                        j.SkippedDuplicates,
                        j.ErrorMessage,
                        j.ProcessedDate,
                        j.CreatedDate))
                    .ToListAsync(cancellationToken);

                return Results.Ok(Result<List<JobDto>>.Success(jobs));
            })
            .WithTags(Tags.BacktestAdmin)
            .WithDescription("Get all CSV import jobs for an asset with their processing status.")
            .Produces<Result<List<JobDto>>>()
            .RequireAuthorization("AdminOnly");
        }
    }
}
