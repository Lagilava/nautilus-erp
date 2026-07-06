using ERP.Application.Common.Interfaces;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Taxation.Taxes.Queries;

/// <summary>Lists all taxes with their rate history and the rate in force today.</summary>
public sealed record GetTaxesQuery : IRequest<Result<IReadOnlyList<TaxDto>>>;

public sealed class GetTaxesQueryHandler : IRequestHandler<GetTaxesQuery, Result<IReadOnlyList<TaxDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetTaxesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<TaxDto>>> Handle(GetTaxesQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var taxes = await _db.Taxes.AsNoTracking()
            .Include(t => t.Rates)
            .OrderBy(t => t.Code)
            .ToListAsync(ct);

        // Rate resolution uses the domain method, so "current rate" logic lives in one place.
        var dtos = taxes.Select(t => new TaxDto(
                t.Id, t.Code, t.Name, t.Treatment, t.IsActive,
                t.GetRateOn(today),
                t.Rates
                    .OrderBy(r => r.EffectiveFrom)
                    .Select(r => new TaxRateDto(r.Id, r.Percentage, r.EffectiveFrom, r.EffectiveTo))
                    .ToList()))
            .ToList();

        return Result.Success<IReadOnlyList<TaxDto>>(dtos);
    }
}
