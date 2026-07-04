using Faktura.Domain.Invoicing;

namespace Faktura.Domain.Abstractions;

/// <summary>Genererar en PDF för en skickad faktura/kreditfaktura. Implementeras i Infrastructure.</summary>
public interface IInvoicePdfGenerator
{
    /// <param name="invoice">Skickad faktura eller kreditfaktura.</param>
    /// <param name="sellerName">Säljarens (organisationens) namn.</param>
    byte[] Generate(Invoice invoice, string sellerName);
}
