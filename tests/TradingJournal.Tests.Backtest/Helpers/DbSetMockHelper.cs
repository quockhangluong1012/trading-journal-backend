using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;

namespace TradingJournal.Tests.Backtest.Helpers;

public static class DbSetMockHelper
{
    public static Mock<DbSet<T>> CreateMockDbSet<T>(IQueryable<T> elements) where T : class
    {
        return elements.ToList().BuildMockDbSet();
    }
}