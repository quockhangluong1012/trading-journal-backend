namespace TradingJournal.Modules.RiskManagement.Infrastructure;

internal interface IRiskDbContext
{
    DbSet<RiskConfig> RiskConfigs { get; set; }
    DbSet<AccountBalanceEntry> AccountBalanceEntries { get; set; }
    DbSet<DailyRiskSnapshot> DailyRiskSnapshots { get; set; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
