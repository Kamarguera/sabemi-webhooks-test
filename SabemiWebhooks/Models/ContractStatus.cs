namespace SabemiWebhooks.Models;

/// <summary>Status consolidado por contrato, atualizado pelo processamento em background.</summary>
public class ContractStatus
{
    public string   IdContrato   { get; set; } = "";
    public string   UltimoStatus { get; set; } = "";
    public DateTime Atualizado   { get; set; }
    public int      Total        { get; set; }
    public decimal  ValorTotal   { get; set; }
}
