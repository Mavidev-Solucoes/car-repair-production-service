namespace Infrastructure.Messaging;

public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public bool Enabled { get; init; }

    public string HostName { get; init; } = "localhost";

    public int Port { get; init; } = 5672;

    public string UserName { get; init; } = "guest";

    public string Password { get; init; } = "guest";

    public string VirtualHost { get; init; } = "/";

    /// <summary>
    /// Exchange onde o serviço publica seus eventos.
    /// </summary>
    public string ExchangeName { get; init; } = "car-repair.events";

    /// <summary>
    /// Exchange de onde o serviço consome os comandos da saga.
    /// </summary>
    public string CommandsExchangeName { get; init; } = "car-repair.commands";

    public string DeadLetterExchangeName { get; init; } = "car-repair.commands.dlx";

    public int RetryCount { get; init; } = 3;

    public int RetryDelayMilliseconds { get; init; } = 5000;

    public ushort PrefetchCount { get; init; } = 10;

    public bool Durable { get; init; } = true;
}
