#nullable enable

using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LexCalculus.Web.Pages;

/// <summary>
/// /makalelerim — kullanıcının kendi makale yönetim sayfası. 2 sekme:
/// Yayında (default) + Taslak. Yeni makale + düzenle Adım 4.6 P3'te
/// (Quill editor entegrasyonu) eklenecek. Faz 4.6 P2/3.
/// </summary>
[Authorize]
public sealed class MakalelerimModel : PageModel
{
    private readonly IUserPostService _posts;
    private readonly UserManager<ApplicationUser> _userManager;

    public MakalelerimModel(
        IUserPostService posts,
        UserManager<ApplicationUser> userManager)
    {
        _posts = posts;
        _userManager = userManager;
    }

    public string ActiveTab { get; private set; } = "yayinda";
    public IReadOnlyList<UserPost> Posts { get; private set; } = Array.Empty<UserPost>();
    public int PublishedCount { get; private set; }
    public int DraftCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? tab, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var uid)) return Challenge();

        ActiveTab = tab == "taslak" ? "taslak" : "yayinda";

        var allPosts = await _posts.GetByUserIdAsync(uid, includeUnpublished: true, ct);

        PublishedCount = allPosts.Count(p => p.IsPublished);
        DraftCount = allPosts.Count - PublishedCount;

        Posts = ActiveTab == "taslak"
            ? allPosts.Where(p => !p.IsPublished).ToList()
            : allPosts.Where(p => p.IsPublished).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostPublishAsync(int id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var uid)) return Challenge();
        var result = await _posts.PublishAsync(id, uid, ct);
        SetMessage(result, success: "Makale yayınlandı.");
        return RedirectToPage(new { tab = "yayinda" });
    }

    public async Task<IActionResult> OnPostUnpublishAsync(int id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var uid)) return Challenge();
        var result = await _posts.UnpublishAsync(id, uid, ct);
        SetMessage(result, success: "Makale yayından kaldırıldı.");
        return RedirectToPage(new { tab = "taslak" });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, string? returnTab, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var uid)) return Challenge();
        var result = await _posts.DeleteAsync(id, uid, ct);
        SetMessage(result, success: "Makale silindi.");
        var tab = returnTab == "taslak" ? "taslak" : "yayinda";
        return RedirectToPage(new { tab });
    }

    private bool TryGetUserId(out int userId)
    {
        var raw = _userManager.GetUserId(User);
        return int.TryParse(raw, out userId);
    }

    private void SetMessage(UserPostResult result, string success)
    {
        if (result.Success)
            TempData["Success"] = success;
        else
            TempData["Error"] = result.ErrorMessage ?? "İşlem başarısız oldu.";
    }
}
