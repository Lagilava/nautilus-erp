using ERP.Application.Common.Interfaces;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Catalog.Products.Queries;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<Result<ProductDto>>;

public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    private readonly IApplicationDbContext _db;

    public GetProductByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var dto = await _db.Products.AsNoTracking()
            .Where(p => p.Id == request.Id)
            .Select(p => new ProductDto(
                p.Id, p.Sku, p.Name, p.Description, p.Barcode,
                p.CategoryId, p.Category!.Name,
                p.UnitOfMeasureId, p.UnitOfMeasure!.Code,
                p.TaxId, p.Tax!.Code,
                p.CostPrice, p.SellingPrice, p.IsActive))
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? Result.Failure<ProductDto>(Error.NotFound("Product not found."))
            : Result.Success(dto);
    }
}
