using TradingJournal.Modules.Notifications.Dto;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Shared.Contracts;

namespace TradingJournal.Modules.Notifications.Features.V1;

public sealed class GetUnreadCount
{
    public record Request() : IQuery<Result<UnreadCountDto>>
    {
        public int UserId { get; set; }
    }

    internal sealed class Handler(INotificationDbContext context, ICacheRepository cacheRepository)
        : IQueryHandler<Request, Result<UnreadCountDto>>
    {
        public async Task<Result<UnreadCountDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            UnreadCountDto dto = await cacheRepository.GetOrCreateAsync<UnreadCountDto>(
                CacheKeys.UnreadCountForUser(request.UserId),
                async ct =>
                {
                    int count = await context.Notifications
                        .CountAsync(n => n.UserId == request.UserId && !n.IsRead && !n.IsDisabled, ct);
                    return new UnreadCountDto(count);
                },
                expiration: TimeSpan.FromSeconds(30),
                cancellationToken: cancellationToken) ?? new UnreadCountDto(0);

            return Result<UnreadCountDto>.Success(dto);
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
