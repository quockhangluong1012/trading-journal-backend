namespace TradingJournal.Modules.Notifications.Dto;

public record NotificationDto(
    int Id,
    string Title,
    string Message,
    string Type,
    string Priority,
    bool IsRead,
    DateTimeOffset? ReadAt,
    string? Metadata,
    string? ActionUrl,
    DateTimeOffset CreatedDate);

public record UnreadCountDto(int Count);
