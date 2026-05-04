using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TradingJournal.Shared.Idempotency;

/// <summary>
/// SQL Server-backed idempotency store using raw ADO.NET for minimal overhead.
/// Stores cached responses in a dedicated [dbo].[IdempotencyKeys] table.
/// 
/// Table DDL (run once):
/// <code>
/// CREATE TABLE [dbo].[IdempotencyKeys] (
///     IdempotencyKey NVARCHAR(128) NOT NULL PRIMARY KEY,
///     StatusCode INT NOT NULL,
///     ResponseBody NVARCHAR(MAX) NULL,
///     ContentType NVARCHAR(256) NULL,
///     CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
///     ExpiresAt DATETIMEOFFSET NOT NULL
/// );
/// CREATE INDEX IX_IdempotencyKeys_ExpiresAt ON [dbo].[IdempotencyKeys] (ExpiresAt);
/// </code>
/// </summary>
internal sealed class SqlIdempotencyStore(
    IConfiguration configuration,
    ILogger<SqlIdempotencyStore> logger) : IIdempotencyStore
{
    private string ConnectionString =>
        configuration.GetConnectionString("TradeDatabase")
        ?? throw new InvalidOperationException("TradeDatabase connection string is not configured.");

    private const string EnsureTableSql = """
        IF OBJECT_ID('[dbo].[IdempotencyKeys]', 'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[IdempotencyKeys] (
                IdempotencyKey NVARCHAR(128) NOT NULL PRIMARY KEY,
                StatusCode INT NOT NULL,
                ResponseBody NVARCHAR(MAX) NULL,
                ContentType NVARCHAR(256) NULL,
                CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                ExpiresAt DATETIMEOFFSET NOT NULL
            );
            CREATE INDEX IX_IdempotencyKeys_ExpiresAt ON [dbo].[IdempotencyKeys] (ExpiresAt);
        END
        """;

    private const string GetSql = """
        SELECT StatusCode, ResponseBody, ContentType, CreatedAt, ExpiresAt
        FROM [dbo].[IdempotencyKeys]
        WHERE IdempotencyKey = @Key AND ExpiresAt > SYSDATETIMEOFFSET()
        """;

    private const string InsertSql = """
        IF NOT EXISTS (SELECT 1 FROM [dbo].[IdempotencyKeys] WHERE IdempotencyKey = @Key)
        BEGIN
            INSERT INTO [dbo].[IdempotencyKeys] (IdempotencyKey, StatusCode, ResponseBody, ContentType, CreatedAt, ExpiresAt)
            VALUES (@Key, @StatusCode, @ResponseBody, @ContentType, @CreatedAt, @ExpiresAt);
            SELECT 1;
        END
        ELSE
            SELECT 0;
        """;

    private const string CleanupSql = """
        DELETE FROM [dbo].[IdempotencyKeys] WHERE ExpiresAt <= SYSDATETIMEOFFSET()
        """;

    private bool _tableEnsured;

    public async Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);

        await using SqlConnection conn = new(ConnectionString);
        await conn.OpenAsync(ct);

        await using SqlCommand cmd = new(GetSql, conn);
        cmd.Parameters.Add(new SqlParameter("@Key", key));

        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new IdempotencyRecord
        {
            IdempotencyKey = key,
            StatusCode = reader.GetInt32(0),
            ResponseBody = reader.IsDBNull(1) ? null : reader.GetString(1),
            ContentType = reader.IsDBNull(2) ? null : reader.GetString(2),
            CreatedAt = reader.GetDateTimeOffset(3),
            ExpiresAt = reader.GetDateTimeOffset(4)
        };
    }

    public async Task<bool> SaveAsync(IdempotencyRecord record, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);

        await using SqlConnection conn = new(ConnectionString);
        await conn.OpenAsync(ct);

        await using SqlCommand cmd = new(InsertSql, conn);
        cmd.Parameters.Add(new SqlParameter("@Key", record.IdempotencyKey));
        cmd.Parameters.Add(new SqlParameter("@StatusCode", record.StatusCode));
        cmd.Parameters.Add(new SqlParameter("@ResponseBody", (object?)record.ResponseBody ?? DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@ContentType", (object?)record.ContentType ?? DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@CreatedAt", record.CreatedAt));
        cmd.Parameters.Add(new SqlParameter("@ExpiresAt", record.ExpiresAt));

        object? result = await cmd.ExecuteScalarAsync(ct);
        bool inserted = result is int i && i == 1;

        if (inserted)
        {
            logger.LogDebug("Idempotency key stored: {Key}", record.IdempotencyKey);
        }

        return inserted;
    }

    public async Task CleanupExpiredAsync(CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);

        await using SqlConnection conn = new(ConnectionString);
        await conn.OpenAsync(ct);

        await using SqlCommand cmd = new(CleanupSql, conn);
        int deleted = await cmd.ExecuteNonQueryAsync(ct);

        if (deleted > 0)
        {
            logger.LogInformation("Idempotency cleanup: removed {Count} expired records", deleted);
        }
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
