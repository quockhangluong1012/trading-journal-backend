using TradingJournal.Modules.Notifications.Dto;

namespace TradingJournal.Modules.Notifications.Features.V1;

public sealed class GetUnreadCount
{
    public record Request() : IQuery<Result<UnreadCountDto>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(INotificationDbContext context)
        : IQueryHandler<Request, Result<UnreadCountDto>>
    {
        public async Task<Result<UnreadCountDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            int count = await context.Notifications
                .CountAsync(n => n.UserId == request.UserId && !n.IsRead && !n.IsDisabled, cancellationToken);

            return Result<UnreadCountDto>.Success(new UnreadCountDto(count));
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Notifications);

            group.MapGet("/unread-count", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<UnreadCountDto> result = await sender.Send(
                    new Request { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<UnreadCountDto>>(StatusCodes.Status200OK)
            .WithSummary("Get the unread notification count for the current user.")
            .WithTags(Tags.Notifications)
            .RequireAuthorization();
        }
    }
}
