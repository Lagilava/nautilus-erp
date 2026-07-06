using ERP.Application.Common.Interfaces;
using ERP.Domain.Catalog;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Catalog.UnitsOfMeasure;

public sealed record UnitOfMeasureDto(Guid Id, string Code, string Name, bool IsActive);

// ---- Create ----
public sealed record CreateUnitOfMeasureCommand(string Code, string Name) : IRequest<Result<Guid>>;

public sealed class CreateUnitOfMeasureCommandValidator : AbstractValidator<CreateUnitOfMeasureCommand>
{
    public CreateUnitOfMeasureCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateUnitOfMeasureCommandHandler
    : IRequestHandler<CreateUnitOfMeasureCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    public CreateUnitOfMeasureCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateUnitOfMeasureCommand request, CancellationToken ct)
    {
        if (await _db.UnitsOfMeasure.AnyAsync(u => u.Code == request.Code, ct))
            return Result.Failure<Guid>(Error.Conflict($"Unit '{request.Code}' already exists."));

        var uom = new UnitOfMeasure { Code = request.Code, Name = request.Name };
        _db.UnitsOfMeasure.Add(uom);
        await _db.SaveChangesAsync(ct);
        return Result.Success(uom.Id);
    }
}

// ---- List ----
public sealed record GetUnitsOfMeasureQuery : IRequest<Result<IReadOnlyList<UnitOfMeasureDto>>>;

public sealed class GetUnitsOfMeasureQueryHandler
    : IRequestHandler<GetUnitsOfMeasureQuery, Result<IReadOnlyList<UnitOfMeasureDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetUnitsOfMeasureQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<UnitOfMeasureDto>>> Handle(GetUnitsOfMeasureQuery request, CancellationToken ct)
    {
        var items = await _db.UnitsOfMeasure.AsNoTracking()
            .OrderBy(u => u.Code)
            .Select(u => new UnitOfMeasureDto(u.Id, u.Code, u.Name, u.IsActive))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<UnitOfMeasureDto>>(items);
    }
}
