using ERP.Application.Common.Interfaces;
using ERP.Domain.Organization;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Organization.Branches;

public sealed record BranchDto(
    Guid Id, string Code, string Name, string? City, string? Country,
    string? TaxIdentificationNumber, bool IsActive);

// ---- Create ----
public sealed record CreateBranchCommand(
    string Code, string Name, string? AddressLine1, string? AddressLine2,
    string? City, string? Country, string? Phone, string? Email,
    string? TaxIdentificationNumber) : IRequest<Result<Guid>>;

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.TaxIdentificationNumber).MaximumLength(32);
    }
}

public sealed class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    public CreateBranchCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateBranchCommand request, CancellationToken ct)
    {
        if (await _db.Branches.AnyAsync(b => b.Code == request.Code, ct))
            return Result.Failure<Guid>(Error.Conflict($"Branch '{request.Code}' already exists."));

        var branch = new Branch
        {
            Code = request.Code,
            Name = request.Name,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            Country = request.Country,
            Phone = request.Phone,
            Email = request.Email,
            TaxIdentificationNumber = request.TaxIdentificationNumber
        };
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync(ct);
        return Result.Success(branch.Id);
    }
}

// ---- List ----
public sealed record GetBranchesQuery : IRequest<Result<IReadOnlyList<BranchDto>>>;

public sealed class GetBranchesQueryHandler : IRequestHandler<GetBranchesQuery, Result<IReadOnlyList<BranchDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetBranchesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<BranchDto>>> Handle(GetBranchesQuery request, CancellationToken ct)
    {
        var items = await _db.Branches.AsNoTracking()
            .OrderBy(b => b.Code)
            .Select(b => new BranchDto(
                b.Id, b.Code, b.Name, b.City, b.Country, b.TaxIdentificationNumber, b.IsActive))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<BranchDto>>(items);
    }
}
