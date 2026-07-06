using ERP.Application.Common.Models;
using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Queries.GetCurrentUser;

/// <summary>Returns the profile of the currently authenticated caller.</summary>
public sealed record GetCurrentUserQuery : IRequest<Result<UserIdentity>>;
