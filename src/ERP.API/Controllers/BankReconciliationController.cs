using ERP.API.Common;
using ERP.Application.Features.Accounting.BankReconciliation;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Bank reconciliation: import/enter statement lines, list what is unreconciled on both sides
/// (statement lines and Cash-account journal lines), and match/unmatch a pair. Writes:
/// Manager/Administrator, following the sales/purchasing controllers' pattern.
/// </summary>
[Authorize]
[Route("api/bank-reconciliation")]
public sealed class BankReconciliationController : ApiControllerBase
{
    private const string Writers = $"{Roles.Administrator},{Roles.Manager}";

    [HttpGet("statement-lines/unreconciled")]
    public async Task<IActionResult> UnreconciledStatementLines(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetUnreconciledStatementLinesQuery(), ct));

    [HttpGet("journal-lines/unreconciled")]
    public async Task<IActionResult> UnreconciledJournalLines(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetUnreconciledCashJournalLinesQuery(), ct));

    [HttpPost("statement-lines")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> CreateStatementLine(CreateBankStatementLineCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("match")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Match(MatchStatementLineCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("statement-lines/{id:guid}/unmatch")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Unmatch(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new UnmatchStatementLineCommand(id), ct));
}
