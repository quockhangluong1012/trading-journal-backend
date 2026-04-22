namespace TradingJournal.Modules.Trades.Features.V1.TradingZone;

public sealed class UpdateTradingZone
{
    public sealed record Request(int Id, string Name, string FromTime, string ToTime, string? Description, int UserId = 0) : ICommand<Result<bool>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Trading Zone Id must be greater than 0.");

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Trading Zone Name is required.");

            RuleFor(x => x.FromTime)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("FromTime cannot be null.")
                .NotEmpty().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("FromTime cannot be empty.")
                .Must(BeAValidTime).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("FromTime must be in a valid time format (e.g., HH:mm).");
            RuleFor(x => x.ToTime)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("ToTime cannot be null.")
                .NotEmpty().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("ToTime cannot be empty.")
                .Must(BeAValidTime).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("ToTime must be in a valid time format (e.g., HH:mm).");
        }

        private bool BeAValidTime(string time)
        {
            return TimeSpan.TryParse(time, out _);
        }
    }

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            var tradingZone = await context.TradingZones.FirstOrDefaultAsync(x => x.Id == request.Id && x.CreatedBy == request.UserId, cancellationToken);

            if (tradingZone is null)
            {
                return Result<bool>.Failure(Error.NotFound);
            }

            tradingZone.Name = request.Name;
            tradingZone.FromTime = request.FromTime;
            tradingZone.ToTime = request.ToTime;
            tradingZone.Description = request.Description;

            await context.SaveChangesAsync(cancellationToken);

            return Result<bool>.Success(true);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingZones);

            group.MapPut("/", async ([FromBody] Request request, ClaimsPrincipal user, ISender sender) =>
            {
                Result<bool> result = await sender.Send(request with { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Update a trading zone by ID.")
            .WithDescription("Updates a trading zone by its ID.")
            .WithTags(Tags.TradingZones)
            .RequireAuthorization("AdminOnly");
        }
    }
}