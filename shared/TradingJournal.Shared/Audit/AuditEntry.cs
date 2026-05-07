using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
///     Timestamp DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
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
    public DateTime Timestamp { get; set; }
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
    internal EntityEntry? TrackedEntry { get; init; }

    public AuditEntry ToAuditEntry()
    {
        string entityId = TrackedEntry is null ? EntityId : GetPrimaryKeyValue(TrackedEntry, EntityId);

        return new AuditEntry
        {
            EntityName = EntityName,
            EntityId = entityId,
            Action = Action,
            ChangedBy = ChangedBy,
            Timestamp = DateTime.UtcNow,
            OldValues = OldValues.Count > 0 ? JsonSerializer.Serialize(OldValues) : null,
            NewValues = NewValues.Count > 0 ? JsonSerializer.Serialize(NewValues) : null,
            AffectedColumns = AffectedColumns.Count > 0 ? JsonSerializer.Serialize(AffectedColumns) : null
        };
    }

    private static string GetPrimaryKeyValue(EntityEntry entry, string fallbackEntityId)
    {
        List<string> keyValues = entry.Properties
            .Where(property => property.Metadata.IsPrimaryKey())
            .Select(property =>
            {
                object? keyValue = entry.State == EntityState.Deleted
                    ? property.OriginalValue
                    : property.CurrentValue;

                return keyValue?.ToString();
            })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();

        return keyValues.Count == 0 ? fallbackEntityId : string.Join(",", keyValues);
    }
}
