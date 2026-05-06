using Moq;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Features.V1.Search;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class NaturalLanguageTradeSearchValidatorTests
{
    private readonly SearchTradesNaturalLanguage.Validator _validator = new();

    [Fact]
    public void Validate_EmptyQuery_ReturnsInvalid()
    {
        var result = _validator.Validate(new SearchTradesNaturalLanguage.Request(string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("query", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class NaturalLanguageTradeSearchHandlerTests
{
    private readonly Mock<IOpenRouterAIService> _aiService = new();

    [Fact]
    public async Task Handle_WhenAiReturnsStructuredFilters_ReturnsSuccess()
    {
        NaturalLanguageTradeSearchResultDto expected = new(
            Asset: "EURUSD",
            Position: "Long",
            Status: "Closed",
            FromDate: DateTime.UtcNow.AddDays(-7),
            ToDate: DateTime.UtcNow,
            Interpretation: "Closed EURUSD long trades from the last week.");

        _aiService
            .Setup(service => service.SearchTradesNaturalLanguageAsync(
                It.Is<NaturalLanguageTradeSearchRequestDto>(dto => dto.Query == "show my closed EURUSD longs from last week" && dto.UserId == 7),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new SearchTradesNaturalLanguage.Handler(_aiService.Object);

        var result = await handler.Handle(
            new SearchTradesNaturalLanguage.Request("show my closed EURUSD longs from last week", 7),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task Handle_WhenAiReturnsNull_ReturnsFailure()
    {
        _aiService
            .Setup(service => service.SearchTradesNaturalLanguageAsync(It.IsAny<NaturalLanguageTradeSearchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NaturalLanguageTradeSearchResultDto?)null);

        var handler = new SearchTradesNaturalLanguage.Handler(_aiService.Object);

        var result = await handler.Handle(
            new SearchTradesNaturalLanguage.Request("find my losers this month", 11),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }
}