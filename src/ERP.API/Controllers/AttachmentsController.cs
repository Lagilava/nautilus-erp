using ERP.API.Common;
using ERP.Application.Features.Documents.Attachments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Generic file attachments for any record, referenced by (entityType, entityId) — e.g. a
/// scanned supplier invoice or a goods-receipt photo. See <see cref="ERP.Domain.Documents.Attachment"/>.
/// </summary>
[Authorize]
[Route("api/attachments")]
public sealed class AttachmentsController : ApiControllerBase
{
    private const long MaxUploadBytes = UploadAttachmentCommandValidator.MaxSizeBytes;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string entityType, [FromQuery] Guid entityId, CancellationToken ct)
        => HandleResult(await Sender.Send(new GetAttachmentsQuery(entityType, entityId), ct));

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> Upload(
        [FromForm] string entityType, [FromForm] Guid entityId, [FromForm] IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "validation", detail: "The uploaded file is empty.");

        await using var stream = file.OpenReadStream();
        var command = new UploadAttachmentCommand(entityType, entityId, file.FileName, file.ContentType, file.Length, stream);
        var result = await Sender.Send(command, ct);
        return HandleResult(result);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var result = await Sender.Send(new DownloadAttachmentQuery(id), ct);
        if (result.IsFailure) return HandleResult(result);

        var (metadata, file) = result.Value;
        return File(file.Content, metadata.ContentType, metadata.FileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new DeleteAttachmentCommand(id), ct));
}
