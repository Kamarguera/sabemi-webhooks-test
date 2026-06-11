using SabemiWebhooks.Contracts;
using SabemiWebhooks.Security;
using SabemiWebhooks.Services;

namespace SabemiWebhooks.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/webhooks");

        group.MapPost("/pagamento", async (PaymentRequest payload, PaymentService service) =>
        {
            var result = await service.ReceiveAsync(payload);
            return result.Status switch
            {
                IntakeStatus.Invalid   => Results.BadRequest(new { message = result.Error }),
                IntakeStatus.Duplicate => Results.Ok(new { message = "Duplicado — ignorado." }),
                _                      => Results.Accepted(null, new { message = "Recebido. Processando em background." })
            };
        })
        .AddEndpointFilter<ApiKeyFilter>();

        group.MapGet("/pagamentos", async (PaymentService service, string? status, string? idContrato) =>
            Results.Ok(await service.ListAsync(status, idContrato)));
    }
}
