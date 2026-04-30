using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Trades.Features.V1.Templates;

public sealed class UpdateTemplate
{
    public record Request(
        int Id,
        string Name,
        string? Description,
        string? Asset,
        PositionType? Position,
        int? TradingZoneId,
        int? TradingSessionId,
        int? TradingSetupId,
        decimal? DefaultStopLoss,
        decimal? DefaultTargetTier1,
        decimal? DefaultTargetTier2,
        decimal? DefaultTargetTier3,
        ConfidenceLevel? DefaultConfidenceLevel,
        string? DefaultNotes,
        List<int>? DefaultChecklistIds,
        List<int>? DefaultEmotionTagIds,
        List<int>? DefaultTechnicalAnalysisTagIds,
        bool IsFavorite,
        int SortOrder) : ICommand<Result<bool>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Template Id must be greater than 0.");

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Template name is required.")
                .MaximumLength(200).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Template name cannot exceed 200 characters.");
        }
    }

    public sealed class Handler(ITradeDbContext context, IHttpContextAccessor httpContextAccessor)
        : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            int userId = httpContextAccessor.HttpContext?.User.GetCurrentUserId() ?? 0;
            if (userId <= 0)
            {
                return Result<bool>.Failure(Error.Create("Unauthorized."));
            }

            TradeTemplate? template = await context.TradeTemplates
                .FirstOrDefaultAsync(t => t.Id == request.Id && t.CreatedBy == userId && !t.IsDisabled,
                    cancellationToken);

            if (template is null)
            {
                return Result<bool>.Failure(Error.Create("Template not found."));
            }

            // Check for duplicate name (excluding current template)
            bool nameExists = await context.TradeTemplates
                .AsNoTracking()
                .AnyAsync(t => t.CreatedBy == userId && t.Name == request.Name
                    && t.Id != request.Id && !t.IsDisabled, cancellationToken);

            if (nameExists)
            {
                return Result<bool>.Failure(Error.Create($"A template named '{request.Name}' already exists."));
            }

            template.Name = request.Name.Trim();
            template.Description = request.Description?.Trim();
            template.Asset = request.Asset?.Trim().ToUpperInvariant();
            template.Position = request.Position;
            template.TradingZoneId = request.TradingZoneId;
            template.TradingSessionId = request.TradingSessionId;
            template.TradingSetupId = request.TradingSetupId;
            template.DefaultStopLoss = request.DefaultStopLoss;
            template.DefaultTargetTier1 = request.DefaultTargetTier1;
            template.DefaultTargetTier2 = request.DefaultTargetTier2;
            template.DefaultTargetTier3 = request.DefaultTargetTier3;
            template.DefaultConfidenceLevel = request.DefaultConfidenceLevel;
            template.DefaultNotes = request.DefaultNotes;
            template.DefaultChecklistIds = request.DefaultChecklistIds is { Count: > 0 }
                ? string.Join(",", request.DefaultChecklistIds) : null;
            template.DefaultEmotionTagIds = request.DefaultEmotionTagIds is { Count: > 0 }
                ? string.Join(",", request.DefaultEmotionTagIds) : null;
            template.DefaultTechnicalAnalysisTagIds = request.DefaultTechnicalAnalysisTagIds is { Count: > 0 }
                ? string.Join(",", request.DefaultTechnicalAnalysisTagIds) : null;
            template.IsFavorite = request.IsFavorite;
            template.SortOrder = request.SortOrder;

            int rows = await context.SaveChangesAsync(cancellationToken);

            return rows > 0
                ? Result<bool>.Success(true)
                : Result<bool>.Failure(Error.Create("Failed to update trade template."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeTemplates);

            group.MapPut("/{id:int}", async (int id, [FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                if (id != request.Id)
                {
                    return Results.BadRequest(Result<bool>.Failure(Error.Create("Route ID does not match request body.")));
                }

                Result<bool> result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Update a trade template.")
            .WithDescription("Updates an existing trade template with new default values.")
            .WithTags(Tags.TradeTemplates)
            .RequireAuthorization();
        }
    }
}
