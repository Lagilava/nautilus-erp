using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Domain.Sales;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sales.Customers;

public sealed record CustomerDto(
    Guid Id, string Code, string Name, string? Email, string? Phone,
    string? TaxIdentificationNumber, decimal CreditLimit, bool IsActive, string? RowVersion);

// ---- Create ----
public sealed record CreateCustomerCommand(
    string Code, string Name, string? Email, string? Phone,
    string? AddressLine1, string? City, string? Country,
    string? TaxIdentificationNumber, decimal CreditLimit) : IRequest<Result<Guid>>;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    public CreateCustomerCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateCustomerCommand request, CancellationToken ct)
    {
        if (await _db.Customers.AnyAsync(c => c.Code == request.Code, ct))
            return Result.Failure<Guid>(Error.Conflict($"Customer '{request.Code}' already exists."));

        var customer = new Customer
        {
            Code = request.Code,
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            AddressLine1 = request.AddressLine1,
            City = request.City,
            Country = request.Country,
            TaxIdentificationNumber = request.TaxIdentificationNumber,
            CreditLimit = request.CreditLimit
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);
        return Result.Success(customer.Id);
    }
}

// ---- List ----
public sealed record GetCustomersQuery : PagedQuery, IRequest<Result<PagedResult<CustomerDto>>>;

public sealed class GetCustomersQueryHandler
    : IRequestHandler<GetCustomersQuery, Result<PagedResult<CustomerDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetCustomersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<PagedResult<CustomerDto>>> Handle(GetCustomersQuery request, CancellationToken ct)
    {
        var query = _db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(c => c.Code.Contains(term) || c.Name.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CustomerDto(
                c.Id, c.Code, c.Name, c.Email, c.Phone, c.TaxIdentificationNumber, c.CreditLimit, c.IsActive,
                c.RowVersion == null ? null : Convert.ToBase64String(c.RowVersion)))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<CustomerDto>(items, request.Page, request.PageSize, total));
    }
}

// ---- Update ----
/// <summary>
/// <see cref="RowVersion"/> must be the base64 token the caller last read the customer with
/// (<see cref="CustomerDto.RowVersion"/>). If another user saved a change in the meantime, the
/// token no longer matches what's in the database and this fails with a conflict rather than
/// silently overwriting their edit.
/// </summary>
public sealed record UpdateCustomerCommand(
    Guid Id, string Name, string? Email, string? Phone,
    string? AddressLine1, string? City, string? Country,
    string? TaxIdentificationNumber, decimal CreditLimit, string? RowVersion) : IRequest<Result>;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IRealtimeNotifier _notifications;
    private readonly ICurrentUserService _currentUser;

    public UpdateCustomerCommandHandler(
        IApplicationDbContext db, IRealtimeNotifier notifications, ICurrentUserService currentUser)
    {
        _db = db;
        _notifications = notifications;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateCustomerCommand request, CancellationToken ct)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (customer is null) return Result.Failure(Error.NotFound("Customer not found."));

        // Tell EF what version we believe is still current. If a concurrent edit already moved
        // it on, the UPDATE's WHERE clause matches zero rows and SaveChanges throws below —
        // rather than the two edits silently clobbering one another.
        var entry = _db.Entry(customer);
        entry.Property(c => c.RowVersion).OriginalValue =
            string.IsNullOrEmpty(request.RowVersion) ? null : Convert.FromBase64String(request.RowVersion);

        customer.Name = request.Name;
        customer.Email = request.Email;
        customer.Phone = request.Phone;
        customer.AddressLine1 = request.AddressLine1;
        customer.City = request.City;
        customer.Country = request.Country;
        customer.TaxIdentificationNumber = request.TaxIdentificationNumber;
        customer.CreditLimit = request.CreditLimit;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict(
                "This customer was changed by someone else since you loaded it. Reload and try again."));
        }

        await _notifications.PublishToAllAsync(
            new NotificationMessage(
                "Customer updated", $"{customer.Name} was updated by {_currentUser.Email ?? "another user"}.",
                EntityType: "Customer", EntityId: customer.Id), ct);

        return Result.Success();
    }
}
