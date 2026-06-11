namespace SabemiWebhooks.Models;

/// <summary>Log de eventos brutos — registra toda notificação recebida do banco parceiro.</summary>
public class WebhookEvent
{
    public Guid     Id            { get; set; }
    public string   IdTransacao   { get; set; } = "";
    public string   IdContrato    { get; set; } = "";
    public decimal  Valor         { get; set; }
    public DateTime DataPagamento { get; set; }
    public string   Status        { get; set; } = "";
    public DateTime RecebidoEm    { get; set; }
    public bool     Processado    { get; set; }
    public string?  Erro          { get; set; }
}
