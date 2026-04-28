using Microsoft.EntityFrameworkCore;
using Moq;
using MockQueryable.Moq;

namespace TradingJournal.Tests.Scanner.Helpers;

public static class DbSetMockHelper
{
    public static Mock<DbSet<T>> CreateMockDbSet<T>(IQueryable<T> elements) where T : class
    {
        return elements.ToList().BuildMockDbSet();
    }

    public static Mock<DbSet<T>> CreateMockDbSet<T>(List<T> elements) where T : class
    {
        return elements.BuildMockDbSet();
    }
}
