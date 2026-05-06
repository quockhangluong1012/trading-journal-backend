using Moq;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Features.V1.Lessons;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class SuggestLessonsValidatorTests
{
    private readonly SuggestLessons.Validator _validator = new();

    [Fact]
    public void Validate_ToDateBeforeFromDate_ReturnsInvalid()
    {
        DateTime fromDate = new(2026, 5, 1);
        DateTime toDate = new(2026, 4, 30);

        var result = _validator.Validate(new SuggestLessons.Request(fromDate, toDate));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("to date", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class SuggestLessonsHandlerTests
{
    private readonly Mock<IOpenRouterAIService> _aiService = new();

    [Fact]
    public async Task Handle_WhenAiReturnsSuggestions_ReturnsSuccess()
    {
        DateTime fromDate = new(2026, 4, 1);
        DateTime toDate = new(2026, 4, 30);

        SuggestedLessonsResultDto expected = new()
        {
            Summary = "Recurring execution errors now cluster around late-session continuation entries.",
            SampleSize = 14,
            Suggestions =
            [
                new SuggestedLessonDto
                {
                    Title = "Stop chasing late-session continuation",
                    Content = "Several losing trades were entered after the primary move was already extended.",
                    Category = 1,
                    Severity = 1,
                    KeyTakeaway = "Only take continuation trades before momentum is exhausted.",
                    ActionItems = "Wait for a fresh liquidity sweep or stand down after the main session impulse.",
                    ImpactScore = 7,
                    LinkedTradeIds = [11, 13, 17]
                }
            ]
        };

        _aiService
            .Setup(service => service.SuggestLessonsAsync(
                It.Is<SuggestLessonsRequestDto>(dto => dto.UserId == 19 && dto.FromDate == fromDate && dto.ToDate == toDate),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new SuggestLessons.Handler(_aiService.Object);

        var result = await handler.Handle(new SuggestLessons.Request(fromDate, toDate, 19), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task Handle_WhenAiReturnsNull_ReturnsFailure()
    {
        _aiService
            .Setup(service => service.SuggestLessonsAsync(It.IsAny<SuggestLessonsRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SuggestedLessonsResultDto?)null);

        var handler = new SuggestLessons.Handler(_aiService.Object);

        var result = await handler.Handle(new SuggestLessons.Request(null, null, 5), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }
}