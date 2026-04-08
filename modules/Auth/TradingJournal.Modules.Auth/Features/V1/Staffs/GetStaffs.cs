 
using TradingJournal.Modules.Auth.Features.V1.Staffs.Models;

namespace TradingJournal.Modules.Auth.Features.V1.Staffs;

public sealed class GetStaffs
{
    internal sealed record Request() : IQuery<Result<IReadOnlyCollection<StaffDto>>>;

    internal sealed class Handler(IAuthDbContext context) : IQueryHandler<Request, Result<IReadOnlyCollection<StaffDto>>>
    {
        public async Task<Result<IReadOnlyCollection<StaffDto>>> Handle(Request request, CancellationToken cancellationToken)
        {
            var staffs = await context.Staffs.ToListAsync(cancellationToken);
            var dtos = staffs.Select(s => new StaffDto(s.Id, s.Email, s.FullName, s.IsActive, s.CreatedDate)).ToList();
            return Result<IReadOnlyCollection<StaffDto>>.Success(dtos);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup($"{AuthConstants.Endpoints.AuthBase}/staffs");

            group.MapGet("/", async (ISender sender) =>
            {
                var result = await sender.Send(new Request());
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<IReadOnlyCollection<StaffDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get all staffs.")
            .WithTags("Staffs")
            .RequireAuthorization("AdminOnly");
        }
    }
}
