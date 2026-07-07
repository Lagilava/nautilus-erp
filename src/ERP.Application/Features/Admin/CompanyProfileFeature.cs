using ERP.Application.Common.Interfaces;
using ERP.Domain.Organization;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Admin;

public sealed record CompanyProfileDto(
    string LegalName, string? TradingName, string? Tin, string? AddressLine1,
    string? City, string? Country, string? Phone, string? Email, string BaseCurrency);

// ---- Get ----
public sealed record GetCompanyProfileQuery : IRequest<Result<CompanyProfileDto>>;

public sealed class GetCompanyProfileQueryHandler : IRequestHandler<GetCompanyProfileQuery, Result<CompanyProfileDto>>
{
    private readonly IApplicationDbContext _db;
    public GetCompanyProfileQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<CompanyProfileDto>> Handle(GetCompanyProfileQuery request, CancellationToken ct)
    {
        var c = await _db.CompanyProfiles.AsNoTracking().FirstOrDefaultAsync(ct)
                ?? new CompanyProfile();
        return Result.Success(new CompanyProfileDto(
            c.LegalName, c.TradingName, c.Tin, c.AddressLine1, c.City, c.Country, c.Phone, c.Email, c.BaseCurrency));
    }
}

// ---- Update ----
public sealed record UpdateCompanyProfileCommand(
    string LegalName, string? TradingName, string? Tin, string? AddressLine1,
    string? City, string? Country, string? Phone, string? Email) : IRequest<Result>;

public sealed class UpdateCompanyProfileCommandValidator : AbstractValidator<UpdateCompanyProfileCommand>
{
    public UpdateCompanyProfileCommandValidator()
    {
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Tin).MaximumLength(32);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class UpdateCompanyProfileCommandHandler : IRequestHandler<UpdateCompanyProfileCommand, Result>
{
    private readonly IApplicationDbContext _db;
    public UpdateCompanyProfileCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result> Handle(UpdateCompanyProfileCommand request, CancellationToken ct)
    {
        var company = await _db.CompanyProfiles.FirstOrDefaultAsync(ct);
        if (company is null)
        {
            company = new CompanyProfile { Id = CompanyProfile.SingletonId };
            _db.CompanyProfiles.Add(company);
        }

        company.LegalName = request.LegalName;
        company.TradingName = request.TradingName;
        company.Tin = request.Tin;
        company.AddressLine1 = request.AddressLine1;
        company.City = request.City;
        company.Country = request.Country;
        company.Phone = request.Phone;
        company.Email = request.Email;

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
