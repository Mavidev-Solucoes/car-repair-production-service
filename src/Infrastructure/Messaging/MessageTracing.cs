using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using NewRelic.Api.Agent;

namespace Infrastructure.Messaging;

/// <summary>
/// Trace distribuído pelo RabbitMQ (W3C Trace Context nos headers da mensagem).
/// Na publicação grava traceparent/tracestate do Activity atual e, com o agente do New Relic carregado,
/// os headers do trace dele (newrelic, traceparent, tracestate). No consumo, o handler roda numa transação
/// do New Relic que aceita esses headers e num Activity filho do traceparent recebido, então o próximo
/// comando ou evento publicado continua o mesmo trace. Sem o agente, as chamadas da API do New Relic não fazem nada.
/// </summary>
public static class MessageTracing
{
    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";

    public static void Inject(IDictionary<string, object> headers)
    {
        var activity = Activity.Current;
        if (activity is { IdFormat: ActivityIdFormat.W3C, Id: not null })
        {
            headers[TraceParentHeader] = activity.Id;
            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                headers[TraceStateHeader] = activity.TraceStateString;
            }
        }

        NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction
            .InsertDistributedTraceHeaders(headers, static (carrier, key, value) => carrier[key] = value);
    }

    [Transaction]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task ConsumeAsync(
        string messageType,
        string? correlationId,
        IDictionary<string, object>? headers,
        Func<Task> handle)
    {
        NewRelic.Api.Agent.NewRelic.SetTransactionName("RabbitMQ", messageType);
        var transaction = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;
        transaction.AcceptDistributedTraceHeaders(headers, static (carrier, key) => ReadValues(carrier, key), TransportType.AMQP);
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            transaction.AddCustomAttribute("correlationId", correlationId);
        }

        using var activity = new Activity($"RabbitMQ consume {messageType}");
        if (ActivityContext.TryParse(Read(headers, TraceParentHeader), Read(headers, TraceStateHeader), out var parent))
        {
            activity.SetParentId(parent.TraceId, parent.SpanId, parent.TraceFlags);
            activity.TraceStateString = parent.TraceState;
        }

        activity.SetTag("messaging.system", "rabbitmq");
        activity.SetTag("messaging.message.type", messageType);
        activity.SetTag("correlation.id", correlationId);
        activity.Start();
        try
        {
            await handle();
        }
        finally
        {
            activity.Stop();
        }
    }

    /// <summary>O RabbitMQ.Client entrega os headers string como byte[] no consumo.</summary>
    public static string? Read(IDictionary<string, object>? headers, string key)
    {
        if (headers is null || !headers.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => null
        };
    }

    private static IEnumerable<string> ReadValues(IDictionary<string, object>? headers, string key) =>
        Read(headers, key) is { } value ? [value] : [];
}
