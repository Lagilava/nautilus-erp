using ERP.Application.Common.Interfaces;
using Hangfire;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

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

/// <summary>
/// Sends email over real SMTP via MailKit. Selected instead of <see cref="LoggingEmailSender"/>
/// when "Smtp:Host" is configured — see <see cref="DependencyInjection.AddNotifications"/>.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpSettings> settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };

        using var client = new SmtpClient();
        var socketOptions = _settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

        try
        {
            await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions, cancellationToken);

            if (!string.IsNullOrEmpty(_settings.User))
                await client.AuthenticateAsync(_settings.User, _settings.Password, cancellationToken);

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("EMAIL sent → {To}: {Subject}", message.To, message.Subject);
        }
        catch (Exception ex)
        {
            // Hangfire retries the job on an unhandled exception; rethrow rather than swallow
            // so a transient SMTP outage is retried instead of silently dropping the email.
            _logger.LogError(ex, "EMAIL failed → {To}: {Subject}", message.To, message.Subject);
            throw;
        }
    }
}
