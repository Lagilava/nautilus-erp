using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.Mfa;

// ---- Begin setup: (re)generate an authenticator secret, not yet active ----
public sealed record BeginMfaSetupCommand : IRequest<Result<MfaSetup>>;

public sealed class BeginMfaSetupCommandHandler : IRequestHandler<BeginMfaSetupCommand, Result<MfaSetup>>
{
    private readonly IIdentityService _identity;
    private readonly ICurrentUserService _currentUser;

    public BeginMfaSetupCommandHandler(IIdentityService identity, ICurrentUserService currentUser)
    {
        _identity = identity;
        _currentUser = currentUser;
    }

    public Task<Result<MfaSetup>> Handle(BeginMfaSetupCommand request, CancellationToken ct)
        => _currentUser.UserId is { } userId
            ? _identity.BeginMfaSetupAsync(userId, ct)
            : Task.FromResult(Result.Failure<MfaSetup>(Error.Unauthorized("Not authenticated.")));
}

// ---- Confirm setup with a code: turns MFA on and returns recovery codes ----
public sealed record EnableMfaCommand(string Code) : IRequest<Result<IReadOnlyList<string>>>;

public sealed class EnableMfaCommandValidator : AbstractValidator<EnableMfaCommand>
{
    public EnableMfaCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(6).Matches("^[0-9]+$").WithMessage("Enter the 6-digit code.");
    }
}

public sealed class EnableMfaCommandHandler : IRequestHandler<EnableMfaCommand, Result<IReadOnlyList<string>>>
{
    private readonly IIdentityService _identity;
    private readonly ICurrentUserService _currentUser;

    public EnableMfaCommandHandler(IIdentityService identity, ICurrentUserService currentUser)
    {
        _identity = identity;
        _currentUser = currentUser;
    }

    public Task<Result<IReadOnlyList<string>>> Handle(EnableMfaCommand request, CancellationToken ct)
        => _currentUser.UserId is { } userId
            ? _identity.EnableMfaAsync(userId, request.Code, ct)
            : Task.FromResult(Result.Failure<IReadOnlyList<string>>(Error.Unauthorized("Not authenticated.")));
}

// ---- Disable: requires the current password ----
public sealed record DisableMfaCommand(string CurrentPassword) : IRequest<Result>;

public sealed class DisableMfaCommandValidator : AbstractValidator<DisableMfaCommand>
{
    public DisableMfaCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
    }
}

public sealed class DisableMfaCommandHandler : IRequestHandler<DisableMfaCommand, Result>
{
    private readonly IIdentityService _identity;
    private readonly ICurrentUserService _currentUser;

    public DisableMfaCommandHandler(IIdentityService identity, ICurrentUserService currentUser)
    {
        _identity = identity;
        _currentUser = currentUser;
    }

    public Task<Result> Handle(DisableMfaCommand request, CancellationToken ct)
        => _currentUser.UserId is { } userId
            ? _identity.DisableMfaAsync(userId, request.CurrentPassword, ct)
            : Task.FromResult(Result.Failure(Error.Unauthorized("Not authenticated.")));
}
