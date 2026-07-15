using ERP.Application.Common.Interfaces;
using ERP.Domain.Accounting;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Accounting.ChartOfAccounts;

public sealed record AccountDto(Guid Id, string Code, string Name, AccountType Type, bool IsSystem, bool IsActive);

public sealed record GetAccountsQuery(bool? ActiveOnly = null) : IRequest<Result<IReadOnlyList<AccountDto>>>;

public sealed class GetAccountsQueryHandler : IRequestHandler<GetAccountsQuery, Result<IReadOnlyList<AccountDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetAccountsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<AccountDto>>> Handle(GetAccountsQuery request, CancellationToken ct)
    {
        var query = _db.Accounts.AsNoTracking().AsQueryable();
        if (request.ActiveOnly is true) query = query.Where(a => a.IsActive);

        var items = await query
            .OrderBy(a => a.Code)
            .Select(a => new AccountDto(a.Id, a.Code, a.Name, a.Type, a.IsSystem, a.IsActive))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<AccountDto>>(items);
    }
}

public sealed record GetAccountByIdQuery(Guid Id) : IRequest<Result<AccountDto>>;

public sealed class GetAccountByIdQueryHandler : IRequestHandler<GetAccountByIdQuery, Result<AccountDto>>
{
    private readonly IApplicationDbContext _db;
    public GetAccountByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<AccountDto>> Handle(GetAccountByIdQuery request, CancellationToken ct)
    {
        var account = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.Id, ct);
        if (account is null) return Result.Failure<AccountDto>(Error.NotFound("Account not found."));

        return Result.Success(new AccountDto(
            account.Id, account.Code, account.Name, account.Type, account.IsSystem, account.IsActive));
    }
}
