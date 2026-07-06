using ERP.Application.Common.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Notifications;

/// <summary>
/// Enqueues emails onto Hangfire so delivery happens on a background worker with retries,
/// off the request thread. The job resolves <see cref="IEmailSender"/> to do the sending.
/// </summary>
public sealed class HangfireEmailQueue : IEmailQueue
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireEmailQueue(IBackgroundJobClient jobs) => _jobs = jobs;

    public void Enqueue(EmailMessage message)
        => _jobs.Enqueue<EmailDispatchJob>(job => job.SendAsync(message));
}

/// <summary>The background job that actually sends a queued email.</summary>
public sealed class EmailDispatchJob
{
    private readonly IEmailSender _sender;
    public EmailDispatchJob(IEmailSender sender) => _sender = sender;

    public Task SendAsync(EmailMessage message) => _sender.SendAsync(message);
}

/// <summary>
/// Stub email sender: logs the email instead of contacting an SMTP server. A real SMTP
/// implementation replaces this via DI — no other code changes.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EMAIL (stub) → {To}: {Subject}", message.To, message.Subject);
        return Task.CompletedTask;
    }
}
