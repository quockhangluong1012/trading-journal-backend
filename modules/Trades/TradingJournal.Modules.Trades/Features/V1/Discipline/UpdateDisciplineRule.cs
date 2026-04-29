namespace TradingJournal.Modules.Trades.Features.V1.Discipline;

public sealed class UpdateDisciplineRule
{
    public sealed record Request(int Id, string Name, string? Description,
        LessonCategory Category, bool IsActive, int SortOrder) : ICommand<Result>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(1000);
            RuleFor(x => x.Category).Must(Enum.IsDefined);
        }
    }

    public sealed class Handler(ITradeDbContext context, IHttpContextAccessor http)
        : ICommandHandler<Request, Result>
    {
        public async Task<Result> Handle(Request request, CancellationToken cancellationToken)
        {
            int userId = http.HttpContext?.User.GetCurrentUserId() ?? 0;

            DisciplineRule? rule = await context.DisciplineRules
                .FirstOrDefaultAsync(r => r.Id == request.Id && r.CreatedBy == userId, cancellationToken);

            if (rule is null)
                return Result.Failure(Error.Create("Rule not found."));

            rule.Name = request.Name;
            rule.Description = request.Description;
            rule.Category = request.Category;
            rule.IsActive = request.IsActive;
            rule.SortOrder = request.SortOrder;

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Discipline);

            group.MapPut("/rules/{id:int}", async (int id, [FromBody] Request request, ISender sender) =>
            {
                if (id != request.Id)
                    return Results.BadRequest("Route ID mismatch.");

                Result result = await sender.Send(request);
                return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result);
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Update a discipline rule.")
            .WithTags(Tags.Discipline)
            .RequireAuthorization();
        }
    }
}
