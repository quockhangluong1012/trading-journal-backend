namespace TradingJournal.Tests.Notifications.Helpers;

/// <summary>
/// Builds Moq-backed <see cref="DbSet{T}"/> instances from in-memory lists so handlers that
/// query <c>INotificationDbContext</c> can be unit-tested without a real database.
/// </summary>
public static class DbSetMockHelper
{
    public static Mock<DbSet<T>> CreateMockDbSet<T>(IEnumerable<T> elements) where T : class
    {
        return elements.ToList().BuildMockDbSet();
    }
}
