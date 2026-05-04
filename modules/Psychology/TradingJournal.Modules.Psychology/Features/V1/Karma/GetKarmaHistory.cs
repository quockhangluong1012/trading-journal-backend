using TradingJournal.Modules.Psychology.Services;
using TradingJournal.Modules.Psychology.ViewModel;

namespace TradingJournal.Modules.Psychology.Features.V1.Karma;

public sealed class GetKarmaHistory
{
    internal record Request(int UserId = 0, int Days = 30) : IQuery<Result<List<KarmaEventViewModel>>>;

    internal sealed class Handler(IKarmaService karmaService) : IQueryHandler<Request, Result<List<KarmaEventViewModel>>>
    {
        public async Task<Result<List<KarmaEventViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            var history = await karmaService.GetKarmaHistoryAsync(request.UserId, request.Days, cancellationToken);
            return Result<List<KarmaEventViewModel>>.Success(history);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("api/v1/karma/history", async (ClaimsPrincipal user, ISender sender, int days = 30) =>
            {
                var result = await sender.Send(new Request(user.GetCurrentUserId(), days));
                return result;
            })
            .Produces<Result<List<KarmaEventViewModel>>>(StatusCodes.Status200OK)
            .WithSummary("Get karma history")
            .WithDescription("Returns karma event history for the authenticated user within the specified number of days.")
            .WithTags("Karma")
            .RequireAuthorization();
        }
    }
}
