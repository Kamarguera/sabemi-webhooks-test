using System.Threading.Channels;

namespace SabemiWebhooks.Services;

/// <summary>Job enviado ao worker em background após a gravação do evento bruto.</summary>
public record ProcessPaymentJob(Guid EventId);

/// <summary>
/// Fila em memória (Channel) entre o endpoint e o worker.
/// Permite que o endpoint responda 202 imediatamente enquanto o
/// processamento pesado acontece em background, com shutdown gracioso.
/// </summary>
public class PaymentQueue
{
    private readonly Channel<ProcessPaymentJob> _channel =
        Channel.CreateUnbounded<ProcessPaymentJob>();

    public ValueTask EnqueueAsync(ProcessPaymentJob job) =>
        _channel.Writer.WriteAsync(job);

    public IAsyncEnumerable<ProcessPaymentJob> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
