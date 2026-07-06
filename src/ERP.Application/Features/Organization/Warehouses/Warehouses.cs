using ERP.Application.Common.Interfaces;
using ERP.Domain.Organization;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Organization.Warehouses;

public sealed record WarehouseDto(Guid Id, string Code, string Name, Guid BranchId, string BranchName, bool IsActive);

// ---- Create ----
public sealed record CreateWarehouseCommand(string Code, string Name, Guid BranchId) : IRequest<Result<Guid>>;

public sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class CreateWarehouseCommandHandler : IRequestHandler<CreateWarehouseCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    public CreateWarehouseCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateWarehouseCommand request, CancellationToken ct)
    {
        if (await _db.Warehouses.AnyAsync(w => w.Code == request.Code, ct))
            return Result.Failure<Guid>(Error.Conflict($"Warehouse '{request.Code}' already exists."));
        if (!await _db.Branches.AnyAsync(b => b.Id == request.BranchId, ct))
            return Result.Failure<Guid>(Error.Validation("Branch does not exist."));

        var warehouse = new Warehouse { Code = request.Code, Name = request.Name, BranchId = request.BranchId };
        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(ct);
        return Result.Success(warehouse.Id);
    }
}

// ---- List ----
public sealed record GetWarehousesQuery : IRequest<Result<IReadOnlyList<WarehouseDto>>>;

public sealed class GetWarehousesQueryHandler : IRequestHandler<GetWarehousesQuery, Result<IReadOnlyList<WarehouseDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetWarehousesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<WarehouseDto>>> Handle(GetWarehousesQuery request, CancellationToken ct)
    {
        var items = await _db.Warehouses.AsNoTracking()
            .OrderBy(w => w.Code)
            .Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.BranchId, w.Branch!.Name, w.IsActive))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<WarehouseDto>>(items);
    }
}
