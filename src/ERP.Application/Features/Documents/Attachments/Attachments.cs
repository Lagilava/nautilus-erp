using ERP.Application.Common.Interfaces;
using ERP.Domain.Documents;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Documents.Attachments;

public sealed record AttachmentDto(
    Guid Id, string EntityType, Guid EntityId, string FileName, string ContentType, long SizeBytes, DateTimeOffset CreatedAt);

// ---- Upload ----
public sealed record UploadAttachmentCommand(
    string EntityType, Guid EntityId, string FileName, string ContentType, long SizeBytes, Stream Content)
    : IRequest<Result<AttachmentDto>>;

public sealed class UploadAttachmentCommandValidator : AbstractValidator<UploadAttachmentCommand>
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    public UploadAttachmentCommandValidator()
    {
        RuleFor(x => x.EntityType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(260);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(128);
        RuleFor(x => x.SizeBytes).GreaterThan(0).LessThanOrEqualTo(MaxSizeBytes);
    }
}

public sealed class UploadAttachmentCommandHandler : IRequestHandler<UploadAttachmentCommand, Result<AttachmentDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public UploadAttachmentCommandHandler(IApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<Result<AttachmentDto>> Handle(UploadAttachmentCommand request, CancellationToken ct)
    {
        var storageKey = await _storage.SaveAsync(request.Content, request.FileName, request.ContentType, ct);

        var attachment = new Attachment
        {
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            StorageKey = storageKey
        };
        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync(ct);

        return Result.Success(new AttachmentDto(
            attachment.Id, attachment.EntityType, attachment.EntityId, attachment.FileName,
            attachment.ContentType, attachment.SizeBytes, attachment.CreatedAt));
    }
}

// ---- List (for one owning record) ----
public sealed record GetAttachmentsQuery(string EntityType, Guid EntityId) : IRequest<Result<IReadOnlyList<AttachmentDto>>>;

public sealed class GetAttachmentsQueryHandler : IRequestHandler<GetAttachmentsQuery, Result<IReadOnlyList<AttachmentDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetAttachmentsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<AttachmentDto>>> Handle(GetAttachmentsQuery request, CancellationToken ct)
    {
        var items = await _db.Attachments.AsNoTracking()
            .Where(a => a.EntityType == request.EntityType && a.EntityId == request.EntityId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AttachmentDto(a.Id, a.EntityType, a.EntityId, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<AttachmentDto>>(items);
    }
}

// ---- Download ----
public sealed record DownloadAttachmentQuery(Guid Id) : IRequest<Result<(AttachmentDto Metadata, StoredFile File)>>;

public sealed class DownloadAttachmentQueryHandler : IRequestHandler<DownloadAttachmentQuery, Result<(AttachmentDto, StoredFile)>>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public DownloadAttachmentQueryHandler(IApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<Result<(AttachmentDto, StoredFile)>> Handle(DownloadAttachmentQuery request, CancellationToken ct)
    {
        var attachment = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.Id, ct);
        if (attachment is null)
            return Result.Failure<(AttachmentDto, StoredFile)>(Error.NotFound("Attachment not found."));

        var stored = await _storage.OpenAsync(attachment.StorageKey, ct);
        var dto = new AttachmentDto(
            attachment.Id, attachment.EntityType, attachment.EntityId, attachment.FileName,
            attachment.ContentType, attachment.SizeBytes, attachment.CreatedAt);
        return Result.Success((dto, stored with { ContentType = attachment.ContentType, FileName = attachment.FileName }));
    }
}

// ---- Delete ----
public sealed record DeleteAttachmentCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteAttachmentCommandHandler : IRequestHandler<DeleteAttachmentCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public DeleteAttachmentCommandHandler(IApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<Result> Handle(DeleteAttachmentCommand request, CancellationToken ct)
    {
        var attachment = await _db.Attachments.FirstOrDefaultAsync(a => a.Id == request.Id, ct);
        if (attachment is null)
            return Result.Failure(Error.NotFound("Attachment not found."));

        _db.Attachments.Remove(attachment);
        await _db.SaveChangesAsync(ct);
        await _storage.DeleteAsync(attachment.StorageKey, ct);

        return Result.Success();
    }
}
