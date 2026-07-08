using System.Net;
using ERP.Application.Common.Interfaces;
using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly IIdentityService _identity;
    private readonly IEmailQueue _email;
    private readonly IAppUrls _urls;

    public ForgotPasswordCommandHandler(IIdentityService identity, IEmailQueue email, IAppUrls urls)
    {
        _identity = identity;
        _email = email;
        _urls = urls;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var tokenResult = await _identity.GeneratePasswordResetTokenAsync(request.Email, cancellationToken);

        // Only send when the account exists — but the caller can't tell the difference,
        // because we always return the same successful result.
        if (tokenResult.IsSuccess)
        {
            var link = $"{_urls.ClientBaseUrl}/reset-password" +
                       $"?email={WebUtility.UrlEncode(request.Email)}" +
                       $"&token={WebUtility.UrlEncode(tokenResult.Value)}";

            _email.Enqueue(new EmailMessage(
                request.Email,
                "Reset your Nautilus ERP password",
                $"A password reset was requested for your account.\n\n{link}\n\n" +
                "If you did not request this, you can safely ignore this email."));
        }

        return Result.Success();
    }
}
