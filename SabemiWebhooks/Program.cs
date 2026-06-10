using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

app.UseCors();

var apiKey = app.Configuration["ApiKey"]!;

app.MapPost("/webhooks/pagamento", async (HttpRequest req, AppDbContext db, PaymentRequest payload) =>
{
    if (!req.Headers.TryGetValue("X-Api-Key", out var key) || key != apiKey)
        return Results.Unauthorized();

    if (await db.Events.AnyAsync(e => e.IdTransacao == payload.IdTransacao))
        return Results.Ok(new { message = "Duplicado — ignorado." });

    var evt = new WebhookEvent
    {
        Id = Guid.NewGuid(),
        IdTransacao = payload.IdTransacao,
        IdContrato  = payload.IdContrato,
        Valor        = payload.Valor,
        DataPagamento = payload.DataPagamento,
        Status       = payload.Status,
        RecebidoEm   = DateTime.UtcNow
    };
    db.Events.Add(evt);
    await db.SaveChangesAsync();

    var appRef = app;
    _ = Task.Run(async () =>
    {
        await Task.Delay(2000);
        using var scope = appRef.Services.CreateScope();
        var bg = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            var e = await bg.Events.FindAsync(evt.Id);
            var c = await bg.Contracts.FindAsync(payload.IdContrato);
            if (c == null)
                bg.Contracts.Add(new ContractStatus
                {
                    IdContrato = payload.IdContrato,
                    UltimoStatus = payload.Status,
                    Atualizado = DateTime.UtcNow,
                    Total = 1,
                    ValorTotal = payload.Valor
                });
            else
            {
                c.UltimoStatus = payload.Status;
                c.Atualizado = DateTime.UtcNow;
                c.Total++;
                c.ValorTotal += payload.Valor;
            }
            e!.Processado = true;
            await bg.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            using var s2 = appRef.Services.CreateScope();
            var db2 = s2.ServiceProvider.GetRequiredService<AppDbContext>();
            var e = await db2.Events.FindAsync(evt.Id);
            if (e != null) { e.Erro = ex.Message; e.Processado = true; await db2.SaveChangesAsync(); }
        }
    });

    return Results.Accepted(null, new { message = "Recebido. Processando em background." });
});

app.MapGet("/webhooks/pagamentos", async (AppDbContext db, string? status, string? idContrato) =>
{
    var q = db.Events.AsQueryable();
    if (!string.IsNullOrEmpty(status))    q = q.Where(e => e.Status == status);
    if (!string.IsNullOrEmpty(idContrato)) q = q.Where(e => e.IdContrato == idContrato);
    return Results.Ok(await q.OrderByDescending(e => e.RecebidoEm).ToListAsync());
});

app.Run();

// ── Models ──────────────────────────────────────────

public record PaymentRequest(string IdTransacao, string IdContrato,
    decimal Valor, DateTime DataPagamento, string Status);

public class WebhookEvent
{
    public Guid     Id            { get; set; }
    public string   IdTransacao   { get; set; } = "";
    public string   IdContrato    { get; set; } = "";
    public decimal  Valor         { get; set; }
    public DateTime DataPagamento { get; set; }
    public string   Status        { get; set; } = "";
    public DateTime RecebidoEm   { get; set; }
    public bool     Processado    { get; set; }
    public string?  Erro          { get; set; }
}

public class ContractStatus
{
    public string   IdContrato  { get; set; } = "";
    public string   UltimoStatus { get; set; } = "";
    public DateTime Atualizado  { get; set; }
    public int      Total       { get; set; }
    public decimal  ValorTotal  { get; set; }
}

public class AppDbContext(DbContextOptions<AppDbContext> o) : DbContext(o)
{
    public DbSet<WebhookEvent>   Events    => Set<WebhookEvent>();
    public DbSet<ContractStatus> Contracts => Set<ContractStatus>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<WebhookEvent>().HasIndex(e => e.IdTransacao).IsUnique();
        m.Entity<ContractStatus>().HasKey(e => e.IdContrato);
    }
}