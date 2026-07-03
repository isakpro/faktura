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

    public InvoiceLine(string description, decimal quantity, decimal unitPriceExclVat, VatRate vatRate)
    {
        Description = description?.Trim() ?? "";
        Quantity = quantity;
        UnitPriceExclVat = unitPriceExclVat;
        VatRate = vatRate;
    }

    /// <summary>Radens nettobelopp (antal × á-pris), öresavrundat.</summary>
    public Money Net => Money.Round(Quantity * UnitPriceExclVat);

    /// <summary>Radens momsbelopp (netto × sats), öresavrundat.</summary>
    public Money Vat => Money.Round(Net.Amount * VatRate.AsFraction());
}
