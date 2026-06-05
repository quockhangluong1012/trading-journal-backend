using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Events;

internal sealed class IntegrationEventProcessorJob(
    InMemoryMessageQueue queue,
    IServiceScopeFactory scopeFactory,
    IDeadLetterSink deadLetterSink,
    ILogger<IntegrationEventProcessorJob> logger) : BackgroundService
{
    // Transient handler failures (e.g. a brief DB hiccup) are retried with exponential backoff
    // before the event is dead-lettered, so a single failure no longer silently drops the event.
    private const int MaxAttempts = 3;
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMilliseconds(200);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Integration Event Processor Job started.");

        try
        {
            await foreach (IIntegrationEvent integrationEvent in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessEventAsync(integrationEvent, stoppingToken);
            }
        }
        catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Integration Event Processor Job was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "An error occurred while processing integration events in the Integration Event Processor Job.");
        }

        logger.LogInformation("Integration Event Processor Job stopped.");
    }

    private async Task ProcessEventAsync(IIntegrationEvent integrationEvent, CancellationToken stoppingToken)
    {
        string eventType = integrationEvent.GetType().Name;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                logger.LogInformation(
                    "Publishing {IntegrationEventType} with EventId {IntegrationEventId} (attempt {Attempt}/{MaxAttempts}).",
                    eventType, integrationEvent.EventId, attempt, MaxAttempts);

                // Fresh scope per attempt so a failed/aborted DbContext is never reused on retry.
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IPublisher publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

                await publisher.Publish(integrationEvent, stoppingToken);

                logger.LogInformation("Successfully published {IntegrationEventType} with EventId {IntegrationEventId}.",
                    eventType, integrationEvent.EventId);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                if (attempt == MaxAttempts)
                {
                    logger.LogError(ex,
                        "Exhausted {MaxAttempts} attempts publishing {IntegrationEventType} with EventId {IntegrationEventId}; dead-lettering.",
                        MaxAttempts, eventType, integrationEvent.EventId);

                    try
                    {
                        await deadLetterSink.SendAsync(integrationEvent, ex, attempt, stoppingToken);
                    }
                    catch (Exception sinkEx)
                    {
                        logger.LogError(sinkEx,
                            "Dead-letter sink failed for {IntegrationEventType} with EventId {IntegrationEventId}.",
                            eventType, integrationEvent.EventId);
                    }

                    return;
                }

                TimeSpan delay = BaseRetryDelay * Math.Pow(2, attempt - 1);
                logger.LogWarning(ex,
                    "Attempt {Attempt}/{MaxAttempts} failed publishing {IntegrationEventType} with EventId {IntegrationEventId}; retrying in {Delay}.",
                    attempt, MaxAttempts, eventType, integrationEvent.EventId, delay);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
