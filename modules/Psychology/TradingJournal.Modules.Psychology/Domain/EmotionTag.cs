using System.ComponentModel.DataAnnotations.Schema;
using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.Psychology.Domain;

[Table(name: "EmotionTags", Schema = "Psychology")]
public sealed class EmotionTag : EntityBase<int>
{
    public string Name { get; set; } = string.Empty;

    public EmotionType EmotionType { get; set; }
}
