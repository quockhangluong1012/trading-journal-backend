using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Psychology.Common.Enum;
using TradingJournal.Modules.Psychology.Domain;
using TradingJournal.Modules.Psychology.Infrastructure.Persistance;
using TradingJournal.Modules.Psychology.Services;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Psychology.Features.V1.Karma;

public sealed class KarmaServiceProgressionTests
{
    private const int UserId = 42;

    [Theory]
    [InlineData(99, 1, "Novice Trader", 1, 100)]
    [InlineData(100, 2, "Apprentice", 200, 300)]
    [InlineData(299, 2, "Apprentice", 1, 300)]
    [InlineData(300, 3, "Journeyman", 350, 650)]
    [InlineData(85000, 19, "Transcendent", 5000, 90000)]
    [InlineData(250000, 25, "Trading God", 0, 250000)]
    public async Task GetKarmaSummary_UsesHarderLevelThresholds(
        int totalKarma,
        int expectedLevel,
        string expectedTitle,
        int expectedPointsToNext,
        int expectedNextThreshold)
    {
        var service = CreateService(
            karmaRecords:
            [
                new KarmaRecord
                {
                    CreatedBy = UserId,
                    ActionType = KarmaActionType.SystemAdjustment,
                    Points = totalKarma,
                    Description = "Seeded karma",
                    RecordedAt = DateTime.UtcNow
                }
            ]);

        var summary = await service.GetKarmaSummaryAsync(UserId);

        Assert.Equal(totalKarma, summary.TotalKarma);
        Assert.Equal(expectedLevel, summary.Level);
        Assert.Equal(expectedTitle, summary.Title);
        Assert.Equal(expectedPointsToNext, summary.PointsToNextLevel);
        Assert.Equal(expectedNextThreshold, summary.NextLevelThreshold);
    }

    [Fact]
    public async Task GetAchievements_ExposesExpandedAchievementCatalog()
    {
        var service = CreateService();

        var achievements = await service.GetAchievementsAsync(UserId);
        var types = achievements.Select(achievement => achievement.Type).ToHashSet();

        Assert.True(achievements.Count >= 165);
        Assert.Contains(AchievementType.KarmaLevel24.ToString(), types);
        Assert.Contains(AchievementType.DailyNotes365.ToString(), types);
        Assert.Contains(AchievementType.RiskReward3x100.ToString(), types);
        Assert.Contains(AchievementType.WinRate80.ToString(), types);
        Assert.Contains(AchievementType.ProfitableWeek50.ToString(), types);
    }

    private static KarmaService CreateService(List<KarmaRecord>? karmaRecords = null)
    {
        var dbContext = new Mock<IPsychologyDbContext>();
        dbContext.Setup(context => context.KarmaRecords)
            .Returns((karmaRecords ?? []).BuildMockDbSet().Object);
        dbContext.Setup(context => context.Achievements)
            .Returns(new List<Achievement>().BuildMockDbSet().Object);

        var tradeProvider = new Mock<ITradeProvider>();
        tradeProvider.Setup(provider => provider.GetTradesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradeCacheDto>());

        return new KarmaService(
            dbContext.Object,
            tradeProvider.Object,
            new Mock<IEventBus>().Object,
            NullLogger<KarmaService>.Instance);
    }
}
