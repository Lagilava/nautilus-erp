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
    bool IsActive,
    string? RowVersion) : IRequest<Result>;

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
    private readonly IRealtimeNotifier _notifications;
    private readonly ICurrentUserService _currentUser;

    public UpdateProductCommandHandler(
        IApplicationDbContext db, IRealtimeNotifier notifications, ICurrentUserService currentUser)
    {
        _db = db;
        _notifications = notifications;
        _currentUser = currentUser;
    }

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

        // See UpdateCustomerCommandHandler for why: this makes a save against a stale copy of
        // the product fail loudly instead of silently overwriting someone else's edit.
        _db.Entry(product).Property(p => p.RowVersion).OriginalValue =
            string.IsNullOrEmpty(request.RowVersion) ? null : Convert.FromBase64String(request.RowVersion);

        product.Name = request.Name;
        product.Description = request.Description;
        product.Barcode = request.Barcode;
        product.CategoryId = request.CategoryId;
        product.UnitOfMeasureId = request.UnitOfMeasureId;
        product.TaxId = request.TaxId;
        product.CostPrice = request.CostPrice;
        product.SellingPrice = request.SellingPrice;
        product.IsActive = request.IsActive;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict(
                "This product was changed by someone else since you loaded it. Reload and try again."));
        }

        await _notifications.PublishToAllAsync(
            new NotificationMessage(
                "Product updated", $"{product.Name} was updated by {_currentUser.Email ?? "another user"}.",
                EntityType: "Product", EntityId: product.Id), ct);

        return Result.Success();
    }
}
