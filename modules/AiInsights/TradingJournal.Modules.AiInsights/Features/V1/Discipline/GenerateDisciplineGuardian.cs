using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.AiInsights.Features.V1.Discipline;

public sealed class GenerateDisciplineGuardian
{
    public sealed record Request(int UserId = 0) : ICommand<Result<AiTiltInterventionResultDto>>;

    public sealed class Handler(
        IDisciplineContextProvider disciplineContextProvider,
        IOpenRouterAIService aiService)
        : ICommandHandler<Request, Result<AiTiltInterventionResultDto>>
    {
        public async Task<Result<AiTiltInterventionResultDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            try
            {
                DisciplineGuardianContextDto context = await disciplineContextProvider.GetDisciplineContextAsync(
                    request.UserId,
                    cancellationToken);

                AiTiltInterventionResultDto? response = await aiService.AnalyzeTiltInterventionAsync(
                    new AiTiltInterventionRequestDto(
                        request.UserId,
                        context.TiltScore,
                        context.TiltLevel,
                        context.ConsecutiveLosses,
                        context.TradesLastHour,
                        context.RuleBreaksToday,
                        context.TodayPnl,
                        context.CooldownUntil),
                    cancellationToken);

                return response is not null
                    ? Result<AiTiltInterventionResultDto>.Success(response)
                    : Result<AiTiltInterventionResultDto>.Failure(Error.Create("AI discipline guardian returned empty response."));
            }
            catch (InvalidOperationException ex)
            {
                return Result<AiTiltInterventionResultDto>.Failure(
                    Error.Create(ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.AiDiscipline);

            group.MapPost("/guardian", async (ISender sender, ClaimsPrincipal user) =>
            {
                Result<AiTiltInterventionResultDto> result = await sender.Send(new Request(user.GetCurrentUserId()));

                return ToHttpResult(result);
            })
            .Produces<Result<AiTiltInterventionResultDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Generate AI discipline guardian guidance.")
            .WithDescription("Uses the current tilt and discipline signals to produce AI guidance before behavior deteriorates further.")
            .WithTags(Tags.AiDiscipline)
            .RequireAuthorization();
        }

        private static IResult ToHttpResult(Result<AiTiltInterventionResultDto> result)
        {
            if (result.IsSuccess)
            {
                return Results.Ok(result);
            }

            return result.Errors.Any(error => error.Code == HttpStatusCode.BadRequest.ToString())
                ? Results.BadRequest(result)
                : Results.Problem(result.Errors[0].Description);
        }
    }
}