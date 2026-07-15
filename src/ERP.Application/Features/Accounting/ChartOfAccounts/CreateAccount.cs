using ERP.Application.Common.Interfaces;
using ERP.Domain.Accounting;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Accounting.ChartOfAccounts;

public sealed record CreateAccountCommand(string Code, string Name, AccountType Type) : IRequest<Result<Guid>>;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).IsInEnum();
    }
}

public sealed class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    public CreateAccountCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateAccountCommand request, CancellationToken ct)
    {
        if (await _db.Accounts.AnyAsync(a => a.Code == request.Code, ct))
            return Result.Failure<Guid>(Error.Conflict($"An account with code {request.Code} already exists."));

        var account = new Account
        {
            Code = request.Code,
            Name = request.Name,
            Type = request.Type,
            IsSystem = false,
            IsActive = true
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return Result.Success(account.Id);
    }
}

// ---- Deactivate (system accounts can't be deleted, only deactivated) ----
public sealed record DeactivateAccountCommand(Guid Id) : IRequest<Result>;

public sealed class DeactivateAccountCommandHandler : IRequestHandler<DeactivateAccountCommand, Result>
{
    private readonly IApplicationDbContext _db;
    public DeactivateAccountCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result> Handle(DeactivateAccountCommand request, CancellationToken ct)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == request.Id, ct);
        if (account is null) return Result.Failure(Error.NotFound("Account not found."));

        try { account.Deactivate(); }
        catch (Domain.Common.DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
