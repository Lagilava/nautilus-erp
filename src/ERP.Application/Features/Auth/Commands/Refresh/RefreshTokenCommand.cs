using ERP.Application.Common.Models;
using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.Refresh;

/// <summary>Exchanges a valid refresh token for a new token pair, rotating the refresh token.</summary>
public sealed record RefreshTokenCommand(string RefreshToken)
    : IRequest<Result<AuthenticationResult>>;
