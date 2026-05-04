using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TradingJournal.Shared.Audit;

/// <summary>
/// Persists audit log entries to a dedicated SQL table using raw ADO.NET.
/// Auto-creates the table on first use. Designed for fire-and-forget
/// persistence to avoid impacting the main SaveChanges performance.
/// </summary>
public interface IAuditLogStore
{
    Task SaveAsync(IReadOnlyList<AuditEntry> entries, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEntry>> QueryAsync(string? entityName, string? entityId,
        DateTimeOffset? from, DateTimeOffset? to, int maxResults = 100, CancellationToken ct = default);
}

internal sealed class SqlAuditLogStore(
    IConfiguration configuration,
    ILogger<SqlAuditLogStore> logger) : IAuditLogStore
{
    private string ConnectionString =>
        configuration.GetConnectionString("TradeDatabase")
        ?? throw new InvalidOperationException("TradeDatabase connection string is not configured.");

    private const string EnsureTableSql = """
        IF OBJECT_ID('[dbo].[AuditLogs]', 'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[AuditLogs] (
                Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                EntityName NVARCHAR(256) NOT NULL,
                EntityId NVARCHAR(128) NOT NULL,
                Action NVARCHAR(16) NOT NULL,
                ChangedBy INT NOT NULL,
                [Timestamp] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                OldValues NVARCHAR(MAX) NULL,
                NewValues NVARCHAR(MAX) NULL,
                AffectedColumns NVARCHAR(MAX) NULL
            );
            CREATE INDEX IX_AuditLogs_Entity ON [dbo].[AuditLogs] (EntityName, EntityId);
            CREATE INDEX IX_AuditLogs_Timestamp ON [dbo].[AuditLogs] ([Timestamp] DESC);
            CREATE INDEX IX_AuditLogs_ChangedBy ON [dbo].[AuditLogs] (ChangedBy);
        END
        """;

    private const string InsertSql = """
        INSERT INTO [dbo].[AuditLogs] (EntityName, EntityId, Action, ChangedBy, [Timestamp], OldValues, NewValues, AffectedColumns)
        VALUES (@EntityName, @EntityId, @Action, @ChangedBy, @Timestamp, @OldValues, @NewValues, @AffectedColumns)
        """;

    private bool _tableEnsured;

    public async Task SaveAsync(IReadOnlyList<AuditEntry> entries, CancellationToken ct = default)
    {
        if (entries.Count == 0) return;

        try
        {
            await EnsureTableAsync(ct);

            await using SqlConnection conn = new(ConnectionString);
            await conn.OpenAsync(ct);

            foreach (AuditEntry entry in entries)
            {
                await using SqlCommand cmd = new(InsertSql, conn);
                cmd.Parameters.Add(new SqlParameter("@EntityName", entry.EntityName));
                cmd.Parameters.Add(new SqlParameter("@EntityId", entry.EntityId));
                cmd.Parameters.Add(new SqlParameter("@Action", entry.Action));
                cmd.Parameters.Add(new SqlParameter("@ChangedBy", entry.ChangedBy));
                cmd.Parameters.Add(new SqlParameter("@Timestamp", entry.Timestamp));
                cmd.Parameters.Add(new SqlParameter("@OldValues", (object?)entry.OldValues ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@NewValues", (object?)entry.NewValues ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@AffectedColumns", (object?)entry.AffectedColumns ?? DBNull.Value));

                await cmd.ExecuteNonQueryAsync(ct);
            }

            logger.LogDebug("Audit: Persisted {Count} audit entries", entries.Count);
        }
        catch (Exception ex)
        {
            // Audit logging should never break the main operation
            logger.LogError(ex, "Audit: Failed to persist {Count} audit entries", entries.Count);
        }
    }

    public async Task<IReadOnlyList<AuditEntry>> QueryAsync(string? entityName, string? entityId,
        DateTimeOffset? from, DateTimeOffset? to, int maxResults = 100, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);

        var conditions = new List<string>();
        var parameters = new List<SqlParameter>();

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            conditions.Add("EntityName = @EntityName");
            parameters.Add(new SqlParameter("@EntityName", entityName));
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            conditions.Add("EntityId = @EntityId");
            parameters.Add(new SqlParameter("@EntityId", entityId));
        }

        if (from.HasValue)
        {
            conditions.Add("[Timestamp] >= @From");
            parameters.Add(new SqlParameter("@From", from.Value));
        }

        if (to.HasValue)
        {
            conditions.Add("[Timestamp] <= @To");
            parameters.Add(new SqlParameter("@To", to.Value));
        }

        string whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        string sql = $"""
            SELECT TOP (@MaxResults) Id, EntityName, EntityId, Action, ChangedBy, [Timestamp], OldValues, NewValues, AffectedColumns
            FROM [dbo].[AuditLogs]
            {whereClause}
            ORDER BY [Timestamp] DESC
            """;

        parameters.Add(new SqlParameter("@MaxResults", maxResults));

        await using SqlConnection conn = new(ConnectionString);
        await conn.OpenAsync(ct);

        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddRange([.. parameters]);

        var results = new List<AuditEntry>();
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            results.Add(new AuditEntry
            {
                Id = reader.GetInt64(0),
                EntityName = reader.GetString(1),
                EntityId = reader.GetString(2),
                Action = reader.GetString(3),
                ChangedBy = reader.GetInt32(4),
                Timestamp = reader.GetDateTimeOffset(5),
                OldValues = reader.IsDBNull(6) ? null : reader.GetString(6),
                NewValues = reader.IsDBNull(7) ? null : reader.GetString(7),
                AffectedColumns = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }

        return results;
    }

    private async Task EnsureTableAsync(CancellationToken ct)
    {
        if (_tableEnsured) return;

        await using SqlConnection conn = new(ConnectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(EnsureTableSql, conn);
        await cmd.ExecuteNonQueryAsync(ct);

        _tableEnsured = true;
    }
}
