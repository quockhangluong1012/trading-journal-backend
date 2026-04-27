using Microsoft.AspNetCore.SignalR;
using TradingJournal.Modules.Notifications.Dto;
using TradingJournal.Modules.Notifications.Hubs;

namespace TradingJournal.Modules.Notifications.Features.V1;

public sealed class MarkAsRead
{
    public record Request(int NotificationId) : ICommand<Result<bool>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(
        INotificationDbContext context,
        IHubContext<NotificationHub> hubContext)
        : ICommandHandler<Request, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Request request, CancellationToken cancellationToken)
        {
            Notification? notification = await context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.Id == request.NotificationId &&
                    n.UserId == request.UserId &&
                    !n.IsDisabled, cancellationToken);

            if (notification is null)
            {
                return Result<bool>.Failure(Error.NotFound);
            }

            if (notification.IsRead)
            {
                return Result<bool>.Success(true);
            }

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            // Push updated unread count to user
            int unreadCount = await context.Notifications
                .CountAsync(n => n.UserId == request.UserId && !n.IsRead && !n.IsDisabled, cancellationToken);

            string groupName = $"user-{request.UserId}";
            await hubContext.Clients.Group(groupName)
                .SendAsync("NotificationRead", new { Id = request.NotificationId }, cancellationToken);
            await hubContext.Clients.Group(groupName)
                .SendAsync("UnreadCountChanged", new UnreadCountDto(unreadCount), cancellationToken);

            return Result<bool>.Success(true);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Notifications);

            group.MapPut("/{id:int}/read", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result<bool> result = await sender.Send(
                    new Request(id) { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .WithSummary("Mark a notification as read.")
            .WithTags(Tags.Notifications)
            .RequireAuthorization();
        }
    }
}
