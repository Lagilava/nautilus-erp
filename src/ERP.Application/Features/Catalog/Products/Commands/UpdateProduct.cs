using ERP.Application.Common.Interfaces;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Catalog.Products.Commands;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    string? Barcode,
    Guid CategoryId,
    Guid UnitOfMeasureId,
    Guid TaxId,
    decimal CostPrice,
    decimal SellingPrice,
    bool IsActive) : IRequest<Result>;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Barcode).MaximumLength(64);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.UnitOfMeasureId).NotEmpty();
        RuleFor(x => x.TaxId).NotEmpty();
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public UpdateProductCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (product is null)
            return Result.Failure(Error.NotFound("Product not found."));

        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
            return Result.Failure(Error.Validation("Category does not exist."));
        if (!await _db.UnitsOfMeasure.AnyAsync(u => u.Id == request.UnitOfMeasureId, ct))
            return Result.Failure(Error.Validation("Unit of measure does not exist."));
        if (!await _db.Taxes.AnyAsync(t => t.Id == request.TaxId, ct))
            return Result.Failure(Error.Validation("Tax does not exist."));

        product.Name = request.Name;
        product.Description = request.Description;
        product.Barcode = request.Barcode;
        product.CategoryId = request.CategoryId;
        product.UnitOfMeasureId = request.UnitOfMeasureId;
        product.TaxId = request.TaxId;
        product.CostPrice = request.CostPrice;
        product.SellingPrice = request.SellingPrice;
        product.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
