namespace TradingJournal.Modules.Psychology.ViewModel;

public sealed class KarmaSummaryViewModel
{
    public int TotalKarma { get; set; }
    public int Level { get; set; }
    public string Title { get; set; } = string.Empty;
    public int PointsToNextLevel { get; set; }
    public int NextLevelThreshold { get; set; }
    public double LevelProgress { get; set; }
    public int TotalAchievements { get; set; }
    public int UnlockedAchievements { get; set; }
    public int CurrentJournalingStreak { get; set; }
    public List<KarmaEventViewModel> RecentEvents { get; set; } = [];
}

public sealed class KarmaEventViewModel
{
    public string ActionType { get; set; } = string.Empty;
    public int Points { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
}

public sealed class AchievementViewModel
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public bool IsUnlocked { get; set; }
    public DateTimeOffset? UnlockedAt { get; set; }
    public string Category { get; set; } = string.Empty;
}
