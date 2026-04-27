using TradingJournal.Modules.Notifications.Dto;

namespace TradingJournal.Modules.Notifications.Features.V1;

public sealed class GetNotifications
{
    public record Request() : IQuery<Result<List<NotificationDto>>>
    {
        public int UserId { get; set; }
        public bool UnreadOnly { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    internal sealed class Handler(INotificationDbContext context)
        : IQueryHandler<Request, Result<List<NotificationDto>>>
    {
        public async Task<Result<List<NotificationDto>>> Handle(Request request, CancellationToken cancellationToken)
        {
            IQueryable<Notification> query = context.Notifications
                .Where(n => n.UserId == request.UserId && !n.IsDisabled);

            if (request.UnreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            List<NotificationDto> notifications = await query
                .OrderByDescending(n => n.CreatedDate)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(n => new NotificationDto(
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Type.ToString(),
                    n.Priority.ToString(),
                    n.IsRead,
                    n.ReadAt,
                    n.Metadata,
                    n.ActionUrl,
                    n.CreatedDate))
                .ToListAsync(cancellationToken);

            return Result<List<NotificationDto>>.Success(notifications);
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Notifications);

            group.MapGet("/", async (
                ClaimsPrincipal user,
                ISender sender,
                bool? unreadOnly,
                int? page,
                int? pageSize) =>
            {
                var request = new Request
                {
                    UserId = user.GetCurrentUserId(),
                    UnreadOnly = unreadOnly ?? false,
                    Page = Math.Max(1, page ?? 1),
                    PageSize = Math.Clamp(pageSize ?? 20, 1, 50)
                };

                Result<List<NotificationDto>> result = await sender.Send(request);

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<List<NotificationDto>>>(StatusCodes.Status200OK)
            .WithSummary("Get notifications for the current user.")
            .WithTags(Tags.Notifications)
            .RequireAuthorization();
        }
    }
}
