using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.AiInsights.Domain;

[Table(name: "MorningBriefings", Schema = "Trades")]
public sealed class MorningBriefing : EntityBase<int>
{
    public DateOnly BriefingDateUtc { get; set; }

    public string Greeting { get; set; } = string.Empty;

    public string Briefing { get; set; } = string.Empty;

    public string ActionItem { get; set; } = string.Empty;

    public string OverallMood { get; set; } = string.Empty;

    public string FocusAreasJson { get; set; } = "[]";

    public string WarningsJson { get; set; } = "[]";
}