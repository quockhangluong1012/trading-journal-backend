using TradingJournal.Shared.Infrastructure;

namespace TradingJournal.Modules.Notifications.Infrastructure;

internal sealed class NotificationDbContext(
    DbContextOptions<NotificationDbContext> options,
    IHttpContextAccessor httpContextAccessor)
    : AuditableDbContext(options, httpContextAccessor), INotificationDbContext
{
    public DbSet<Notification> Notifications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Notification>(builder =>
        {
            builder.ToTable("Notifications", "Notification");

            builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedDate })
                .HasDatabaseName("IX_Notifications_UserReadDate");

            builder.HasIndex(n => new { n.UserId, n.CreatedDate })
                .HasDatabaseName("IX_Notifications_UserDate");

            builder.Property(n => n.Title).HasMaxLength(200);
            builder.Property(n => n.Message).HasMaxLength(1000);
            builder.Property(n => n.Metadata).HasMaxLength(4000);
            builder.Property(n => n.ActionUrl).HasMaxLength(500);
        });
    }
}
