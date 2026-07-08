using ERP.Application.Common.Interfaces;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.Profile;

// ---- Update own display details ----

/// <summary>
/// Self-service profile update. Email, roles, and branch are intentionally absent: those
/// are privileged attributes and only an administrator may change them.
/// </summary>
public sealed record UpdateProfileCommand(string FirstName, string LastName) : IRequest<Result>;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, Result>
{
    private readonly IIdentityService _identity;
    private readonly ICurrentUserService _currentUser;

    public UpdateProfileCommandHandler(IIdentityService identity, ICurrentUserService currentUser)
    {
        _identity = identity;
        _currentUser = currentUser;
    }

    public Task<Result> Handle(UpdateProfileCommand request, CancellationToken ct)
        => _currentUser.UserId is { } userId
            ? _identity.UpdateProfileAsync(userId, request.FirstName, request.LastName, ct)
            : Task.FromResult(Result.Failure(Error.Unauthorized("Not authenticated.")));
}

// ---- Change own password (requires the current one) ----
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<Result>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a non-alphanumeric character.")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must differ from the current one.");
    }
}

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IIdentityService _identity;
    private readonly ICurrentUserService _currentUser;

    public ChangePasswordCommandHandler(IIdentityService identity, ICurrentUserService currentUser)
    {
        _identity = identity;
        _currentUser = currentUser;
    }

    public Task<Result> Handle(ChangePasswordCommand request, CancellationToken ct)
        => _currentUser.UserId is { } userId
            ? _identity.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct)
            : Task.FromResult(Result.Failure(Error.Unauthorized("Not authenticated.")));
}
