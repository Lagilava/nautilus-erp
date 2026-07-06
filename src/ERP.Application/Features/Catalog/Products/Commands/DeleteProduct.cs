using ERP.Application.Common.Interfaces;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Catalog.Products.Commands;

/// <summary>Soft-deletes a product (the DbContext converts the delete into a DeletedAt stamp).</summary>
public sealed record DeleteProductCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public DeleteProductCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (product is null)
            return Result.Failure(Error.NotFound("Product not found."));

        _db.Products.Remove(product);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
