using ERP.API.Common;
using ERP.Application.Features.Sales.Customers;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[Authorize]
public sealed class CustomersController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetCustomersQuery query, CancellationToken ct)
        => HandleResult(await Sender.Send(query, ct));

    [HttpPost]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
    public async Task<IActionResult> Create(CreateCustomerCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));
}
