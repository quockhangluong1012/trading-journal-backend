using Microsoft.AspNetCore.SignalR;
using TradingJournal.Modules.Notifications.Dto;
using TradingJournal.Modules.Notifications.Hubs;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Shared.Contracts;

namespace TradingJournal.Modules.Notifications.Features.V1;

public sealed class MarkAllAsRead
{
    public record Request() : ICommand<Result<int>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(
        INotificationDbContext context,
        IHubContext<NotificationHub> hubContext,
        ICacheRepository cacheRepository)
        : ICommandHandler<Request, Result<int>>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken cancellationToken)
        {
            int updated = await context.Notifications
                .Where(n => n.UserId == request.UserId && !n.IsRead && !n.IsDisabled)
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, DateTime.UtcNow),
                    cancellationToken);

            await cacheRepository.RemoveCache(CacheKeys.UnreadCountForUser(request.UserId), cancellationToken);

            // Push zero unread count to all user connections
            string groupName = $"user-{request.UserId}";
            await hubContext.Clients.Group(groupName)
                .SendAsync("UnreadCountChanged", new UnreadCountDto(0), cancellationToken);

            return Result<int>.Success(updated);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Notifications);

            group.MapPut("/read-all", async (ClaimsPrincipal user, ISender sender) =>
            {
                Result<int> result = await sender.Send(
                    new Request { UserId = user.GetCurrentUserId() });

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<int>>(StatusCodes.Status200OK)
            .WithSummary("Mark all notifications as read. Returns the number of notifications updated.")
            .WithTags(Tags.Notifications)
            .RequireAuthorization();
        }
    }
}
