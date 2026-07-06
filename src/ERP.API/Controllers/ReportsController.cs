using ERP.API.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Reporting;
using ERP.Application.Features.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Reports exportable as CSV/Excel/PDF. The query produces a provider-agnostic table; the
/// controller renders it in the requested format via <see cref="IReportExporter"/>.
/// </summary>
[Authorize]
[Route("api/reports")]
public sealed class ReportsController : ApiControllerBase
{
    private readonly IReportExporter _exporter;

    public ReportsController(IReportExporter exporter) => _exporter = exporter;

    [HttpGet("inventory-valuation")]
    public async Task<IActionResult> InventoryValuation(
        [FromQuery] Guid? warehouseId, [FromQuery] ReportFormat format = ReportFormat.Csv,
        CancellationToken ct = default)
    {
        var result = await Sender.Send(new GetInventoryValuationReportQuery(warehouseId), ct);
        if (result.IsFailure) return HandleResult(result);

        var file = _exporter.Export(result.Value, format);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
