using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Notifications;

namespace ERP.IntegrationTests;

/// <summary>The queued email job delegates to the configured sender.</summary>
public class EmailDispatchJobTests
{
    private sealed class RecordingSender : IEmailSender
    {
        public EmailMessage? Sent { get; private set; }
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Sent = message;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Job_sends_the_message_via_the_sender()
    {
        var sender = new RecordingSender();
        var job = new EmailDispatchJob(sender);

        await job.SendAsync(new EmailMessage("a@b.com", "Hello", "Body"));

        Assert.NotNull(sender.Sent);
        Assert.Equal("a@b.com", sender.Sent!.To);
        Assert.Equal("Hello", sender.Sent.Subject);
    }
}
