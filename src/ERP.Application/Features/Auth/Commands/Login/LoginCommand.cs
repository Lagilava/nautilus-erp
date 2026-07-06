using ERP.Application.Common.Models;
using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.Login;

/// <summary>Authenticates by email + password, returning a token pair on success.</summary>
public sealed record LoginCommand(string Email, string Password)
    : IRequest<Result<AuthenticationResult>>;
