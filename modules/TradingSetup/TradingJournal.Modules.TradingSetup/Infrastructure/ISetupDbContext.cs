namespace TradingJournal.Modules.Setups.Infrastructure;

public interface ISetupDbContext
{
    DbSet<TradingSetup> TradingSetups { get; set; }

    DbSet<SetupStep> SetupSteps { get; set; }

    DbSet<SetupConnection> SetupConnections { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
