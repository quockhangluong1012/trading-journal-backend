using TradingJournal.Shared.Extensions;
using DomainEntity = TradingJournal.Modules.Trades.Domain.TradingProfile;

namespace TradingJournal.Modules.Trades.Features.V1.TradingProfile;

public sealed class UpdateTradingProfile
{
    public record Request(int? MaxTradesPerDay, decimal? MaxDailyLossPercentage, int? MaxConsecutiveLosses, bool IsDisciplineEnabled) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.MaxTradesPerDay)
                .GreaterThanOrEqualTo(0).When(x => x.MaxTradesPerDay.HasValue)
                .WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("MaxTradesPerDay cannot be negative.");

            RuleFor(x => x.MaxDailyLossPercentage)
                .GreaterThanOrEqualTo(0).When(x => x.MaxDailyLossPercentage.HasValue)
                .WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("MaxDailyLossPercentage cannot be negative.");

            RuleFor(x => x.MaxConsecutiveLosses)
                .GreaterThanOrEqualTo(0).When(x => x.MaxConsecutiveLosses.HasValue)
                .WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("MaxConsecutiveLosses cannot be negative.");
        }
    }

    public sealed class Handler(ITradeDbContext context, IHttpContextAccessor httpContextAccessor) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            int userId = httpContextAccessor.HttpContext?.User.GetCurrentUserId() ?? 0;
            if (userId == 0) return Result<int>.Failure(Error.Create("Unauthorized"));

            try
            {
                return await context.ExecuteInTransactionAsync(async ct =>
                {
                    var profile = await context.TradingProfiles
                        .FirstOrDefaultAsync(x => x.CreatedBy == userId, ct);

                    if (profile == null)
                    {
                        profile = new DomainEntity
                        {
                            Id = 0,
                            MaxTradesPerDay = request.MaxTradesPerDay,
                            MaxDailyLossPercentage = request.MaxDailyLossPercentage,
                            MaxConsecutiveLosses = request.MaxConsecutiveLosses,
                            IsDisciplineEnabled = request.IsDisciplineEnabled
                        };
                        await context.TradingProfiles.AddAsync(profile, ct);
                    }
                    else
                    {
                        profile.MaxTradesPerDay = request.MaxTradesPerDay;
                        profile.MaxDailyLossPercentage = request.MaxDailyLossPercentage;
                        profile.MaxConsecutiveLosses = request.MaxConsecutiveLosses;
                        profile.IsDisciplineEnabled = request.IsDisciplineEnabled;
                        context.TradingProfiles.Update(profile);
                    }

                    await context.SaveChangesAsync(ct);

                    return Result<int>.Success(profile.Id);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                return Result<int>.Failure(Error.Create(ex.Message));
            }
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingProfiles);

            group.MapPost("/", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                Result<int> result = await sender.Send(request);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Update user's trading profile.")
            .WithTags(Tags.TradingProfiles)
            .RequireAuthorization();
        }
    }
}
