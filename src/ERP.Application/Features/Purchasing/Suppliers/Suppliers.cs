using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Domain.Purchasing;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Purchasing.Suppliers;

public sealed record SupplierDto(
    Guid Id, string Code, string Name, string? Email, string? Phone,
    string? TaxIdentificationNumber, bool IsActive);

// ---- Create ----
public sealed record CreateSupplierCommand(
    string Code, string Name, string? Email, string? Phone,
    string? AddressLine1, string? City, string? Country, string? TaxIdentificationNumber)
    : IRequest<Result<Guid>>;

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    public CreateSupplierCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateSupplierCommand request, CancellationToken ct)
    {
        if (await _db.Suppliers.AnyAsync(sup => sup.Code == request.Code, ct))
            return Result.Failure<Guid>(Error.Conflict($"Supplier '{request.Code}' already exists."));

        var supplier = new Supplier
        {
            Code = request.Code,
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            AddressLine1 = request.AddressLine1,
            City = request.City,
            Country = request.Country,
            TaxIdentificationNumber = request.TaxIdentificationNumber
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync(ct);
        return Result.Success(supplier.Id);
    }
}

// ---- List ----
public sealed record GetSuppliersQuery : PagedQuery, IRequest<Result<PagedResult<SupplierDto>>>;

public sealed class GetSuppliersQueryHandler
    : IRequestHandler<GetSuppliersQuery, Result<PagedResult<SupplierDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetSuppliersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<PagedResult<SupplierDto>>> Handle(GetSuppliersQuery request, CancellationToken ct)
    {
        var query = _db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(sup => sup.Code.Contains(term) || sup.Name.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(sup => sup.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(sup => new SupplierDto(
                sup.Id, sup.Code, sup.Name, sup.Email, sup.Phone, sup.TaxIdentificationNumber, sup.IsActive))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<SupplierDto>(items, request.Page, request.PageSize, total));
    }
}
