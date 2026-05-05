namespace TradingJournal.Modules.Notifications.Dto;

public record NotificationDto(
    int Id,
    string Title,
    string Message,
    string Type,
    string Priority,
    bool IsRead,
    DateTime? ReadAt,
    string? Metadata,
    string? ActionUrl,
    DateTime CreatedDate);

public record UnreadCountDto(int Count);
