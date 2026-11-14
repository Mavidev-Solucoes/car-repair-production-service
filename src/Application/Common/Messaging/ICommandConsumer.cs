namespace Application.Common.Messaging;

public interface ICommandConsumer
{
    Task SubscribeAsync(
        string queueName,
        string commandType,
        Func<IntegrationMessage, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken = default);
}
