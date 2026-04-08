using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace TradingJournal.Shared.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Stopwatch timer = new();
        timer.Start();

        TResponse response = await next(cancellationToken);

        timer.Stop();
        
        logger.LogWarning("[PERFORMANCE] The request {Request} took {TimeTaken} milliseconds.",
            typeof(TRequest).Name, timer.ElapsedMilliseconds);

        return response;
    }
}