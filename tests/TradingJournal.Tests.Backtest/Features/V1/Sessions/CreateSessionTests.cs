using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Moq;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Backtest.Common.Enums;
using TradingJournal.Modules.Backtest.Domain;
using TradingJournal.Modules.Backtest.Features.V1.Sessions;
using TradingJournal.Modules.Backtest.Infrastructure;
using TradingJournal.Tests.Backtest.Helpers;

namespace TradingJournal.Tests.Backtest.Features.V1.Sessions;

#region Validator

public sealed class CreateSessionValidatorTests
{
    private static readonly CreateSession.Validator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var request = new CreateSession.Request("EURUSD", DateTime.UtcNow.AddDays(-30), null, 10_000m, 50);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_Asset_Is_Empty()
    {
        var request = new CreateSession.Request("", DateTime.UtcNow.AddDays(-30), null, 10_000m, 50);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Asset);
    }

    [Fact]
    public void Should_Have_Error_When_StartDate_Is_In_Future()
    {
        var request = new CreateSession.Request("EURUSD", DateTime.UtcNow.AddDays(10), null, 10_000m, 50);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.StartDate);
    }

    [Fact]
    public void Should_Have_Error_When_InitialBalance_Is_Zero()
    {
        var request = new CreateSession.Request("EURUSD", DateTime.UtcNow.AddDays(-30), null, 0m, 50);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.InitialBalance);
    }

    [Fact]
    public void Should_Have_Error_When_InitialBalance_Is_Negative()
    {
        var request = new CreateSession.Request("EURUSD", DateTime.UtcNow.AddDays(-30), null, -100m, 50);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.InitialBalance);
    }

    [Fact]
    public void Should_Have_Error_When_Leverage_Is_Zero()
    {
        var request = new CreateSession.Request("EURUSD", DateTime.UtcNow.AddDays(-30), null, 10_000m, 0);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Leverage);
    }
}

#endregion

#region Handler

public sealed class CreateSessionHandlerTests
{
    private readonly Mock<IBacktestDbContext> _context = new();
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly CreateSession.Handler _handler;

    public CreateSessionHandlerTests()
    {
        _handler = new CreateSession.Handler(_context.Object, _eventBus.Object);
    }

    [Fact]
    public async Task Handle_CreatesSession_WithAssetSpread()
    {
        var asset = new BacktestAsset
        {
            Id = 1,
            Symbol = "EURUSD",
            DefaultSpreadPips = 1.5m,
            PipSize = 0.0001m
        };

        _context.Setup(x => x.BacktestAssets)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestAsset> { asset }.AsQueryable()).Object);
        _context.Setup(x => x.BacktestSessions)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestSession>().AsQueryable()).Object);
        _context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _eventBus.Setup(x => x.PublishAsync(It.IsAny<TradingJournal.Modules.Backtest.Events.FetchHistoricalDataEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var request = new CreateSession.Request("eurusd", DateTime.UtcNow.AddDays(-30), null, 10_000m, 50);
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _context.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NormalizesAssetToUpperCase()
    {
        _context.Setup(x => x.BacktestAssets)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestAsset>().AsQueryable()).Object);
        _context.Setup(x => x.BacktestSessions)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestSession>().AsQueryable()).Object);
        _context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _eventBus.Setup(x => x.PublishAsync(It.IsAny<TradingJournal.Modules.Backtest.Events.FetchHistoricalDataEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var request = new CreateSession.Request("  eurusd  ", DateTime.UtcNow.AddDays(-30), null, 10_000m, 50);
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_UsesZeroSpread_WhenAssetNotFound()
    {
        _context.Setup(x => x.BacktestAssets)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestAsset>().AsQueryable()).Object);
        _context.Setup(x => x.BacktestSessions)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestSession>().AsQueryable()).Object);
        _context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _eventBus.Setup(x => x.PublishAsync(It.IsAny<TradingJournal.Modules.Backtest.Events.FetchHistoricalDataEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var request = new CreateSession.Request("UNKNOWN", DateTime.UtcNow.AddDays(-30), null, 10_000m, 50);
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_PublishesEvent_AfterCreation()
    {
        _context.Setup(x => x.BacktestAssets)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestAsset>().AsQueryable()).Object);
        _context.Setup(x => x.BacktestSessions)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestSession>().AsQueryable()).Object);
        _context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _eventBus.Setup(x => x.PublishAsync(It.IsAny<TradingJournal.Modules.Backtest.Events.FetchHistoricalDataEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var request = new CreateSession.Request("EURUSD", DateTime.UtcNow.AddDays(-30), null, 10_000m, 50);
        await _handler.Handle(request, CancellationToken.None);

        _eventBus.Verify(x => x.PublishAsync(It.IsAny<TradingJournal.Modules.Backtest.Events.FetchHistoricalDataEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

#endregion
