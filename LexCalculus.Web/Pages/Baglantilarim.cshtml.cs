#nullable enable

using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Services;
using LexCalculus.Core.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LexCalculus.Web.Pages;

/// <summary>
/// /baglantilarim — kullanıcının kendi bağlantı yönetim sayfası. 3 sekme:
/// aktif (Accepted), bekleyen (Pending alıcı), gönderdiklerim (Pending gönderen).
/// Faz 4.2 P2/3 — charter §3.2.
/// </summary>
[Authorize]
public sealed class BaglantilarimModel : PageModel
{
    private readonly IConnectionService _connections;
    private readonly IUserBlockService _blocks;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMediaStorage _storage;

    public BaglantilarimModel(
        IConnectionService connections,
        IUserBlockService blocks,
        UserManager<ApplicationUser> userManager,
        IMediaStorage storage)
    {
        _connections = connections;
        _blocks = blocks;
        _userManager = userManager;
        _storage = storage;
    }

    public string ActiveTab { get; private set; } = "aktif";
    public IReadOnlyList<UserConnection> Connections { get; private set; } = Array.Empty<UserConnection>();
    public IReadOnlyList<UserBlock> BlockedUsers { get; private set; } = Array.Empty<UserBlock>();
    public int ActiveCount { get; private set; }
    public int PendingCount { get; private set; }
    public int SentCount { get; private set; }
    public int BlockedCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? tab, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var uid)) return Challenge();

        ActiveTab = tab switch
        {
            "bekleyen" => "bekleyen",
            "gonderdiklerim" => "gonderdiklerim",
            "engellenenler" => "engellenenler",
            _ => "aktif"
        };

        // Sekme sayıları her zaman çek (badge gösterimi)
        ActiveCount = await _connections.GetConnectionCountAsync(uid, ct);
        var pending = await _connections.GetPendingForUserAsync(uid, ct);
        var sent = await _connections.GetSentByUserAsync(uid, ct);
        PendingCount = pending.Count;
        SentCount = sent.Count;
        BlockedCount = await _blocks.GetBlockedCountAsync(uid, ct);

        if (ActiveTab == "engellenenler")
        {
            BlockedUsers = await _blocks.GetBlockedByUserAsync(uid, ct);
        }
        else
        {
            Connections = ActiveTab switch
            {
                "bekleyen" => pending,
                "gonderdiklerim" => sent,
                _ => await _connections.GetActiveForUserAsync(uid, ct)
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAcceptAsync(int id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var uid)) return Challenge();
        var result = await _connections.AcceptAsync(id, uid, ct);
        SetMessage(result, success: "Bağlantı isteği kabul edildi.");
        return RedirectToPage(new { tab = "bekleyen" });
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var uid)) return Challenge();
        var result = await _connections.RejectAsync(id, uid, ct);
        SetMessage(result, success: "İstek reddedildi.");
        return RedirectToPage(new { tab = "bekleyen" });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var uid)) return Challenge();
        var result = await _connections.CancelAsync(id, uid, ct);
        SetMessage(result, success: "İstek iptal edildi.");
        return RedirectToPage(new { tab = "gonderdiklerim" });
    }

    public async Task<IActionResult> OnPostRemoveAsync(int id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var uid)) return Challenge();
        var result = await _connections.RemoveAsync(id, uid, ct);
        SetMessage(result, success: "Bağlantı kaldırıldı.");
        return RedirectToPage(new { tab = "aktif" });
    }

    // Faz 4.3 — Engellenenler sekmesi unblock handler
    public async Task<IActionResult> OnPostUnblockAsync(int blockedUserId, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var uid)) return Challenge();
        var result = await _blocks.UnblockAsync(uid, blockedUserId, ct);
        if (result.Success)
            TempData["Success"] = "Engelleme kaldırıldı.";
        else
            TempData["Error"] = result.ErrorMessage ?? "İşlem başarısız oldu.";
        return RedirectToPage(new { tab = "engellenenler" });
    }

    /// <summary>View'dan çağrılır — bağlantıdaki "öteki" kullanıcıyı döner.</summary>
    public ApplicationUser GetOtherUser(UserConnection conn)
    {
        if (!TryGetUserId(out var uid))
            return conn.Target;
        return conn.RequesterId == uid ? conn.Target : conn.Requester;
    }

    /// <summary>View'dan çağrılır — avatarın public URL'i (yoksa null).</summary>
    public string? GetAvatarUrl(ApplicationUser user)
    {
        var path = user.Profile?.AvatarUrl;
        return string.IsNullOrEmpty(path) ? null : _storage.GetPublicUrl(path);
    }

    private bool TryGetUserId(out int userId)
    {
        var raw = _userManager.GetUserId(User);
        return int.TryParse(raw, out userId);
    }

    private void SetMessage(ConnectionResult result, string success)
    {
        if (result.Success)
            TempData["Success"] = success;
        else
            TempData["Error"] = result.ErrorMessage ?? "İşlem başarısız oldu.";
    }
}
