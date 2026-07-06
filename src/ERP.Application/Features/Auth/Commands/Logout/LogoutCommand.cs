using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.Logout;

/// <summary>Revokes a refresh token so it can no longer be used to obtain access tokens.</summary>
public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;
