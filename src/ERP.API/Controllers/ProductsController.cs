using ERP.API.Common;
using ERP.Application.Features.Catalog.Products.Commands;
using ERP.Application.Features.Catalog.Products.Queries;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Product master CRUD. Reads: any authenticated user. Writes: Manager/Administrator.</summary>
[Authorize]
public sealed class ProductsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetProductsQuery query, CancellationToken ct)
        => HandleResult(await Sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new GetProductByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
    public async Task<IActionResult> Create(CreateProductCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
    public async Task<IActionResult> Update(Guid id, UpdateProductCommand command, CancellationToken ct)
        => id != command.Id
            ? BadRequest("Route id and body id must match.")
            : HandleResult(await Sender.Send(command, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new DeleteProductCommand(id), ct));
}
