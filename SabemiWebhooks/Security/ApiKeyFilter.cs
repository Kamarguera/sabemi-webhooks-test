namespace SabemiWebhooks.Security;

/// <summary>
/// Filtro de endpoint que exige o header X-Api-Key com a chave configurada.
/// Aplicado apenas às rotas que recebem dados do banco parceiro.
/// </summary>
public class ApiKeyFilter(IConfiguration config) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var apiKey = config["ApiKey"];
        if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var key) ||
            key != apiKey)
            return Results.Unauthorized();

        return await next(context);
    }
}
