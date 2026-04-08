namespace TradingJournal.Modules.Auth.Infrastructure;

internal sealed class AuthDbContext : DbContext, IAuthDbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Staff> Staffs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(builder =>
        {
            builder.HasIndex(u => u.Email).IsUnique();
            builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
            builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            builder.Property(u => u.FullName).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<Staff>(builder =>
        {
            builder.HasIndex(s => s.Email).IsUnique();
            builder.Property(s => s.Email).HasMaxLength(256).IsRequired();
            builder.Property(s => s.PasswordHash).HasMaxLength(256).IsRequired();
            builder.Property(s => s.FullName).HasMaxLength(256).IsRequired();
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<EntityBase<int>>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedDate = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedDate = DateTime.UtcNow;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
