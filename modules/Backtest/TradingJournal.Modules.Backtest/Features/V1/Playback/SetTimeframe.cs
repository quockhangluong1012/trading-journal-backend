namespace TradingJournal.Modules.Backtest.Features.V1.Playback;

public sealed class SetTimeframe
{
    public record Request(int SessionId, string Timeframe) : ICommand<Result<string>>
    {
        public int UserId { get; set; }
    }

    public sealed record Body(string Timeframe);

    internal sealed class Handler(IBacktestDbContext context, IPlaybackEngine playbackEngine)
        : ICommandHandler<Request, Result<string>>
    {
        public async Task<Result<string>> Handle(Request request, CancellationToken cancellationToken)
        {
            bool isOwner = await context.BacktestSessions
                .AnyAsync(s => s.Id == request.SessionId
                               && s.CreatedBy == request.UserId, cancellationToken);

            if (!isOwner)
                return Result<string>.Failure(Error.NotFound);

            if (string.IsNullOrWhiteSpace(request.Timeframe)
                || !Enum.TryParse(request.Timeframe, true, out Timeframe timeframe))
            {
                return Result<string>.Failure(Error.InvalidInput);
            }

            try
            {
                await playbackEngine.ChangeTimeframeAsync(request.SessionId, timeframe, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return Result<string>.Failure(Error.NotFound);
            }
            catch
            {
                return Result<string>.Failure(Error.UnexpectedError);
            }

            return Result<string>.Success(timeframe.ToString());
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Playback);

            group.MapPut("/{sessionId:int}/timeframe", async (int sessionId, [FromBody] Body body, ClaimsPrincipal user, ISender sender) =>
            {
                Result<string> result = await sender.Send(new Request(sessionId, body.Timeframe) with { UserId = user.GetCurrentUserId() });

                if (result.IsSuccess)
                {
                    return Results.Ok(result);
                }

                string errorCode = result.Errors.FirstOrDefault()?.Code ?? string.Empty;

                return errorCode switch
                {
                    "Error.NotFound" => Results.NotFound(result),
                    "Error.InvalidInput" => Results.BadRequest(result),
                    _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Failed to change timeframe")
                };
            })
            .Produces<Result<string>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Change the playback timeframe.")
            .WithDescription("Persists the selected playback timeframe so historical candles and future playback stay in sync.")
            .WithTags(Tags.BacktestPlayback)
            .RequireAuthorization();
        }
    }
}