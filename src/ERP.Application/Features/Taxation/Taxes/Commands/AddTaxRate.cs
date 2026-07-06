using ERP.Application.Common.Interfaces;
using ERP.Domain.Taxation;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Taxation.Taxes.Commands;

/// <summary>
/// Introduces a new effective-dated rate for a tax (e.g. VAT 15% → 15.5% from a date).
/// The previously open-ended rate is closed the day before the new one starts, so the
/// history has no overlap and documents always resolve the rate that applied on their date.
/// </summary>
public sealed record AddTaxRateCommand(Guid TaxId, decimal Percentage, DateOnly EffectiveFrom)
    : IRequest<Result>;

public sealed class AddTaxRateCommandValidator : AbstractValidator<AddTaxRateCommand>
{
    public AddTaxRateCommandValidator()
    {
        RuleFor(x => x.TaxId).NotEmpty();
        RuleFor(x => x.Percentage).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EffectiveFrom).NotEmpty();
    }
}

public sealed class AddTaxRateCommandHandler : IRequestHandler<AddTaxRateCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public AddTaxRateCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result> Handle(AddTaxRateCommand request, CancellationToken ct)
    {
        var tax = await _db.Taxes
            .Include(t => t.Rates)
            .FirstOrDefaultAsync(t => t.Id == request.TaxId, ct);

        if (tax is null)
            return Result.Failure(Error.NotFound("Tax not found."));
        if (tax.Treatment != TaxTreatment.Standard)
            return Result.Failure(Error.Validation("Only standard-rated taxes can carry rates."));

        if (tax.Rates.Any(r => r.EffectiveFrom == request.EffectiveFrom))
            return Result.Failure(Error.Conflict("A rate already starts on that date."));
        if (tax.Rates.Any(r => r.EffectiveFrom > request.EffectiveFrom))
            return Result.Failure(Error.Validation("New rate must start after the latest existing rate."));

        // Close the current open-ended rate the day before the new one begins.
        var openRate = tax.Rates.FirstOrDefault(r => r.EffectiveTo == null);
        if (openRate is not null)
            openRate.EffectiveTo = request.EffectiveFrom.AddDays(-1);

        tax.Rates.Add(new TaxRate
        {
            TaxId = tax.Id,
            Percentage = request.Percentage,
            EffectiveFrom = request.EffectiveFrom
        });

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
