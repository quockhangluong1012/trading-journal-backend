using TradingJournal.Modules.Scanner.Dto;

namespace TradingJournal.Modules.Scanner.Features.V1.Watchlists;

public sealed class CreateWatchlist
{
    public record Command() : ICommand<Result<WatchlistDto>>
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<CreateWatchlistAssetRequest> Assets { get; set; } = [];
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Assets).NotEmpty().WithMessage("At least one asset is required.");
            RuleForEach(x => x.Assets).ChildRules(a =>
            {
                a.RuleFor(x => x.Symbol).NotEmpty().MaximumLength(30);
                a.RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
            });
        }
    }

    internal sealed class Handler(IScannerDbContext context)
        : ICommandHandler<Command, Result<WatchlistDto>>
    {
        public async Task<Result<WatchlistDto>> Handle(Command request, CancellationToken cancellationToken)
        {
            var watchlist = new Watchlist
            {
                Id = default!,
                Name = request.Name,
                UserId = request.UserId,
                IsActive = true,
                Assets = request.Assets.Select(a => new WatchlistAsset
                {
                    Id = default!,
                    Symbol = a.Symbol.ToUpperInvariant(),
                    DisplayName = a.DisplayName
                }).ToList()
            };

            context.Watchlists.Add(watchlist);
            await context.SaveChangesAsync(cancellationToken);

            var dto = new WatchlistDto(
                watchlist.Id,
                watchlist.Name,
                watchlist.IsActive,
                watchlist.CreatedDate,
                watchlist.Assets.Select(a => new WatchlistAssetDto(a.Id, a.Symbol, a.DisplayName, new List<string>())).ToList());

            return Result<WatchlistDto>.Success(dto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Watchlists);

            group.MapPost("/", async (ClaimsPrincipal user, ISender sender, CreateWatchlistRequest body) =>
            {
                var command = new Command
                {
                    UserId = user.GetCurrentUserId(),
                    Name = body.Name,
                    Assets = body.Assets
                };

                Result<WatchlistDto> result = await sender.Send(command);
                return result.IsSuccess ? Results.Created($"/api/v1/scanner/watchlists/{result.Value.Id}", result) : Results.BadRequest(result);
            })
            .Produces<Result<WatchlistDto>>(StatusCodes.Status201Created)
            .WithSummary("Create a new watchlist with assets.")
            .WithTags(Tags.Watchlists)
            .RequireAuthorization();
        }
    }
}
