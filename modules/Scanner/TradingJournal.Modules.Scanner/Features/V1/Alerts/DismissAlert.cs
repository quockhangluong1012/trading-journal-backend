namespace TradingJournal.Modules.Scanner.Features.V1.Alerts;

public sealed class DismissAlert
{
    public record Command() : ICommand<Result<bool>>
    {
        public int UserId { get; set; }
        public int AlertId { get; set; }
    }

    internal sealed class Handler(IScannerDbContext context)
        : ICommandHandler<Command, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Command request, CancellationToken cancellationToken)
        {
            ScannerAlert? alert = await context.ScannerAlerts
                .FirstOrDefaultAsync(a => a.Id == request.AlertId && a.UserId == request.UserId, cancellationToken);

            if (alert is null)
                return Result<bool>.Failure(new Error("AlertNotFound", "Alert not found."));

            alert.IsDismissed = true;

            await context.SaveChangesAsync(cancellationToken);

            return Result<bool>.Success(true);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Alerts);

            group.MapPut("/{id:int}/dismiss", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result<bool> result = await sender.Send(new Command
                {
                    UserId = user.GetCurrentUserId(),
                    AlertId = id
                });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .WithSummary("Dismiss a scanner alert.")
            .WithTags(Tags.Alerts)
            .RequireAuthorization();
        }
    }
}
