using Microsoft.EntityFrameworkCore;
using SabemiWebhooks.Data;
using SabemiWebhooks.Endpoints;
using SabemiWebhooks.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// Provider selecionável via configuração: Postgres (padrão, conforme especificação)
// ou Sqlite (fallback sem infraestrutura — útil para rodar sem Docker)
var provider = builder.Configuration["Database:Provider"] ?? "Postgres";
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        o.UseSqlite(builder.Configuration.GetConnectionString("Sqlite"));
    else
        o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
});

builder.Services.AddScoped<PaymentService>();
builder.Services.AddSingleton<PaymentQueue>();
builder.Services.AddHostedService<PaymentProcessingWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

app.UseCors();
app.MapWebhookEndpoints();

app.Run();

public partial class Program { } // expõe o entry point para os testes de integração
