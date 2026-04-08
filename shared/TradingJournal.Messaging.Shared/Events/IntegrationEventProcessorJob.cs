using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Events;

internal sealed class IntegrationEventProcessorJob(InMemoryMessageQueue queue,
    IPublisher publisher,
    ILogger<IntegrationEventProcessorJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Integration Event Processor Job started.");

        try
        {
            await foreach (IIntegrationEvent integrationEvent in queue.Reader.ReadAllAsync(stoppingToken))
            {
                logger.LogInformation("Integration Event Processor Job started publishing {IntegrationEventType} with EventId: {IntegrationEventId}.",
                    integrationEvent.GetType().Name, integrationEvent.EventId);

                try
                {
                    await publisher.Publish(integrationEvent, stoppingToken);
                    logger.LogInformation("Successfully published {IntegrationEventType} with EventId: {IntegrationEventId}.",
                        integrationEvent.GetType().Name, integrationEvent.EventId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to publish {IntegrationEventType} with EventId: {IntegrationEventId}.",
                        integrationEvent.GetType().Name, integrationEvent.EventId);
                    // Continue processing other events even if one fails
                }
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
}