using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace TradingJournal.Shared.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : notnull
{
    private const int SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Stopwatch timer = Stopwatch.StartNew();

        TResponse response = await next(cancellationToken);

        timer.Stop();

        long elapsed = timer.ElapsedMilliseconds;
        string requestName = typeof(TRequest).Name;

        if (elapsed > SlowRequestThresholdMs)
        {
            logger.LogWarning("[PERFORMANCE] Slow request {Request} took {TimeTaken}ms.", requestName, elapsed);
        }
        else
        {
            logger.LogDebug("[PERFORMANCE] {Request} completed in {TimeTaken}ms.", requestName, elapsed);
        }

        return response;
    }
}