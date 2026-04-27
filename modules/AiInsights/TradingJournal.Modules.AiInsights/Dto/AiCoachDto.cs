namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record AiCoachMessageDto(string Role, string Content);

public sealed record AiCoachRequestDto(
    List<AiCoachMessageDto> Messages,
    int UserId = 0);

public sealed record AiCoachResponseDto(string Reply);
