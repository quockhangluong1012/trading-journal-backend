using Microsoft.AspNetCore.SignalR;
using TradingJournal.Modules.Notifications.Dto;
using TradingJournal.Modules.Notifications.Hubs;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Shared.Contracts;

namespace TradingJournal.Modules.Notifications.Features.V1;

public sealed class DeleteNotification
{
    public record Request(int NotificationId) : ICommand<Result<bool>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(
        INotificationDbContext context,
        IHubContext<NotificationHub> hubContext,
        ICacheRepository cacheRepository)
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

            // Soft-delete using the existing IsDisabled field from EntityBase
            notification.IsDisabled = true;
            notification.UpdatedDate = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            // Push updated unread count if the deleted notification was unread
            if (!notification.IsRead)
            {
                await cacheRepository.RemoveCache(CacheKeys.UnreadCountForUser(request.UserId), cancellationToken);

                int unreadCount = await context.Notifications
                    .CountAsync(n => n.UserId == request.UserId && !n.IsRead && !n.IsDisabled, cancellationToken);

                await hubContext.Clients.Group($"user-{request.UserId}")
                    .SendAsync("UnreadCountChanged", new UnreadCountDto(unreadCount), cancellationToken);
            }

            return Result<bool>.Success(true);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Notifications);

            group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, ISender sender) =>
            {
                Result<bool> result = await sender.Send(
                    new Request(id) { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .Produces<Result<bool>>(StatusCodes.Status200OK)
            .WithSummary("Soft-delete a notification.")
            .WithTags(Tags.Notifications)
            .RequireAuthorization();
        }
    }
}
