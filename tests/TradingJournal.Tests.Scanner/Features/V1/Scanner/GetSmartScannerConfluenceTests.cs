using Moq;
using TradingJournal.Modules.Scanner.Common.Enums;
using TradingJournal.Modules.Scanner.Domain;
using TradingJournal.Modules.Scanner.Features.V1.Scanner;
using TradingJournal.Modules.Scanner.Infrastructure;
using TradingJournal.Modules.Scanner.Services;
using TradingJournal.Modules.Scanner.Services.EconomicCalendar;
using TradingJournal.Modules.Scanner.Services.ICTAnalysis;
using TradingJournal.Modules.Scanner.Services.LiveData;
using TradingJournal.Tests.Scanner.Helpers;

namespace TradingJournal.Tests.Scanner.Features.V1.Scanner;

public sealed class GetSmartScannerConfluenceHandlerTests
{
    private readonly Mock<IScannerDbContext> _scannerDb = new();
    private readonly Mock<ILiveMarketDataProvider> _liveMarketDataProvider = new();
    private readonly Mock<IEconomicCalendarProvider> _economicCalendarProvider = new();

    [Fact]
    public async Task Handle_GroupsMultiTimeframeSignals_AndOverlaysEconomicRisk()
    {
        _scannerDb.Setup(db => db.ScannerConfigs)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<ScannerConfig>()).Object);

        List<CandleData> candles = [
            new(DateTime.UtcNow.AddMinutes(-15), 1.1m, 1.2m, 1.0m, 1.15m, 1000m),
            new(DateTime.UtcNow.AddMinutes(-10), 1.15m, 1.22m, 1.12m, 1.21m, 1200m)
        ];

        _liveMarketDataProvider
            .Setup(provider => provider.GetRecentCandlesAsync("EURUSD", It.IsAny<ScannerTimeframe>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(candles);

        var detector = new Mock<IIctDetector>();
        detector.SetupGet(d => d.PatternType).Returns(IctPatternType.FVG);
        detector.Setup(d => d.Detect(It.IsAny<IReadOnlyList<CandleData>>(), "EURUSD", ScannerTimeframe.H1))
            .Returns([new DetectedPattern(IctPatternType.FVG, ScannerTimeframe.H1, 1.21m, 1.22m, 1.18m, "H1 FVG", DateTime.UtcNow)]);
        detector.Setup(d => d.Detect(It.IsAny<IReadOnlyList<CandleData>>(), "EURUSD", ScannerTimeframe.M15))
            .Returns([new DetectedPattern(IctPatternType.FVG, ScannerTimeframe.M15, 1.205m, 1.21m, 1.19m, "M15 FVG", DateTime.UtcNow)]);
        detector.Setup(d => d.Detect(It.IsAny<IReadOnlyList<CandleData>>(), "EURUSD", ScannerTimeframe.D1))
            .Returns([]);
        detector.Setup(d => d.Detect(It.IsAny<IReadOnlyList<CandleData>>(), "EURUSD", ScannerTimeframe.M5))
            .Returns([]);

        MultiTimeframeAnalyzer analyzer = new([detector.Object]);

        _economicCalendarProvider
            .Setup(provider => provider.GetTodayEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new EconomicEvent
                {
                    Id = "USD_CPI",
                    Country = "United States",
                    Currency = "USD",
                    EventName = "CPI",
                    EventDateUtc = DateTime.UtcNow.AddMinutes(20),
                    Impact = EconomicImpact.High
                }
            ]);

        var handler = new GetSmartScannerConfluence.Handler(
            _scannerDb.Object,
            _liveMarketDataProvider.Object,
            analyzer,
            _economicCalendarProvider.Object);

        var result = await handler.Handle(new GetSmartScannerConfluence.Query("EURUSD", 4), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Red", result.Value.EconomicRiskState);
        Assert.Single(result.Value.Candidates);
        Assert.Equal(2, result.Value.Candidates[0].ConfluenceScore);
        Assert.Equal(2, result.Value.Candidates[0].ConfirmingTimeframes.Count);
    }
}