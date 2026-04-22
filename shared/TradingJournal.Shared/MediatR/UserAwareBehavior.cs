using MediatR;
using TradingJournal.Shared.CQRS;
using TradingJournal.Shared.Security;

namespace TradingJournal.Shared.MediatR;

public sealed class UserAwareBehavior<TRequest, TResponse>(IUserContext userContext) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is IUserAwareRequest userAwareRequest)
        {
            userAwareRequest.UserId = userContext.UserId;
        }

        return await next(cancellationToken);
    }
}
