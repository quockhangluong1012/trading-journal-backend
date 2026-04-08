using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Shared.Dtos;

public sealed class EmotionTagCacheDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public EmotionType EmotionType { get; set; }
}
