using TradingJournal.Shared.Infrastructure;

namespace TradingJournal.Modules.Setups.Infrastructure;

internal sealed class SetupDbContext(DbContextOptions<SetupDbContext> options, IHttpContextAccessor httpContextAccessor)
    : AuditableDbContext(options, httpContextAccessor), ISetupDbContext
{
    public DbSet<TradingSetup> TradingSetups { get; set; } = null!;

    public DbSet<SetupStep> SetupSteps { get; set; } = null!;

    public DbSet<SetupConnection> SetupConnections { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TradingSetup>(builder =>
        {
            builder.ToTable("TradingSetups", "Setups");

            builder.HasMany(setup => setup.Steps)
                .WithOne(step => step.TradingSetup)
                .HasForeignKey(step => step.TradingSetupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(setup => setup.Connections)
                .WithOne(connection => connection.TradingSetup)
                .HasForeignKey(connection => connection.TradingSetupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SetupStep>(builder =>
        {
            builder.ToTable("SetupSteps", "Setups");
        });

        modelBuilder.Entity<SetupConnection>(builder =>
        {
            builder.ToTable("SetupConnections", "Setups");

            builder.HasOne(connection => connection.SourceStep)
                .WithMany()
                .HasForeignKey(connection => connection.SourceStepId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(connection => connection.TargetStep)
                .WithMany()
                .HasForeignKey(connection => connection.TargetStepId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
