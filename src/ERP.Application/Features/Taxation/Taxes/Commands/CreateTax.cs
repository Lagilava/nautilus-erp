using ERP.Application.Common.Interfaces;
using ERP.Domain.Taxation;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Taxation.Taxes.Commands;

/// <summary>
/// Creates a tax definition with an optional initial rate. For Standard treatment an
/// initial percentage + effective-from date establishes the first rate window.
/// </summary>
public sealed record CreateTaxCommand(
    string Code,
    string Name,
    TaxTreatment Treatment) : IRequest<Result<Guid>>
{
    public decimal? InitialPercentage { get; init; }
    public DateOnly? EffectiveFrom { get; init; }
}

public sealed class CreateTaxCommandValidator : AbstractValidator<CreateTaxCommand>
{
    public CreateTaxCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Treatment).IsInEnum();

        // A standard-rated tax must be created with a starting rate; zero/exempt must not carry one.
        When(x => x.Treatment == TaxTreatment.Standard, () =>
        {
            RuleFor(x => x.InitialPercentage)
                .NotNull().WithMessage("A standard-rated tax requires an initial percentage.")
                .GreaterThanOrEqualTo(0).When(x => x.InitialPercentage.HasValue);
            RuleFor(x => x.EffectiveFrom)
                .NotNull().WithMessage("A standard-rated tax requires an effective-from date.");
        });

        When(x => x.Treatment != TaxTreatment.Standard, () =>
            RuleFor(x => x.InitialPercentage)
                .Null().WithMessage("Zero-rated and exempt taxes must not carry a percentage."));
    }
}

public sealed class CreateTaxCommandHandler : IRequestHandler<CreateTaxCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;

    public CreateTaxCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateTaxCommand request, CancellationToken ct)
    {
        if (await _db.Taxes.AnyAsync(t => t.Code == request.Code, ct))
            return Result.Failure<Guid>(Error.Conflict($"A tax with code '{request.Code}' already exists."));

        var tax = new Tax
        {
            Code = request.Code,
            Name = request.Name,
            Treatment = request.Treatment
        };

        if (request.Treatment == TaxTreatment.Standard)
        {
            tax.Rates.Add(new TaxRate
            {
                Percentage = request.InitialPercentage!.Value,
                EffectiveFrom = request.EffectiveFrom!.Value
            });
        }

        _db.Taxes.Add(tax);
        await _db.SaveChangesAsync(ct);
        return Result.Success(tax.Id);
    }
}
