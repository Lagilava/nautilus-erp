using ERP.Domain.Taxation;

namespace ERP.Application.Features.Taxation.Taxes;

public sealed record TaxRateDto(Guid Id, decimal Percentage, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

/// <summary>Read model for a tax and its rate history, including the rate in force today.</summary>
public sealed record TaxDto(
    Guid Id,
    string Code,
    string Name,
    TaxTreatment Treatment,
    bool IsActive,
    decimal CurrentRate,
    IReadOnlyList<TaxRateDto> Rates);
