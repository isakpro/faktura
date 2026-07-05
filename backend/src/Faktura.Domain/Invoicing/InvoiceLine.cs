using Faktura.Domain.Common;

namespace Faktura.Domain.Invoicing;

/// <summary>
/// En fakturarad. Priser anges exkl. moms. Netto och moms härleds och avrundas till öre per rad
/// så att deltotaler summerar till fakturans total utan differens.
/// </summary>
public sealed class InvoiceLine
{
    public string Description { get; }
    public decimal Quantity { get; }
    public decimal UnitPriceExclVat { get; }
    public VatRate VatRate { get; }

    /// <summary>Valfri enhet (st/tim/kg …). Null för äldre rader och fritextrader utan enhet.</summary>
    public string? Unit { get; }

    public InvoiceLine(string description, decimal quantity, decimal unitPriceExclVat, VatRate vatRate, string? unit = null)
    {
        Description = description?.Trim() ?? "";
        Quantity = quantity;
        UnitPriceExclVat = unitPriceExclVat;
        VatRate = vatRate;
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
    }

    /// <summary>Radens nettobelopp (antal × á-pris), öresavrundat.</summary>
    public Money Net => Money.Round(Quantity * UnitPriceExclVat);

    /// <summary>Radens momsbelopp (netto × sats), öresavrundat.</summary>
    public Money Vat => Money.Round(Net.Amount * VatRate.AsFraction());
}
