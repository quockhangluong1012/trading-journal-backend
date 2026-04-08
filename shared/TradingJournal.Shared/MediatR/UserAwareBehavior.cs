using System.Reflection;
using MediatR;
using TradingJournal.Shared.Security;

namespace TradingJournal.Shared.MediatR;

public sealed class UserAwareBehavior<TRequest, TResponse>(IUserContext userContext) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        PropertyInfo? userIdProperty = typeof(TRequest).GetProperty("UserId");
        
        if (userIdProperty != null && userIdProperty.CanWrite && userIdProperty.PropertyType == typeof(int))
        {
            userIdProperty.SetValue(request, userContext.UserId);
        }

        return await next(cancellationToken);
    }
}
