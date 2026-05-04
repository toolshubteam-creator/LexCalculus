using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LexCalculus.Web.SignalR;

/// <summary>
/// Mesajlaşma için SignalR Hub. Cookie auth (charter §3 Karar 8) — anonim
/// negotiate 401. Hub method tanımı YOK; gönderme REST endpoint üzerinden,
/// Hub yalnızca server → client broadcast kanalı.
/// OnConnected: kullanıcıyı user-{userId} grubuna ekler (multi-tab destekli;
/// her connection ayrı ConnectionId, hepsi gruba dahil).
/// Faz 5.6.
/// </summary>
[Authorize]
public sealed class MessagesHub : Hub
{
    /// <summary>Group ismini userId'den üreten yardımcı (server tarafı senkron).</summary>
    public static string GroupName(int userId) => $"user-{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
        }
        await base.OnDisconnectedAsync(exception);
    }
}
