using System.Text.Json;

namespace TradingJournal.Shared.Audit;

/// <summary>
/// Records a single entity change event (Create, Update, Delete).
/// Captures before/after state as JSON for full traceability.
/// 
/// Table DDL (auto-created on first use):
/// <code>
/// CREATE TABLE [dbo].[AuditLogs] (
///     Id BIGINT IDENTITY(1,1) PRIMARY KEY,
///     EntityName NVARCHAR(256) NOT NULL,
///     EntityId NVARCHAR(128) NOT NULL,
///     Action NVARCHAR(16) NOT NULL,
///     ChangedBy INT NOT NULL,
///     Timestamp DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
///     OldValues NVARCHAR(MAX) NULL,
///     NewValues NVARCHAR(MAX) NULL,
///     AffectedColumns NVARCHAR(MAX) NULL
/// );
/// </code>
/// </summary>
public sealed class AuditEntry
{
    public long Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // "Create", "Update", "Delete"
    public int ChangedBy { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? AffectedColumns { get; set; }
}

/// <summary>
/// Represents a pending audit entry before it is persisted.
/// Built from EF Core ChangeTracker entries.
/// </summary>
public sealed class AuditEntryBuilder
{
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int ChangedBy { get; set; }
    public Dictionary<string, object?> OldValues { get; } = [];
    public Dictionary<string, object?> NewValues { get; } = [];
    public List<string> AffectedColumns { get; } = [];

    public AuditEntry ToAuditEntry()
    {
        return new AuditEntry
        {
            EntityName = EntityName,
            EntityId = EntityId,
            Action = Action,
            ChangedBy = ChangedBy,
            Timestamp = DateTimeOffset.UtcNow,
            OldValues = OldValues.Count > 0 ? JsonSerializer.Serialize(OldValues) : null,
            NewValues = NewValues.Count > 0 ? JsonSerializer.Serialize(NewValues) : null,
            AffectedColumns = AffectedColumns.Count > 0 ? JsonSerializer.Serialize(AffectedColumns) : null
        };
    }
}
