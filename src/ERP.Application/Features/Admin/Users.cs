using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Shared.Authorization;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;

namespace ERP.Application.Features.Admin;

// ---- List users ----
public sealed record GetUsersQuery : IRequest<Result<IReadOnlyList<UserAccount>>>;

public sealed class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, Result<IReadOnlyList<UserAccount>>>
{
    private readonly IIdentityService _identity;
    public GetUsersQueryHandler(IIdentityService identity) => _identity = identity;

    public async Task<Result<IReadOnlyList<UserAccount>>> Handle(GetUsersQuery request, CancellationToken ct)
        => Result.Success(await _identity.GetUsersAsync(ct));
}

// ---- Available roles ----
public sealed record GetRolesQuery : IRequest<Result<IReadOnlyList<string>>>;

public sealed class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, Result<IReadOnlyList<string>>>
{
    public Task<Result<IReadOnlyList<string>>> Handle(GetRolesQuery request, CancellationToken ct)
        => Task.FromResult(Result.Success(Roles.All));
}

// ---- Create user ----
public sealed record CreateUserCommand(
    string Email, string Password, string FirstName, string LastName, IReadOnlyList<string> Roles)
    : IRequest<Result<Guid>>;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Assign at least one role.");
        RuleForEach(x => x.Roles).Must(Roles.All.Contains).WithMessage("Unknown role.");
    }
}

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    private readonly IIdentityService _identity;
    public CreateUserCommandHandler(IIdentityService identity) => _identity = identity;

    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        foreach (var role in request.Roles) await _identity.EnsureRoleAsync(role, ct);

        var result = await _identity.CreateUserAsync(
            request.Email, request.Password, request.FirstName, request.LastName, request.Roles, ct);

        return result.IsSuccess ? Result.Success(result.Value.Id) : Result.Failure<Guid>(result.Error);
    }
}

// ---- Set roles ----
public sealed record SetUserRolesCommand(Guid UserId, IReadOnlyList<string> Roles) : IRequest<Result>;

public sealed class SetUserRolesCommandValidator : AbstractValidator<SetUserRolesCommand>
{
    public SetUserRolesCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Roles).NotEmpty().WithMessage("A user must have at least one role.");
        RuleForEach(x => x.Roles).Must(Roles.All.Contains).WithMessage("Unknown role.");
    }
}

public sealed class SetUserRolesCommandHandler : IRequestHandler<SetUserRolesCommand, Result>
{
    private readonly IIdentityService _identity;
    public SetUserRolesCommandHandler(IIdentityService identity) => _identity = identity;

    public Task<Result> Handle(SetUserRolesCommand request, CancellationToken ct)
        => _identity.SetUserRolesAsync(request.UserId, request.Roles, ct);
}

// ---- Activate / deactivate ----
public sealed record SetUserActiveCommand(Guid UserId, bool IsActive) : IRequest<Result>;

public sealed class SetUserActiveCommandHandler : IRequestHandler<SetUserActiveCommand, Result>
{
    private readonly IIdentityService _identity;
    public SetUserActiveCommandHandler(IIdentityService identity) => _identity = identity;

    public Task<Result> Handle(SetUserActiveCommand request, CancellationToken ct)
        => _identity.SetUserActiveAsync(request.UserId, request.IsActive, ct);
}
