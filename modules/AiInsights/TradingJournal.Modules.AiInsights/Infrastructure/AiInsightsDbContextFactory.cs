using Microsoft.EntityFrameworkCore.Design;

namespace TradingJournal.Modules.AiInsights.Infrastructure;

internal sealed class AiInsightsDbContextFactory : IDesignTimeDbContextFactory<AiInsightsDbContext>
{
    private const string FallbackConnectionString = "Server=(localdb)\\mssqllocaldb;Database=TradingJournalAiInsightsDesignTime;Trusted_Connection=True;TrustServerCertificate=True";

    public AiInsightsDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__TradeDatabase")
            ?? Environment.GetEnvironmentVariable("TradeDatabaseConnectionString")
            ?? FallbackConnectionString;

        DbContextOptionsBuilder<AiInsightsDbContext> optionsBuilder = new();

        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
            sqlOptions.MigrationsHistoryTable("__AiInsightsMigrationsHistory", "Trades");
        });

        return new AiInsightsDbContext(optionsBuilder.Options, new HttpContextAccessor());
    }
}