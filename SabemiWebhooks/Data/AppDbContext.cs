using Microsoft.EntityFrameworkCore;
using SabemiWebhooks.Models;

namespace SabemiWebhooks.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<WebhookEvent>   Events    => Set<WebhookEvent>();
    public DbSet<ContractStatus> Contracts => Set<ContractStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Garantia de idempotência no nível do banco: o mesmo id_transacao nunca é gravado duas vezes
        modelBuilder.Entity<WebhookEvent>().HasIndex(e => e.IdTransacao).IsUnique();
        modelBuilder.Entity<ContractStatus>().HasKey(e => e.IdContrato);
    }
}
