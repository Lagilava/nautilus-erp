using ERP.Application.Common.Interfaces;
using ERP.Domain.Catalog;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Catalog.Products.Commands;

public sealed record CreateProductCommand(
    string Sku,
    string Name,
    string? Description,
    string? Barcode,
    Guid CategoryId,
    Guid UnitOfMeasureId,
    Guid TaxId,
    decimal CostPrice,
    decimal SellingPrice) : IRequest<Result<Guid>>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
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

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;

    public CreateProductCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken ct)
    {
        if (await _db.Products.AnyAsync(p => p.Sku == request.Sku, ct))
            return Result.Failure<Guid>(Error.Conflict($"A product with SKU '{request.Sku}' already exists."));

        // Validate the referenced vocabulary exists before creating — clear 400 over an FK crash.
        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
            return Result.Failure<Guid>(Error.Validation("Category does not exist."));
        if (!await _db.UnitsOfMeasure.AnyAsync(u => u.Id == request.UnitOfMeasureId, ct))
            return Result.Failure<Guid>(Error.Validation("Unit of measure does not exist."));
        if (!await _db.Taxes.AnyAsync(t => t.Id == request.TaxId, ct))
            return Result.Failure<Guid>(Error.Validation("Tax does not exist."));

        var product = new Product
        {
            Sku = request.Sku,
            Name = request.Name,
            Description = request.Description,
            Barcode = request.Barcode,
            CategoryId = request.CategoryId,
            UnitOfMeasureId = request.UnitOfMeasureId,
            TaxId = request.TaxId,
            CostPrice = request.CostPrice,
            SellingPrice = request.SellingPrice
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        return Result.Success(product.Id);
    }
}
