namespace TradingJournal.Shared.CQRS;

/// <summary>
/// Marker interface for MediatR requests that require a UserId.
/// Implement this on any Request/Command/Query that needs the current user injected.
/// The <see cref="MediatR.UserAwareBehavior{TRequest,TResponse}"/> will automatically
/// set the UserId from the authenticated user context.
/// </summary>
public interface IUserAwareRequest
{
    int UserId { get; set; }
}
