using ERP.API.Common;
using ERP.Application.Features.Accounting.JournalEntries;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Journal entries: list/get, create a draft manual entry, post it, void a posted entry
/// (writes a reversing entry). Writes: Manager/Administrator, following the sales/purchasing
/// controllers' pattern.
/// </summary>
[Authorize]
[Route("api/journal-entries")]
public sealed class JournalEntriesController : ApiControllerBase
{
    private const string Writers = $"{Roles.Administrator},{Roles.Manager}";

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetJournalEntriesQuery query, CancellationToken ct)
        => HandleResult(await Sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new GetJournalEntryByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Create(CreateManualJournalEntryCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("{id:guid}/post")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Post(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new PostJournalEntryCommand(id), ct));

    [HttpPost("{id:guid}/void")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Void(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new VoidJournalEntryCommand(id), ct));
}
