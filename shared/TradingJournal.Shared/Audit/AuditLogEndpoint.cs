using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace TradingJournal.Shared.Audit;

/// <summary>
/// Extension method to register the audit log query endpoint.
/// Should be called in Program.cs after MapCarter().
/// </summary>
public static class AuditLogEndpointExtensions
{
    /// <summary>
    /// Maps the GET /api/v1/audit-logs endpoint for querying entity change history.
    /// Only accessible by users with the AdminOnly policy.
    /// </summary>
    public static IEndpointRouteBuilder MapAuditLogEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/audit-logs", async (
            IAuditLogStore store,
            string? entityName,
            string? entityId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? maxResults) =>
        {
            IReadOnlyList<AuditEntry> entries = await store.QueryAsync(
                entityName, entityId, from, to, maxResults ?? 100);

            return Results.Ok(new
            {
                count = entries.Count,
                data = entries
            });
        })
        .Produces(StatusCodes.Status200OK)
        .WithSummary("Query audit logs.")
        .WithDescription("Returns audit trail entries filtered by entity name, entity ID, and/or date range.")
        .WithTags("Audit")
        .RequireAuthorization("AdminOnly");

        return app;
    }
}
