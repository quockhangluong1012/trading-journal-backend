using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Infrastructure;

namespace TradingJournal.Tests.Trades.Infrastructure;

public sealed class TradeDbContextPrecisionTests
{
    private const string SqlServerConnectionString = "Server=(localdb)\\mssqllocaldb;Database=TradePricePrecisionTests;Trusted_Connection=True;TrustServerCertificate=True;";

    [Theory]
    [InlineData(nameof(TradeHistory.EntryPrice))]
    [InlineData(nameof(TradeHistory.ExitPrice))]
    [InlineData(nameof(TradeHistory.StopLoss))]
    [InlineData(nameof(TradeHistory.TargetTier1))]
    [InlineData(nameof(TradeHistory.TargetTier2))]
    [InlineData(nameof(TradeHistory.TargetTier3))]
    public void TradeHistoryPriceProperties_UseDecimal18_5(string propertyName)
    {
        IProperty property = GetProperty(typeof(TradeHistory), propertyName);

        Assert.Equal("decimal(18,5)", property.GetColumnType());
        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(5, property.GetScale());
    }

    [Theory]
    [InlineData(nameof(TradeTemplate.DefaultStopLoss))]
    [InlineData(nameof(TradeTemplate.DefaultTargetTier1))]
    [InlineData(nameof(TradeTemplate.DefaultTargetTier2))]
    [InlineData(nameof(TradeTemplate.DefaultTargetTier3))]
    public void TradeTemplatePriceProperties_UseDecimal18_5(string propertyName)
    {
        IProperty property = GetProperty(typeof(TradeTemplate), propertyName);

        Assert.Equal("decimal(18,5)", property.GetColumnType());
        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(5, property.GetScale());
    }

    private static IProperty GetProperty(Type entityType, string propertyName)
    {
        DbContextOptions<TradeDbContext> options = new DbContextOptionsBuilder<TradeDbContext>()
            .UseSqlServer(SqlServerConnectionString)
            .Options;

        IHttpContextAccessor httpContextAccessor = new HttpContextAccessor();

        using TradeDbContext context = new(options, httpContextAccessor);

        return context.Model.FindEntityType(entityType)!.FindProperty(propertyName)!;
    }
}