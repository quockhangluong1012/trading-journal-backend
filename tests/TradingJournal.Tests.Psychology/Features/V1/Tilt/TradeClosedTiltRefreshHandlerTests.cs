using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.Psychology.Common.Enum;
using TradingJournal.Modules.Psychology.EventHandlers;
using TradingJournal.Modules.Psychology.Domain;
using TradingJournal.Modules.Psychology.Services;

namespace TradingJournal.Tests.Psychology.Features.V1.Tilt;

public sealed class TradeClosedTiltRefreshHandlerTests
{
    [Fact]
    public async Task Handle_RecalculatesTiltForTradeOwner()
    {
        var tiltService = new Mock<ITiltDetectionService>();
        tiltService
            .Setup(service => service.RecalculateTiltAsync(17, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TiltSnapshot { Score = 55, Level = TiltLevel.Warning, RecordedAt = DateTime.UtcNow });

        var handler = new TradeClosedTiltRefreshHandler(tiltService.Object, NullLogger<TradeClosedTiltRefreshHandler>.Instance);

        await handler.Handle(new TradeClosedEvent(Guid.NewGuid(), 17, 41, DateTime.UtcNow, -75m), CancellationToken.None);

        tiltService.Verify(service => service.RecalculateTiltAsync(17, It.IsAny<CancellationToken>()), Times.Once);
    }
}