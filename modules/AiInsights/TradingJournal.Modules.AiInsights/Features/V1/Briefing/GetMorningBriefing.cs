using TradingJournal.Modules.AiInsights.Dto;

namespace TradingJournal.Modules.AiInsights.Features.V1.Briefing;

public sealed class GetMorningBriefing
{
    internal record Request(int UserId) : IQuery<Result<MorningBriefingResultDto?>>;

    internal sealed class Handler(IAiInsightsDbContext db) : IQueryHandler<Request, Result<MorningBriefingResultDto?>>
    {
        public async Task<Result<MorningBriefingResultDto?>> Handle(Request request, CancellationToken cancellationToken)
        {
            DateOnly briefingDateUtc = DateOnly.FromDateTime(DateTime.UtcNow);

            MorningBriefing? briefing = await db.MorningBriefings
                .AsNoTracking()
                .Where(item => item.CreatedBy == request.UserId && item.BriefingDateUtc == briefingDateUtc)
                .FirstOrDefaultAsync(cancellationToken);

            return Result<MorningBriefingResultDto?>.Success(
                briefing is null ? null : MorningBriefingMapper.ToDto(briefing));
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiBriefing);

            group.MapGet(string.Empty, async (ISender sender, ClaimsPrincipal user) =>
            {
                Request request = new(user.GetCurrentUserId());
                Result<MorningBriefingResultDto?> result = await sender.Send(request);

                return Results.Ok(result);
            })
            .Produces<Result<MorningBriefingResultDto?>>(StatusCodes.Status200OK)
            .WithSummary("Get today's saved AI morning briefing.")
            .WithDescription("Returns the authenticated user's saved morning briefing for the current UTC day, if one exists.")
            .WithTags(Tags.AiBriefing)
            .RequireAuthorization();
        }
    }
}