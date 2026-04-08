using Microsoft.AspNetCore.Hosting;

namespace TradingJournal.Modules.Trades.Features.V1.Trade;

public class DeleteTrade
{
    public class Request : ICommand<Result<int>>
    {
        public int Id { get; set; }
        public int UserId { get; set; }
    }
    
    public class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Trade ID must be greater than 0.");
        }
    }

    public class Handler(ITradeDbContext tradeDbContext, IWebHostEnvironment env) : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            Domain.TradeHistory? trade = await tradeDbContext.TradeHistories
                .Include(x => x.TradeScreenShots)
                .Include(x => x.TradeChecklists)
                .Include(x => x.TradeTechnicalAnalysisTags)
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.CreatedBy == request.UserId, cancellationToken);

            if (trade == null)
            {
                return Result<int>.Failure(Error.NotFound);
            }

            // Delete physical screenshot files from disk
            foreach (var screenshot in trade.TradeScreenShots)
            {
                DeleteScreenshotFile(screenshot.Url);
            }

            tradeDbContext.TradeHistories.Remove(trade);

            await tradeDbContext.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(trade.Id);
        }

        private void DeleteScreenshotFile(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("/screenshots/"))
                return;

            // https://{host}/screenshots/c5cb241a-d470-4d5e-8ec9-14d47e23e0e8.png

            List<string> parts = [.. url.Split("/screenshots/")];

            if (parts.Count < 2)
            {
                return;
            }

            string fileName = parts[1];

            var filePath = Path.Combine(env.ContentRootPath, "wwwroot", "screenshots", fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeHistory);

            group.MapDelete("/{id}", async ([FromRoute] int id, ISender sender) => {
                Result<int> result = await sender.Send(new Request { Id = id });

                return result.IsSuccess ? Results.Ok(result) 
                    : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Delete a trade history by ID.")
            .WithDescription("Deletes a trade history by its ID.") 
            .WithTags(Tags.TradeHistory)
            .RequireAuthorization();
        }
    }
}