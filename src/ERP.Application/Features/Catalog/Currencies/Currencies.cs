using ERP.Application.Common.Interfaces;
using ERP.Domain.Catalog;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Catalog.Currencies;

public sealed record CurrencyDto(Guid Id, string Code, string Name, string? Symbol, bool IsBaseCurrency, bool IsActive);

// ---- Create ----
public sealed record CreateCurrencyCommand(string Code, string Name, string? Symbol, bool IsBaseCurrency)
    : IRequest<Result<Guid>>;

public sealed class CreateCurrencyCommandValidator : AbstractValidator<CreateCurrencyCommand>
{
    public CreateCurrencyCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(3);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Symbol).MaximumLength(8);
    }
}

public sealed class CreateCurrencyCommandHandler : IRequestHandler<CreateCurrencyCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    public CreateCurrencyCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateCurrencyCommand request, CancellationToken ct)
    {
        var code = request.Code.ToUpperInvariant();
        if (await _db.Currencies.AnyAsync(c => c.Code == code, ct))
            return Result.Failure<Guid>(Error.Conflict($"Currency '{code}' already exists."));

        // Only one base currency: demote any existing base if this one claims it.
        if (request.IsBaseCurrency)
        {
            var currentBase = await _db.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency, ct);
            if (currentBase is not null) currentBase.IsBaseCurrency = false;
        }

        var currency = new Currency
        {
            Code = code,
            Name = request.Name,
            Symbol = request.Symbol,
            IsBaseCurrency = request.IsBaseCurrency
        };
        _db.Currencies.Add(currency);
        await _db.SaveChangesAsync(ct);
        return Result.Success(currency.Id);
    }
}

// ---- List ----
public sealed record GetCurrenciesQuery : IRequest<Result<IReadOnlyList<CurrencyDto>>>;

public sealed class GetCurrenciesQueryHandler : IRequestHandler<GetCurrenciesQuery, Result<IReadOnlyList<CurrencyDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetCurrenciesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<CurrencyDto>>> Handle(GetCurrenciesQuery request, CancellationToken ct)
    {
        var items = await _db.Currencies.AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CurrencyDto(c.Id, c.Code, c.Name, c.Symbol, c.IsBaseCurrency, c.IsActive))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<CurrencyDto>>(items);
    }
}
