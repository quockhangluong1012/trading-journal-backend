using TradingJournal.Modules.Psychology.Services;
using TradingJournal.Modules.Psychology.ViewModel;

namespace TradingJournal.Modules.Psychology.Features.V1.Karma;

public sealed class GetKarmaSummary
{
    internal record Request(int UserId = 0) : IQuery<Result<KarmaSummaryViewModel>>;

    internal sealed class Handler(IKarmaService karmaService) : IQueryHandler<Request, Result<KarmaSummaryViewModel>>
    {
        public async Task<Result<KarmaSummaryViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            var summary = await karmaService.GetKarmaSummaryAsync(request.UserId, cancellationToken);
            return Result<KarmaSummaryViewModel>.Success(summary);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("api/v1/karma/summary", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result;
            })
            .Produces<Result<KarmaSummaryViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Get karma summary")
            .WithDescription("Returns the karma summary including total points, level, title, and recent events for the authenticated user.")
            .WithTags("Karma")
            .RequireAuthorization();
        }
    }
}
