using TradingJournal.Shared.Infrastructure;

namespace TradingJournal.Modules.RiskManagement.Infrastructure;

internal sealed class RiskDbContext(DbContextOptions<RiskDbContext> options, IHttpContextAccessor httpContextAccessor)
    : AuditableDbContext(options, httpContextAccessor), IRiskDbContext
{
    public DbSet<RiskConfig> RiskConfigs { get; set; } = null!;
    public DbSet<AccountBalanceEntry> AccountBalanceEntries { get; set; } = null!;
    public DbSet<DailyRiskSnapshot> DailyRiskSnapshots { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RiskConfig>(builder =>
        {
            builder.Property(r => r.DailyLossLimitPercent).HasPrecision(18, 4);
            builder.Property(r => r.WeeklyDrawdownCapPercent).HasPrecision(18, 4);
            builder.Property(r => r.RiskPerTradePercent).HasPrecision(18, 4);
            builder.Property(r => r.AccountBalance).HasPrecision(18, 2);
        });

        modelBuilder.Entity<AccountBalanceEntry>(builder =>
        {
            builder.Property(e => e.Amount).HasPrecision(18, 2);
            builder.Property(e => e.BalanceAfter).HasPrecision(18, 2);
        });

        modelBuilder.Entity<DailyRiskSnapshot>(builder =>
        {
            builder.Property(s => s.DailyPnl).HasPrecision(18, 2);
            builder.Property(s => s.WeeklyPnl).HasPrecision(18, 2);
            builder.Property(s => s.AccountBalanceEod).HasPrecision(18, 2);
            builder.Property(s => s.MaxDrawdownPercent).HasPrecision(18, 4);
        });
    }
}
