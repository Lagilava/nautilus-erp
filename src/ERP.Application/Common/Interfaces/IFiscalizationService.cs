using ERP.Domain.Sales;

namespace ERP.Application.Common.Interfaces;

/// <summary>Outcome of submitting an invoice to the fiscalization authority.</summary>
public sealed record FiscalizationResult(FiscalStatus Status, string? Reference, string? Message);

/// <summary>
/// Port for submitting issued invoices to Fiji's FRCS TPOS / VAT Monitoring System (VMS)
/// for accreditation. The real integration is <b>unverified</b> — see the Fiji Localization
/// Requirements — so the shipped adapter is a stub that reports <see cref="FiscalStatus.NotSubmitted"/>.
/// Modelling the boundary now means invoicing does not need reworking once the VMS spec is confirmed.
/// </summary>
public interface IFiscalizationService
{
    Task<FiscalizationResult> SubmitAsync(Invoice invoice, CancellationToken cancellationToken = default);
}
