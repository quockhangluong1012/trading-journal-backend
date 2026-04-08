namespace TradingJournal.Modules.Auth.Infrastructure;

public interface IAuthDbContext
{
    DbSet<User> Users { get; set; }
    DbSet<Staff> Staffs { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
