using Mapster;

namespace TradingJournal.Modules.Trades.Features.V1.Discipline;

public sealed class CreateDisciplineRule
{
    public sealed record Request(
        string Name,
        string? Description,
        LessonCategory Category,
        int SortOrder) : ICommand<Result<int>>;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Rule name is required.")
                .MaximumLength(200).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Rule name must not exceed 200 characters.");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Description must not exceed 1000 characters.");

            RuleFor(x => x.Category)
                .Must(Enum.IsDefined).WithErrorCode(nameof(HttpStatusCode.BadRequest))
                .WithMessage("Category must be a valid value.");
        }
    }

    public sealed class Handler(ITradeDbContext context) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            DisciplineRule rule = new()
            {
                Id = 0,
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                SortOrder = request.SortOrder,
                IsActive = true
            };

            await context.DisciplineRules.AddAsync(rule, cancellationToken);
            int rows = await context.SaveChangesAsync(cancellationToken);

            return rows > 0
                ? Result<int>.Success(rule.Id)
                : Result<int>.Failure(Error.Create("Failed to create discipline rule."));
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Discipline);

            group.MapPost("/rules", async ([FromBody] Request request, ISender sender) =>
            {
                Result<int> result = await sender.Send(request);
                return result.IsSuccess
                    ? Results.Created($"{ApiGroup.V1.Discipline}/rules/{result.Value}", result)
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Create a discipline rule.")
            .WithTags(Tags.Discipline)
            .RequireAuthorization();
        }
    }
}
