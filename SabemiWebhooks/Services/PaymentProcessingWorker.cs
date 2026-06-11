using SabemiWebhooks.Data;
using SabemiWebhooks.Models;

namespace SabemiWebhooks.Services;

/// <summary>
/// Worker em background (IHostedService) que consome a fila e aplica a regra
/// de negócio pesada: atualiza o status consolidado do contrato e marca o
/// evento como processado. Falhas são registradas no próprio evento (campo Erro).
/// </summary>
public class PaymentProcessingWorker(
    PaymentQueue queue,
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<PaymentProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delayMs = config.GetValue("ProcessingDelayMs", 2000);

        await foreach (var job in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Simula regra de negócio pesada (requisito do teste)
                await Task.Delay(delayMs, stoppingToken);
                await ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao processar evento {EventId}", job.EventId);
                await RegisterErrorAsync(job, ex);
            }
        }
    }

    private async Task ProcessAsync(ProcessPaymentJob job, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var evt = await db.Events.FindAsync([job.EventId], ct);
        if (evt is null) return;

        var contrato = await db.Contracts.FindAsync([evt.IdContrato], ct);
        if (contrato is null)
        {
            db.Contracts.Add(new ContractStatus
            {
                IdContrato   = evt.IdContrato,
                UltimoStatus = evt.Status,
                Atualizado   = DateTime.UtcNow,
                Total        = 1,
                ValorTotal   = evt.Valor
            });
        }
        else
        {
            contrato.UltimoStatus = evt.Status;
            contrato.Atualizado   = DateTime.UtcNow;
            contrato.Total++;
            contrato.ValorTotal  += evt.Valor;
        }

        evt.Processado = true;
        await db.SaveChangesAsync(ct);
    }

    private async Task RegisterErrorAsync(ProcessPaymentJob job, Exception ex)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var evt = await db.Events.FindAsync(job.EventId);
        if (evt is null) return;

        evt.Erro       = ex.Message;
        evt.Processado = true;
        await db.SaveChangesAsync();
    }
}
