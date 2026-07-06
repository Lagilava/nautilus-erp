using ERP.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Common;

/// <summary>
/// Base for API controllers. Provides the MediatR sender and a single place to translate
/// the Application's <see cref="Result"/> vocabulary into HTTP responses, keeping
/// controllers thin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _sender;
    protected ISender Sender => _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected IActionResult HandleResult<T>(Result<T> result)
        => result.IsSuccess ? Ok(result.Value) : Problem(result.Error);

    protected IActionResult HandleResult(Result result)
        => result.IsSuccess ? NoContent() : Problem(result.Error);

    private IActionResult Problem(Error error)
    {
        var status = error.Code switch
        {
            "validation" => StatusCodes.Status400BadRequest,
            "unauthorized" => StatusCodes.Status401Unauthorized,
            "locked_out" => StatusCodes.Status423Locked,
            "conflict" => StatusCodes.Status409Conflict,
            "not_found" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(statusCode: status, title: error.Code, detail: error.Message);
    }
}
