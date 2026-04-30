namespace TradingJournal.Modules.Setups.Features.V1.TradingSetups;

public sealed class ReactivateSetup
{
    public sealed record Request(int SetupId, int UserId = 0) : ICommand<Result<bool>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.SetupId)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Setup ID is required.");
        }
    }

    public sealed class Handler(ISetupDbContext context) : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (request.UserId <= 0)
            {
                return Result<bool>.Failure(Error.Create("Current user is required."));
            }

            TradingSetup? setup = await context.TradingSetups
                .FirstOrDefaultAsync(s => s.Id == request.SetupId && s.CreatedBy == request.UserId && !s.IsDisabled,
                    cancellationToken);

            if (setup is null)
            {
                return Result<bool>.Failure(Error.Create("Setup not found."));
            }

            if (setup.Status != SetupStatus.Retired)
            {
                return Result<bool>.Failure(Error.Create("Only retired setups can be reactivated."));
            }

            setup.Status = SetupStatus.Active;
            setup.RetiredReason = null;
            setup.RetiredDate = null;

            await context.SaveChangesAsync(cancellationToken);

            return Result<bool>.Success(true);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradingSetups);

            group.MapPost("/{setupId:int}/reactivate", async (int setupId, ClaimsPrincipal user, ISender sender) =>
            {
                var request = new Request(setupId, user.GetCurrentUserId());
                Result<bool> result = await sender.Send(request);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<bool>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Reactivate a previously retired trading setup.")
            .WithTags(Tags.TradingSetups)
            .RequireAuthorization();
        }
    }
}
