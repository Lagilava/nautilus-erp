using ERP.Application.Common.Models;
using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.Register;

/// <summary>Registers a new user and returns an authenticated token pair.</summary>
public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName) : IRequest<Result<AuthenticationResult>>;
