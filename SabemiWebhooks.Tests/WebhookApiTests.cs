using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SabemiWebhooks.Tests;

/// <summary>
/// Fábrica de testes: sobe a API real em memória usando SQLite em arquivo
/// temporário (isolado por teste) e delay de processamento reduzido.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"sabemi_test_{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_dbPath}");
        builder.UseSetting("ProcessingDelayMs", "50");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (IOException) { }
    }
}

public class WebhookApiTests : IDisposable
{
    private const string ApiKey = "sabemi-secret-key";

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public WebhookApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private void UsarApiKey() =>
        _client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

    private static object Payload(
        string idTransacao = "TX1", string idContrato = "CT1",
        decimal valor = 100m, string status = "Sucesso") =>
        new { idTransacao, idContrato, valor, dataPagamento = "2026-06-10T00:00:00", status };

    private record EventoDto(
        Guid Id, string IdTransacao, string IdContrato, decimal Valor,
        DateTime DataPagamento, string Status, DateTime RecebidoEm,
        bool Processado, string? Erro);

    [Fact]
    public async Task Post_SemApiKey_Retorna401()
    {
        var resp = await _client.PostAsJsonAsync("/webhooks/pagamento", Payload());
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Post_ComApiKey_Retorna202()
    {
        UsarApiKey();
        var resp = await _client.PostAsJsonAsync("/webhooks/pagamento", Payload());
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact]
    public async Task Post_IdTransacaoDuplicado_NaoGravaDuasVezes()
    {
        UsarApiKey();
        await _client.PostAsJsonAsync("/webhooks/pagamento", Payload("TXDUP"));
        var segunda = await _client.PostAsJsonAsync("/webhooks/pagamento", Payload("TXDUP"));

        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);

        var eventos = await _client.GetFromJsonAsync<List<EventoDto>>("/webhooks/pagamentos");
        Assert.Single(eventos!);
    }

    [Fact]
    public async Task Post_ValorInvalido_Retorna400()
    {
        UsarApiKey();
        var resp = await _client.PostAsJsonAsync("/webhooks/pagamento", Payload(valor: -5m));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Get_FiltraPorStatusEPorContrato()
    {
        UsarApiKey();
        await _client.PostAsJsonAsync("/webhooks/pagamento", Payload("TX1", "CT1", status: "Sucesso"));
        await _client.PostAsJsonAsync("/webhooks/pagamento", Payload("TX2", "CT2", status: "Erro"));

        var soErro = await _client.GetFromJsonAsync<List<EventoDto>>("/webhooks/pagamentos?status=Erro");
        Assert.Single(soErro!);
        Assert.Equal("TX2", soErro![0].IdTransacao);

        var soContrato1 = await _client.GetFromJsonAsync<List<EventoDto>>("/webhooks/pagamentos?idContrato=CT1");
        Assert.Single(soContrato1!);
        Assert.Equal("CT1", soContrato1![0].IdContrato);
    }

    [Fact]
    public async Task Evento_EProcessadoEmBackground()
    {
        UsarApiKey();
        await _client.PostAsJsonAsync("/webhooks/pagamento", Payload("TXBG"));

        // O endpoint responde 202 antes do processamento; aguarda o worker concluir
        var processado = false;
        for (var i = 0; i < 40 && !processado; i++)
        {
            await Task.Delay(100);
            var eventos = await _client.GetFromJsonAsync<List<EventoDto>>("/webhooks/pagamentos");
            processado = eventos is [{ Processado: true }];
        }

        Assert.True(processado, "evento não foi processado em background dentro do tempo esperado");
    }
}
