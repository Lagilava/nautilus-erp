using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace ERP.Infrastructure.Notifications;

/// <summary>Publishes notifications to connected SignalR clients.</summary>
public sealed class SignalRNotificationPublisher : IRealtimeNotifier
{
    private readonly IHubContext<NotificationsHub> _hub;

    public SignalRNotificationPublisher(IHubContext<NotificationsHub> hub) => _hub = hub;

    public Task PublishToAllAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        => _hub.Clients.All.SendAsync(NotificationsHub.ClientMethod, message, cancellationToken);

    public Task PublishToUserAsync(Guid userId, NotificationMessage message, CancellationToken cancellationToken = default)
        => _hub.Clients.Group(NotificationsHub.GroupFor(userId.ToString()))
            .SendAsync(NotificationsHub.ClientMethod, message, cancellationToken);
}
