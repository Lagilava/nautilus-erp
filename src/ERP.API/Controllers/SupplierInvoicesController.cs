using ERP.API.Common;
using ERP.Application.Features.Purchasing.SupplierInvoices;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Supplier invoices (accounts payable), their lifecycle, and payments.</summary>
[Authorize]
[Route("api/supplier-invoices")]
public sealed class SupplierInvoicesController : ApiControllerBase
{
    private const string Writers = $"{Roles.Administrator},{Roles.Manager}";

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetSupplierInvoicesQuery query, CancellationToken ct)
        => HandleResult(await Sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new GetSupplierInvoiceByIdQuery(id), ct));

    [HttpPost("from-order")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> CreateFromOrder(CreateSupplierInvoiceFromOrderCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new ApproveSupplierInvoiceCommand(id), ct));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new CancelSupplierInvoiceCommand(id), ct));

    [HttpPost("{id:guid}/payments")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> RecordPayment(Guid id, RecordSupplierPaymentCommand command, CancellationToken ct)
        => id != command.SupplierInvoiceId
            ? BadRequest("Route id and body supplierInvoiceId must match.")
            : HandleResult(await Sender.Send(command, ct));
}
