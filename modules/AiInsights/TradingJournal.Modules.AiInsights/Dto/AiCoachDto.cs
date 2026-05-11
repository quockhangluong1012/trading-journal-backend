namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record AiCoachMessageDto(string Role, string Content);

public static class AiCoachRoles
{
    public const string User = "user";

    public const string Assistant = "assistant";

    public static bool IsValid(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        string normalized = role.Trim().ToLowerInvariant();
        return normalized is User or Assistant;
    }

    public static string Normalize(string role)
    {
        return role.Trim().ToLowerInvariant();
    }
}

public static class AiCoachModes
{
    public const string Coach = "coach";

    public const string Research = "research";

    public static bool IsValid(string? mode)
    {
        if (mode is null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(mode))
        {
            return false;
        }

        string normalized = Normalize(mode);
        return normalized is Coach or Research;
    }

    public static string Normalize(string? mode)
    {
        return string.IsNullOrWhiteSpace(mode)
            ? Coach
            : mode.Trim().ToLowerInvariant();
    }
}

public sealed record AiCoachRequestDto(
    List<AiCoachMessageDto> Messages,
    int UserId = 0,
    string Mode = AiCoachModes.Coach);

public sealed record AiCoachResponseDto(string Reply);
