using TradingJournal.Modules.Psychology.Services;
using TradingJournal.Modules.Psychology.ViewModel;

namespace TradingJournal.Modules.Psychology.Features.V1.Karma;

public sealed class RecalculateKarma
{
    internal record Request(int UserId = 0) : ICommand<Result<KarmaSummaryViewModel>>;

    internal sealed class Handler(IKarmaService karmaService) : ICommandHandler<Request, Result<KarmaSummaryViewModel>>
    {
        public async Task<Result<KarmaSummaryViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            var summary = await karmaService.RecalculateKarmaAsync(request.UserId, cancellationToken);
            return Result<KarmaSummaryViewModel>.Success(summary);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/karma");

            group.MapPost("recalculate", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId()));
                return result;
            })
            .Produces<Result<KarmaSummaryViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Recalculate karma")
            .WithDescription("Rebuilds karma points from all trade and journal activity for the authenticated user.")
            .WithTags("Karma")
            .RequireAuthorization();
        }
    }
}
