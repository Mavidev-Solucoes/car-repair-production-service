using System.Text;
using System.Text.Json.Nodes;
using Application.Common.Messaging;
using Domain.Entities;

namespace Application.WorkOrders.Commands.CreateWorkOrder;

/// <summary>
/// Converte o CreateWorkOrderCommand da saga (payload = contexto da saga) no comando da aplicação.
/// Usa os itens da OS (vindos do ServiceOrderOpened) para descrever o serviço a executar.
/// </summary>
public static class CreateWorkOrderSagaCommandMapper
{
    public const string CommandType = "CreateWorkOrderCommand";

    public static CreateWorkOrderCommand Map(IntegrationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var serviceOrderId = message.GetServiceOrderId();

        return new CreateWorkOrderCommand(
            serviceOrderId,
            serviceOrderId,
            $"OS {serviceOrderId.ToString("N")[..8].ToUpperInvariant()}",
            BuildDescription(message.Payload?["items"] as JsonArray));
    }

    private static string? BuildDescription(JsonArray? items)
    {
        if (items is null || items.Count == 0)
        {
            return null;
        }

        var description = new StringBuilder();
        foreach (var item in items.OfType<JsonObject>())
        {
            var text = item["description"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var quantity = item["quantity"] is JsonValue quantityValue && quantityValue.TryGetValue<int>(out var parsed)
                ? parsed
                : 1;

            if (description.Length > 0)
            {
                description.Append("; ");
            }

            description.Append(quantity).Append("x ").Append(text);
        }

        if (description.Length == 0)
        {
            return null;
        }

        return description.Length <= ServiceJob.DescriptionMaxLength
            ? description.ToString()
            : description.ToString(0, ServiceJob.DescriptionMaxLength - 3) + "...";
    }
}
