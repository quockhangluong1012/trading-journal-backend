using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Trades.Features.V1.TradingProfile;

public sealed class GetTradingProfile
{
    public record Request() : IQuery<Result<Response>>;

    public record Response(int Id, int? MaxTradesPerDay, double? MaxDailyLossPercentage, int? MaxConsecutiveLosses, bool IsDisciplineEnabled);

    public sealed class Handler(ITradeDbContext context, IHttpContextAccessor httpContextAccessor) : IQueryHandler<Request, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Request request, CancellationToken cancellationToken)
        {
            int userId = httpContextAccessor.HttpContext?.User.GetCurrentUserId() ?? 0;
            if (userId == 0) return Result<Response>.Failure(Error.Create("Unauthorized"));

            var profile = await context.TradingProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CreatedBy == userId, cancellationToken);

            if (profile == null)
            {
                return Result<Response>.Success(new Response(0, null, null, null, false));
            }

            return Result<Response>.Success(new Response(
                profile.Id,
                profile.MaxTradesPerDay,
                profile.MaxDailyLossPercentage,
                profile.MaxConsecutiveLosses,
                profile.IsDisciplineEnabled));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingProfiles);

            group.MapGet("/", async (ISender sender) =>
            {
                Result<Response> result = await sender.Send(new Request());
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<Response>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get user's trading profile.")
            .WithTags(Tags.TradingProfiles)
            .RequireAuthorization();
        }
    }
}
