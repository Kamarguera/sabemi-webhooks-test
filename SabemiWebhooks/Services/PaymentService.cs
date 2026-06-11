using Microsoft.EntityFrameworkCore;
using SabemiWebhooks.Contracts;
using SabemiWebhooks.Data;
using SabemiWebhooks.Models;

namespace SabemiWebhooks.Services;

public enum IntakeStatus { Accepted, Duplicate, Invalid }

public record IntakeResult(IntakeStatus Status, string? Error = null);

/// <summary>
/// Camada de negócio: valida o payload, garante idempotência,
/// grava o evento bruto e enfileira o processamento em background.
/// </summary>
public class PaymentService(AppDbContext db, PaymentQueue queue)
{
    public async Task<IntakeResult> ReceiveAsync(PaymentRequest payload)
    {
        if (string.IsNullOrWhiteSpace(payload.IdTransacao) ||
            string.IsNullOrWhiteSpace(payload.IdContrato))
            return new(IntakeStatus.Invalid, "id_transacao e id_contrato são obrigatórios.");

        if (payload.Valor <= 0)
            return new(IntakeStatus.Invalid, "valor deve ser maior que zero.");

        // Caminho rápido: evita gravação se o id_transacao já existe
        if (await db.Events.AnyAsync(e => e.IdTransacao == payload.IdTransacao))
            return new(IntakeStatus.Duplicate);

        var evt = new WebhookEvent
        {
            Id            = Guid.NewGuid(),
            IdTransacao   = payload.IdTransacao,
            IdContrato    = payload.IdContrato,
            Valor         = payload.Valor,
            DataPagamento = AsUtc(payload.DataPagamento),
            Status        = payload.Status,
            RecebidoEm    = DateTime.UtcNow
        };
        db.Events.Add(evt);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Corrida entre requisições simultâneas: o índice único do banco
            // é a garantia final — a segunda gravação falha e tratamos como duplicada
            return new(IntakeStatus.Duplicate);
        }

        await queue.EnqueueAsync(new ProcessPaymentJob(evt.Id));
        return new(IntakeStatus.Accepted);
    }

    /// <summary>PostgreSQL (timestamptz) exige DateTime com Kind=Utc; normaliza o valor recebido.</summary>
    private static DateTime AsUtc(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Utc   => dt,
        DateTimeKind.Local => dt.ToUniversalTime(),
        _                  => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
    };

    /// <summary>Distingue violação de unicidade (duplicata real) de outras falhas de gravação.</summary>
    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException switch
    {
        Npgsql.PostgresException pg            => pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation,
        Microsoft.Data.Sqlite.SqliteException sq => sq.SqliteErrorCode == 19,
        _                                       => false
    };

    public async Task<List<WebhookEvent>> ListAsync(string? status, string? idContrato)
    {
        var query = db.Events.AsQueryable();
        if (!string.IsNullOrEmpty(status))     query = query.Where(e => e.Status == status);
        if (!string.IsNullOrEmpty(idContrato)) query = query.Where(e => e.IdContrato == idContrato);
        return await query.OrderByDescending(e => e.RecebidoEm).ToListAsync();
    }
}
