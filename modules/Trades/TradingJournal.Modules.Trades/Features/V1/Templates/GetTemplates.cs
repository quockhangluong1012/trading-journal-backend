using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Trades.Features.V1.Templates;

public sealed class GetTemplates
{
    public class Request : IQuery<Result<List<TradeTemplateViewModel>>>
    {
        public int UserId { get; set; }
    }

    public sealed class Handler(ITradeDbContext context) : IQueryHandler<Request, Result<List<TradeTemplateViewModel>>>
    {
        public async Task<Result<List<TradeTemplateViewModel>>> Handle(Request request, CancellationToken cancellationToken)
        {
            List<TradeTemplate> templates = await context.TradeTemplates
                .AsNoTracking()
                .Include(t => t.TradingZone)
                .Where(t => t.CreatedBy == request.UserId && !t.IsDisabled)
                .OrderByDescending(t => t.IsFavorite)
                .ThenBy(t => t.SortOrder)
                .ThenByDescending(t => t.UsageCount)
                .ToListAsync(cancellationToken);

            List<TradeTemplateViewModel> viewModels = templates.Select(MapToViewModel).ToList();

            return Result<List<TradeTemplateViewModel>>.Success(viewModels);
        }

        internal static TradeTemplateViewModel MapToViewModel(TradeTemplate template)
        {
            return new TradeTemplateViewModel
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                Asset = template.Asset,
                Position = template.Position.HasValue ? (int)template.Position.Value : null,
                TradingZoneId = template.TradingZoneId,
                TradingZoneName = template.TradingZone?.Name,
                TradingSessionId = template.TradingSessionId,
                TradingSetupId = template.TradingSetupId,
                DefaultStopLoss = template.DefaultStopLoss,
                DefaultTargetTier1 = template.DefaultTargetTier1,
                DefaultTargetTier2 = template.DefaultTargetTier2,
                DefaultTargetTier3 = template.DefaultTargetTier3,
                DefaultConfidenceLevel = template.DefaultConfidenceLevel.HasValue
                    ? (int)template.DefaultConfidenceLevel.Value : null,
                DefaultNotes = template.DefaultNotes,
                DefaultChecklistIds = ParseCommaSeparatedIds(template.DefaultChecklistIds),
                DefaultEmotionTagIds = ParseCommaSeparatedIds(template.DefaultEmotionTagIds),
                DefaultTechnicalAnalysisTagIds = ParseCommaSeparatedIds(template.DefaultTechnicalAnalysisTagIds),
                UsageCount = template.UsageCount,
                IsFavorite = template.IsFavorite,
                SortOrder = template.SortOrder,
                CreatedDate = template.CreatedDate
            };
        }

        internal static List<int>? ParseCommaSeparatedIds(string? commaSeparated)
        {
            if (string.IsNullOrWhiteSpace(commaSeparated)) return null;

            return commaSeparated
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out int id) ? id : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeTemplates);

            group.MapGet("/", async (ISender sender, ClaimsPrincipal user) =>
            {
                Request request = new() { UserId = user.GetCurrentUserId() };
                Result<List<TradeTemplateViewModel>> result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<List<TradeTemplateViewModel>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get all trade templates.")
            .WithDescription("Retrieves all trade templates for the current user, sorted by favorite > sort order > usage.")
            .WithTags(Tags.TradeTemplates)
            .RequireAuthorization();
        }
    }
}
