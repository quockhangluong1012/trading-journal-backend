using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.Notifications.Common.Enums;
using TradingJournal.Modules.Notifications.EventHandlers;
using TradingJournal.Modules.Notifications.Services;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class AiTiltInterventionNotificationHandlerTests
{
    [Fact]
    public async Task Handle_WhenPayloadContainsNullsAndLargeActionItems_NormalizesAndBoundsNotification()
    {
        var notificationService = new Mock<INotificationService>();
        string? capturedTitle = null;
        string? capturedMessage = null;
        string? capturedMetadata = null;
        NotificationPriority? capturedPriority = null;

        notificationService
            .Setup(service => service.CreateAndPushAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<NotificationType>(),
                It.IsAny<NotificationPriority>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, string, string, NotificationType, NotificationPriority, string?, string?, CancellationToken>((_, title, message, _, priority, metadata, _, _) =>
            {
                capturedTitle = title;
                capturedMessage = message;
                capturedPriority = priority;
                capturedMetadata = metadata;
            })
            .ReturnsAsync(1);

        var handler = new AiTiltInterventionNotificationHandler(
            notificationService.Object,
            NullLogger<AiTiltInterventionNotificationHandler>.Instance);

        IReadOnlyList<string> actionItems = Enumerable.Range(0, 12)
            .Select(index => $"step-{index}-{new string('a', 250)}")
            .Cast<string>()
            .ToArray();

        await handler.Handle(
            new AiTiltInterventionDetectedEvent(
                Guid.NewGuid(),
                9,
                81,
                "High",
                null!,
                "revenge_trading",
                null!,
                null!,
                actionItems),
            CancellationToken.None);

        notificationService.Verify(service => service.CreateAndPushAsync(
            9,
            It.IsAny<string>(),
            It.IsAny<string>(),
            NotificationType.TiltWarning,
            It.IsAny<NotificationPriority>(),
            It.IsAny<string?>(),
            "/psychology",
            It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(string.Empty, capturedTitle);
        Assert.NotNull(capturedMessage);
        Assert.StartsWith("Next: step-0-", capturedMessage, StringComparison.Ordinal);
        Assert.True(capturedMessage.Length <= 1000);
        Assert.Equal(NotificationPriority.Low, capturedPriority);
        Assert.NotNull(capturedMetadata);
        Assert.True(capturedMetadata.Length <= 4000);

        using JsonDocument metadataDocument = JsonDocument.Parse(capturedMetadata);
        JsonElement actionItemsElement = metadataDocument.RootElement.GetProperty("ActionItems");
        Assert.Equal(10, actionItemsElement.GetArrayLength());
        Assert.All(actionItemsElement.EnumerateArray(), actionItem => Assert.True(actionItem.GetString()!.Length <= 200));
    }

    [Fact]
    public async Task Handle_WhenMessageIsLong_PreservesFullNextStepPreview()
    {
        var notificationService = new Mock<INotificationService>();
        string? capturedMessage = null;

        notificationService
            .Setup(service => service.CreateAndPushAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<NotificationType>(),
                It.IsAny<NotificationPriority>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, string, string, NotificationType, NotificationPriority, string?, string?, CancellationToken>((_, _, message, _, _, _, _, _) =>
            {
                capturedMessage = message;
            })
            .ReturnsAsync(1);

        var handler = new AiTiltInterventionNotificationHandler(
            notificationService.Object,
            NullLogger<AiTiltInterventionNotificationHandler>.Instance);

        await handler.Handle(
            new AiTiltInterventionDetectedEvent(
                Guid.NewGuid(),
                9,
                81,
                "High",
                "high",
                "revenge_trading",
                "Pause",
                new string('m', 1000),
                ["Review the setup before re-entry"]),
            CancellationToken.None);

        Assert.NotNull(capturedMessage);
        Assert.EndsWith(" Next: Review the setup before re-entry", capturedMessage, StringComparison.Ordinal);
        Assert.True(capturedMessage.Length <= 1000);
    }
}