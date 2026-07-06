using Faktura.Domain.Invoicing;
using Faktura.Domain.Organizations;

namespace Faktura.Domain.Abstractions;

/// <summary>Genererar en PDF för en skickad faktura/kreditfaktura. Implementeras i Infrastructure.</summary>
public interface IInvoicePdfGenerator
{
    /// <param name="invoice">Skickad faktura eller kreditfaktura.</param>
    /// <param name="seller">Säljande organisation — namn + ev. fakturaprofil (orgnr, adress, betalningsuppgifter).</param>
    byte[] Generate(Invoice invoice, Organization? seller);
}
