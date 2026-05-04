namespace TradingJournal.Modules.Auth.Features.V1.AdminDashboard;

public sealed class GetSystemMetrics
{
    internal sealed record RegistrationData(string Date, int UserSignups);

    internal sealed record SystemMetricsDto(
        int TotalUsers,
        int TotalStaff,
        int ActiveUsers,
        int ActiveStaff,
        List<RegistrationData> RegistrationChart
    );

    internal sealed record Request() : IQuery<Result<SystemMetricsDto>>;

    internal sealed class Handler(IAuthDbContext context) : IQueryHandler<Request, Result<SystemMetricsDto>>
    {
        public async Task<Result<SystemMetricsDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            var totalUsers = await context.Users.CountAsync(cancellationToken);
            var activeUsers = await context.Users.CountAsync(u => u.IsActive, cancellationToken);

            var totalStaff = await context.Staffs.CountAsync(cancellationToken);
            var activeStaff = await context.Staffs.CountAsync(s => s.IsActive, cancellationToken);

            var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30);

            var userRegistrations = await context.Users
                .Where(u => u.CreatedDate >= thirtyDaysAgo)
                .GroupBy(u => u.CreatedDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(g => g.Date)
                .ToListAsync(cancellationToken);

            // Fill empty days for charting
            var chartData = new List<RegistrationData>();
            for (int i = 29; i >= 0; i--)
            {
                var dt = DateTimeOffset.UtcNow.Date.AddDays(-i);
                var existing = userRegistrations.FirstOrDefault(x => x.Date == dt);
                chartData.Add(new RegistrationData(dt.ToString("MMM dd"), existing?.Count ?? 0));
            }

            var metrics = new SystemMetricsDto(totalUsers, totalStaff, activeUsers, activeStaff, chartData);
            return Result<SystemMetricsDto>.Success(metrics);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup($"{AuthConstants.Endpoints.AuthBase}/admin");

            group.MapGet("/metrics", async (ISender sender) =>
            {
                var result = await sender.Send(new Request());
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<SystemMetricsDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get general system metrics for Admin Dashboard.")
            .WithTags("Admin Dashboard")
            .RequireAuthorization("AdminOnly");
        }
    }
}
