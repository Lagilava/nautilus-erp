using ERP.API.Common;
using ERP.Application.Features.Sales.Invoices;
using ERP.Application.Features.Sales.Payments;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Invoices, their lifecycle, fiscalization outcome, and payments.</summary>
[Authorize]
public sealed class InvoicesController : ApiControllerBase
{
    private const string Writers = $"{Roles.Administrator},{Roles.Manager}";

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetInvoicesQuery query, CancellationToken ct)
        => HandleResult(await Sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new GetInvoiceByIdQuery(id), ct));

    [HttpPost("from-order")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> CreateFromOrder(CreateInvoiceFromOrderCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("{id:guid}/issue")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Issue(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new IssueInvoiceCommand(id), ct));

    [HttpPost("{id:guid}/void")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Void(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new VoidInvoiceCommand(id), ct));

    [HttpGet("{id:guid}/payments")]
    public async Task<IActionResult> Payments(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new GetPaymentsByInvoiceQuery(id), ct));

    [HttpPost("{id:guid}/payments")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> RecordPayment(Guid id, RecordPaymentCommand command, CancellationToken ct)
        => id != command.InvoiceId
            ? BadRequest("Route id and body invoiceId must match.")
            : HandleResult(await Sender.Send(command, ct));
}
