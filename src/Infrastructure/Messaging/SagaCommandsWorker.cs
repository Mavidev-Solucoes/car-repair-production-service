using Application.Common.Messaging;
using Application.WorkOrders.Commands.CreateWorkOrder;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

/// <summary>
/// Assina os comandos da saga destinados ao production-service (docs/contrato-mensageria.md).
/// </summary>
public sealed class SagaCommandsWorker(
    ICommandConsumer commandConsumer,
    IServiceScopeFactory scopeFactory,
    ILogger<SagaCommandsWorker> logger) : BackgroundService
{
    public const string QueuePrefix = "production-service.";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await commandConsumer.SubscribeAsync(
                    QueuePrefix + CreateWorkOrderSagaCommandMapper.CommandType,
                    CreateWorkOrderSagaCommandMapper.CommandType,
                    HandleCreateWorkOrderAsync,
                    stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not subscribe to saga commands. Retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public async Task HandleCreateWorkOrderAsync(IntegrationMessage message, CancellationToken cancellationToken)
    {
        var command = CreateWorkOrderSagaCommandMapper.Map(message);

        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        Guid workOrderId;
        try
        {
            workOrderId = await sender.Send(command, cancellationToken);
        }
        catch (FluentValidation.ValidationException exception)
        {
            // Payload inválido não melhora com retry: vai direto para a DLQ.
            throw new InvalidMessageException(exception.Message);
        }

        logger.LogInformation(
            "Work order {WorkOrderId} ready for service order {ServiceOrderId}.",
            workOrderId,
            command.ServiceOrderId);
    }
}
