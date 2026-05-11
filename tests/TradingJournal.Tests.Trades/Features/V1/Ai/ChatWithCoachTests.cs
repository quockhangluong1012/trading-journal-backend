using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TradingJournal.Modules.AiInsights.Domain;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Features.V1.AiCoach;
using TradingJournal.Modules.AiInsights.Infrastructure;
using TradingJournal.Modules.AiInsights.Services;
using TradingJournal.Tests.Trades.Helpers;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class ChatWithCoachHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Mock<IOpenRouterAIService> _aiService = new();
    private readonly Mock<IAiInsightsDbContext> _context = new();
    private readonly Mock<DbSet<AiCoachConversation>> _conversationSet;
    private readonly NullLogger<ChatWithCoach.Handler> _logger = NullLogger<ChatWithCoach.Handler>.Instance;

    public ChatWithCoachHandlerTests()
    {
        _conversationSet = DbSetMockHelper.CreateMockDbSet(new List<AiCoachConversation>().AsQueryable());
        _context
            .Setup(x => x.AiCoachConversations)
            .Returns(_conversationSet.Object);
        _context
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    [Fact]
    public async Task Handle_WhenAiReturnsReply_PersistsCoachConversationSnapshot()
    {
        ChatWithCoach.Request request = new(
            [
                new AiCoachMessageDto(AiCoachRoles.User, "Help me review today's execution."),
                new AiCoachMessageDto(AiCoachRoles.Assistant, "Let's break it down."),
                new AiCoachMessageDto(AiCoachRoles.User, "What should I improve first?")
            ],
            UserId: 14);

        _aiService
            .Setup(service => service.ChatWithCoachAsync(
                It.Is<AiCoachRequestDto>(dto => dto.UserId == 14 && dto.Mode == AiCoachModes.Coach),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCoachResponseDto("Start by tightening your entry confirmation."));

        AiCoachConversation? savedConversation = null;
        _conversationSet
            .Setup(set => set.AddAsync(It.IsAny<AiCoachConversation>(), It.IsAny<CancellationToken>()))
            .Callback<AiCoachConversation, CancellationToken>((conversation, _) => savedConversation = conversation);

        var handler = new ChatWithCoach.Handler(_aiService.Object, _context.Object, _logger);

        var result = await handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Start by tightening your entry confirmation.", result.Value.Reply);
        Assert.NotNull(savedConversation);
        Assert.Equal(AiCoachModes.Coach, savedConversation!.Mode);
        Assert.Equal(4, savedConversation.MessageCount);
        Assert.True(TranscriptMatches(savedConversation.TranscriptJson,
        [
            new AiCoachMessageDto(AiCoachRoles.User, "Help me review today's execution."),
            new AiCoachMessageDto(AiCoachRoles.Assistant, "Let's break it down."),
            new AiCoachMessageDto(AiCoachRoles.User, "What should I improve first?"),
            new AiCoachMessageDto(AiCoachRoles.Assistant, "Start by tightening your entry confirmation.")
        ]));
        _conversationSet.Verify(
            set => set.AddAsync(It.IsAny<AiCoachConversation>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _context.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenResearchModeRequested_PersistsResearchConversationSnapshot()
    {
        ChatWithCoach.Request request = new(
            [
                new AiCoachMessageDto(" User ", "Teach me AMD on NQ.")
            ],
            Mode: AiCoachModes.Research,
            UserId: 22);

        _aiService
            .Setup(service => service.ChatWithCoachAsync(
                It.Is<AiCoachRequestDto>(dto =>
                    dto.UserId == 22 &&
                    dto.Mode == AiCoachModes.Research &&
                    dto.Messages.Count == 1 &&
                    dto.Messages[0].Role == AiCoachRoles.User),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCoachResponseDto("AMD starts with accumulation before the displacement leg."));

        AiCoachConversation? savedConversation = null;
        _conversationSet
            .Setup(set => set.AddAsync(It.IsAny<AiCoachConversation>(), It.IsAny<CancellationToken>()))
            .Callback<AiCoachConversation, CancellationToken>((conversation, _) => savedConversation = conversation);

        var handler = new ChatWithCoach.Handler(_aiService.Object, _context.Object, _logger);

        var result = await handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(savedConversation);
        Assert.Equal(AiCoachModes.Research, savedConversation!.Mode);
        Assert.Equal(2, savedConversation.MessageCount);
        Assert.True(TranscriptMatches(savedConversation.TranscriptJson,
        [
            new AiCoachMessageDto(AiCoachRoles.User, "Teach me AMD on NQ."),
            new AiCoachMessageDto(AiCoachRoles.Assistant, "AMD starts with accumulation before the displacement leg.")
        ]));
        _conversationSet.Verify(
            set => set.AddAsync(It.IsAny<AiCoachConversation>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _context.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAiThrows_DoesNotPersistConversationSnapshot()
    {
        ChatWithCoach.Request request = new(
            [
                new AiCoachMessageDto(AiCoachRoles.User, "Help me.")
            ],
            UserId: 9);

        _aiService
            .Setup(service => service.ChatWithCoachAsync(It.IsAny<AiCoachRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("OpenRouter failed."));

        var handler = new ChatWithCoach.Handler(_aiService.Object, _context.Object, _logger);

        var result = await handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsFailure);
        _conversationSet.Verify(set => set.AddAsync(It.IsAny<AiCoachConversation>(), It.IsAny<CancellationToken>()), Times.Never);
        _context.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static bool TranscriptMatches(string transcriptJson, IReadOnlyList<AiCoachMessageDto> expectedMessages)
    {
        List<AiCoachMessageDto>? messages = JsonSerializer.Deserialize<List<AiCoachMessageDto>>(transcriptJson, JsonOptions);

        if (messages is null || messages.Count != expectedMessages.Count)
        {
            return false;
        }

        return messages.Zip(expectedMessages, (actual, expected) =>
                string.Equals(actual.Role, expected.Role, StringComparison.Ordinal) &&
                string.Equals(actual.Content, expected.Content, StringComparison.Ordinal))
            .All(matches => matches);
    }
}

public sealed class ChatWithCoachValidatorTests
{
    private readonly ChatWithCoach.Validator _validator = new();

    [Fact]
    public void Validate_WhenMessagesIsNull_ReturnsInvalidInsteadOfThrowing()
    {
        var result = _validator.Validate(new ChatWithCoach.Request(null!, AiCoachModes.Research, 1));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            string.Equals(error.PropertyName, "Messages", StringComparison.Ordinal) &&
            error.ErrorMessage.Contains("At least one message is required.", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenMessagesContainNullItem_ReturnsInvalid()
    {
        var result = _validator.Validate(new ChatWithCoach.Request([null!], AiCoachModes.Research, 1));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            string.Equals(error.PropertyName, "Messages", StringComparison.Ordinal) &&
            error.ErrorMessage.Contains("cannot contain null items", StringComparison.OrdinalIgnoreCase));
    }
}