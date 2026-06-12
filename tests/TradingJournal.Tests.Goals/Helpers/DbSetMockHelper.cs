namespace TradingJournal.Tests.Goals.Helpers;

public static class DbSetMockHelper
{
    public static Mock<DbSet<T>> CreateMockDbSet<T>(IEnumerable<T> elements) where T : class
    {
        return elements.ToList().BuildMockDbSet();
    }
}
