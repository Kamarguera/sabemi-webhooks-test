namespace SabemiWebhooks.Contracts;

/// <summary>Payload recebido do banco parceiro no webhook de pagamento.</summary>
public record PaymentRequest(
    string   IdTransacao,
    string   IdContrato,
    decimal  Valor,
    DateTime DataPagamento,
    string   Status);
