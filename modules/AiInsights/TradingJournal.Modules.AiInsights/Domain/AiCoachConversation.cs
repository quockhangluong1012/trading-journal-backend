using System.ComponentModel.DataAnnotations.Schema;
using TradingJournal.Shared.Abstractions;
using TradingJournal.Shared.Audit;

namespace TradingJournal.Modules.AiInsights.Domain;

[Table(name: "AiCoachConversations", Schema = "Trades")]
public sealed class AiCoachConversation : EntityBase<int>
{
    public string Mode { get; set; } = string.Empty;

    public int MessageCount { get; set; }

    [AuditIgnore]
    public string TranscriptJson { get; set; } = "[]";
}