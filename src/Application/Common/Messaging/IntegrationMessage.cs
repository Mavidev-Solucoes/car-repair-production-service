using System.Text.Json;
using System.Text.Json.Nodes;

namespace Application.Common.Messaging;

/// <summary>
/// Envelope único de eventos e comandos, conforme docs/contrato-mensageria.md.
/// </summary>
public sealed class IntegrationMessage
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Guid MessageId { get; init; } = Guid.NewGuid();

    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Sempre o id da ordem de serviço.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public JsonObject? Payload { get; init; }

    public Guid GetServiceOrderId()
    {
        return Guid.TryParse(CorrelationId, out var serviceOrderId) && serviceOrderId != Guid.Empty
            ? serviceOrderId
            : throw new InvalidMessageException($"Message {Type} has an invalid correlationId '{CorrelationId}'.");
    }
}

/// <summary>
/// Mensagem que nunca vai ser processada com sucesso: vai direto para a DLQ, sem retry.
/// </summary>
public sealed class InvalidMessageException(string message) : Exception(message);
