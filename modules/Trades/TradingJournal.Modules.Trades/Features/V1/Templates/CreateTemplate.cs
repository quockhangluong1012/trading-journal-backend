using Mapster;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Trades.Features.V1.Templates;

public sealed class CreateTemplate
{
    public record Request(
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
        bool IsFavorite) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Template name is required.")
                .MaximumLength(200).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Template name cannot exceed 200 characters.");
        }
    }

    public sealed class Handler(ITradeDbContext context, IHttpContextAccessor httpContextAccessor)
        : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            int userId = httpContextAccessor.HttpContext?.User.GetCurrentUserId() ?? 0;
            if (userId <= 0)
            {
                return Result<int>.Failure(Error.Create("Unauthorized."));
            }

            // Check for duplicate template name for this user
            bool nameExists = await context.TradeTemplates
                .AsNoTracking()
                .AnyAsync(t => t.CreatedBy == userId && t.Name == request.Name && !t.IsDisabled, cancellationToken);

            if (nameExists)
            {
                return Result<int>.Failure(Error.Create($"A template named '{request.Name}' already exists."));
            }

            TradeTemplate template = new()
            {
                Id = 0,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Asset = request.Asset?.Trim().ToUpperInvariant(),
                Position = request.Position,
                TradingZoneId = request.TradingZoneId,
                TradingSessionId = request.TradingSessionId,
                TradingSetupId = request.TradingSetupId,
                DefaultStopLoss = request.DefaultStopLoss,
                DefaultTargetTier1 = request.DefaultTargetTier1,
                DefaultTargetTier2 = request.DefaultTargetTier2,
                DefaultTargetTier3 = request.DefaultTargetTier3,
                DefaultConfidenceLevel = request.DefaultConfidenceLevel,
                DefaultNotes = request.DefaultNotes,
                DefaultChecklistIds = request.DefaultChecklistIds is { Count: > 0 }
                    ? string.Join(",", request.DefaultChecklistIds) : null,
                DefaultEmotionTagIds = request.DefaultEmotionTagIds is { Count: > 0 }
                    ? string.Join(",", request.DefaultEmotionTagIds) : null,
                DefaultTechnicalAnalysisTagIds = request.DefaultTechnicalAnalysisTagIds is { Count: > 0 }
                    ? string.Join(",", request.DefaultTechnicalAnalysisTagIds) : null,
                IsFavorite = request.IsFavorite,
                UsageCount = 0,
                SortOrder = 0
            };

            await context.TradeTemplates.AddAsync(template, cancellationToken);
            int rows = await context.SaveChangesAsync(cancellationToken);

            return rows > 0
                ? Result<int>.Success(template.Id)
                : Result<int>.Failure(Error.Create("Failed to create trade template."));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeTemplates);

            group.MapPost("/", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                Result<int> result = await sender.Send(request);

                return result.IsSuccess
                    ? Results.Created($"{ApiGroup.V1.TradeTemplates}/{result.Value}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Create a new trade template.")
            .WithDescription("Creates a reusable trade template with default field values.")
            .WithTags(Tags.TradeTemplates)
            .RequireAuthorization();
        }
    }
}
