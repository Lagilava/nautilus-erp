using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ERP.Infrastructure.Notifications;

/// <summary>
/// SignalR hub clients connect to for real-time notifications. Authenticated only; each
/// connection is added to a per-user group so notifications can target a single user.
/// Clients handle the "notification" method.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public const string ClientMethod = "notification";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId));
        await base.OnConnectedAsync();
    }

    public static string GroupFor(string userId) => $"user:{userId}";
}
