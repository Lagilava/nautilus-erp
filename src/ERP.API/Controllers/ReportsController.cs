using ERP.API.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Reporting;
using ERP.Application.Features.Reports;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Reports exportable as CSV/Excel/PDF. The query produces a provider-agnostic table; the
/// controller renders it in the requested format via <see cref="IReportExporter"/>.
///
/// Restricted to Manager/Administrator: these exports carry cost prices and margins across the
/// whole catalogue, which is the most commercially sensitive data the system holds. The queries
/// are additionally branch-scoped, so a Manager exports only their own branch.
/// </summary>
[Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
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

    [HttpGet("receivables-aging")]
    public async Task<IActionResult> ReceivablesAging(
        [FromQuery] ReportFormat format = ReportFormat.Csv, CancellationToken ct = default)
    {
        var result = await Sender.Send(new GetReceivablesAgingReportQuery(), ct);
        if (result.IsFailure) return HandleResult(result);

        var file = _exporter.Export(result.Value, format);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>The receivables-aging table as JSON, for on-screen display.</summary>
    [HttpGet("receivables-aging/data")]
    public async Task<IActionResult> ReceivablesAgingData(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetReceivablesAgingReportQuery(), ct));

    [HttpGet("payables-aging")]
    public async Task<IActionResult> PayablesAging(
        [FromQuery] ReportFormat format = ReportFormat.Csv, CancellationToken ct = default)
    {
        var result = await Sender.Send(new GetPayablesAgingReportQuery(), ct);
        if (result.IsFailure) return HandleResult(result);

        var file = _exporter.Export(result.Value, format);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>The payables-aging table as JSON, for on-screen display.</summary>
    [HttpGet("payables-aging/data")]
    public async Task<IActionResult> PayablesAgingData(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetPayablesAgingReportQuery(), ct));

    [HttpGet("trial-balance")]
    public async Task<IActionResult> TrialBalance(
        [FromQuery] Guid? branchId, [FromQuery] DateOnly? asOfDate,
        [FromQuery] ReportFormat format = ReportFormat.Csv, CancellationToken ct = default)
    {
        var result = await Sender.Send(new GetTrialBalanceQuery(branchId, asOfDate), ct);
        if (result.IsFailure) return HandleResult(result);

        var file = _exporter.Export(result.Value, format);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>The trial-balance table as JSON, for on-screen display.</summary>
    [HttpGet("trial-balance/data")]
    public async Task<IActionResult> TrialBalanceData(
        [FromQuery] Guid? branchId, [FromQuery] DateOnly? asOfDate, CancellationToken ct)
        => HandleResult(await Sender.Send(new GetTrialBalanceQuery(branchId, asOfDate), ct));

    [HttpGet("profit-and-loss")]
    public async Task<IActionResult> ProfitAndLoss(
        [FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate, [FromQuery] Guid? branchId,
        [FromQuery] ReportFormat format = ReportFormat.Csv, CancellationToken ct = default)
    {
        var result = await Sender.Send(new GetProfitAndLossQuery(fromDate, toDate, branchId), ct);
        if (result.IsFailure) return HandleResult(result);

        var file = _exporter.Export(result.Value, format);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>The profit-and-loss table as JSON, for on-screen display.</summary>
    [HttpGet("profit-and-loss/data")]
    public async Task<IActionResult> ProfitAndLossData(
        [FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate, [FromQuery] Guid? branchId, CancellationToken ct)
        => HandleResult(await Sender.Send(new GetProfitAndLossQuery(fromDate, toDate, branchId), ct));

    [HttpGet("balance-sheet")]
    public async Task<IActionResult> BalanceSheet(
        [FromQuery] DateOnly asOfDate, [FromQuery] Guid? branchId,
        [FromQuery] ReportFormat format = ReportFormat.Csv, CancellationToken ct = default)
    {
        var result = await Sender.Send(new GetBalanceSheetQuery(asOfDate, branchId), ct);
        if (result.IsFailure) return HandleResult(result);

        var file = _exporter.Export(result.Value, format);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>The balance-sheet table as JSON, for on-screen display.</summary>
    [HttpGet("balance-sheet/data")]
    public async Task<IActionResult> BalanceSheetData(
        [FromQuery] DateOnly asOfDate, [FromQuery] Guid? branchId, CancellationToken ct)
        => HandleResult(await Sender.Send(new GetBalanceSheetQuery(asOfDate, branchId), ct));
}
