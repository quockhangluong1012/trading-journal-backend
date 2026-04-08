 
using TradingJournal.Modules.Auth.Features.V1.Staffs.Models;

namespace TradingJournal.Modules.Auth.Features.V1.Staffs;

public sealed class GetStaffDetail
{
    internal sealed record Request(int Id) : IQuery<Result<StaffDto>>;

    internal sealed class Handler(IAuthDbContext context) : IQueryHandler<Request, Result<StaffDto>>
    {
        public async Task<Result<StaffDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            var staff = await context.Staffs.FindAsync([request.Id], cancellationToken);
            if (staff == null)
                return Result<StaffDto>.Failure(Error.Create("Staff not found."));

            var dto = new StaffDto(staff.Id, staff.Email, staff.FullName, staff.IsActive, staff.CreatedDate);
            return Result<StaffDto>.Success(dto);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup($"{AuthConstants.Endpoints.AuthBase}/staffs");

            group.MapGet("/{id:int}", async (int id, ISender sender) =>
            {
                var result = await sender.Send(new Request(id));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<StaffDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Get staff detail by ID.")
            .WithTags("Staffs")
            .RequireAuthorization("AdminOnly");
        }
    }
}
