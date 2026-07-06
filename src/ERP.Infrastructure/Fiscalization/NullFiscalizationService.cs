using ERP.Application.Common.Interfaces;
using ERP.Domain.Sales;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Fiscalization;

/// <summary>
/// Placeholder fiscalization adapter. It does NOT contact FRCS/VMS — that integration is
/// unverified and must not be faked as successful. It records the invoice as
/// <see cref="FiscalStatus.NotSubmitted"/> and logs, so the system behaves honestly until a
/// verified VMS adapter replaces it. Swapping in the real adapter is a DI change only.
/// </summary>
public sealed class NullFiscalizationService : IFiscalizationService
{
    private readonly ILogger<NullFiscalizationService> _logger;

    public NullFiscalizationService(ILogger<NullFiscalizationService> logger) => _logger = logger;

    public Task<FiscalizationResult> SubmitAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Fiscalization stub: invoice {Number} not submitted to FRCS/VMS (no verified adapter configured).",
            invoice.Number);

        return Task.FromResult(new FiscalizationResult(
            FiscalStatus.NotSubmitted, Reference: null,
            Message: "FRCS/VMS integration not configured."));
    }
}
