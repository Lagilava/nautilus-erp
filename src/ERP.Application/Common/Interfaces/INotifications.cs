namespace ERP.Application.Common.Interfaces;

/// <summary>A real-time notification pushed to connected clients.</summary>
public sealed record NotificationMessage(string Title, string Message, string Level = "info");

/// <summary>An email to be sent (asynchronously, via the queue).</summary>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>
/// Publishes real-time notifications to clients. Implemented over SignalR in Infrastructure.
/// </summary>
public interface IRealtimeNotifier
{
    Task PublishToAllAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    Task PublishToUserAsync(Guid userId, NotificationMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Enqueues an email for asynchronous delivery. Implemented over Hangfire in Infrastructure,
/// so the caller returns immediately and delivery is retried out of the request path.
/// </summary>
public interface IEmailQueue
{
    void Enqueue(EmailMessage message);
}

/// <summary>Sends an email. Invoked by the queued background job.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
