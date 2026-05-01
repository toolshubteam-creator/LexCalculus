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
/// /makalelerim/duzenle/{id} — mevcut makaleyi düzenleme formu. Yetki: sadece
/// sahip. Faz 4.6 P3/3. Üç POST handler:
///   - Save: durum korunur (yayında ise yayında kalır)
///   - SaveDraft: taslağa al (eğer yayındaysa unpublish)
///   - Publish: yayınla (eğer taslaksa publish)
/// </summary>
[Authorize]
public sealed class MakaleDuzenleModel : PageModel
{
    private readonly IUserPostService _posts;
    private readonly IPostCategoryService _categories;
    private readonly IMediaUploadService _mediaUpload;
    private readonly UserManager<ApplicationUser> _userManager;

    public MakaleDuzenleModel(
        IUserPostService posts,
        IPostCategoryService categories,
        IMediaUploadService mediaUpload,
        UserManager<ApplicationUser> userManager)
    {
        _posts = posts;
        _categories = categories;
        _mediaUpload = mediaUpload;
        _userManager = userManager;
    }

    [BindProperty] public MakaleYeniModel.MakaleInputModel Input { get; set; } = new();
    public IReadOnlyList<PostCategory> Categories { get; private set; } = Array.Empty<PostCategory>();
    public bool IsAlreadyPublished { get; private set; }
    public int PostId { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var uid)) return Challenge();

        var post = await _posts.GetByIdAsync(id, ct);
        if (post is null || post.UserId != uid) return NotFound();

        PostId = post.Id;
        IsAlreadyPublished = post.IsPublished;
        Categories = await _categories.GetActiveAsync(ct);

        Input.Title = post.Title;
        Input.CategoryId = post.CategoryId;
        Input.Body = post.Body;
        Input.FeaturedImageUrl = post.FeaturedImageUrl;
        Input.TagsCsv = string.Join(", ", post.TagLinks.Select(l => l.Tag.Name));

        return Page();
    }

    public Task<IActionResult> OnPostSaveAsync(int id, CancellationToken ct = default)
        => SaveAsync(id, publish: null, ct);

    public Task<IActionResult> OnPostSaveDraftAsync(int id, CancellationToken ct = default)
        => SaveAsync(id, publish: false, ct);

    public Task<IActionResult> OnPostPublishAsync(int id, CancellationToken ct = default)
        => SaveAsync(id, publish: true, ct);

    private async Task<IActionResult> SaveAsync(int id, bool? publish, CancellationToken ct)
    {
        if (!TryGetUserId(out var uid)) return Challenge();

        var post = await _posts.GetByIdAsync(id, ct);
        if (post is null || post.UserId != uid) return NotFound();

        Categories = await _categories.GetActiveAsync(ct);
        IsAlreadyPublished = post.IsPublished;
        PostId = id;

        if (!ModelState.IsValid) return Page();

        string? featuredUrl = Input.FeaturedImageUrl;
        if (Input.FeaturedImageFile is { Length: > 0 } file)
        {
            await using var s = file.OpenReadStream();
            var upload = await _mediaUpload.UploadFeaturedImageAsync(
                uid, s, file.FileName, file.ContentType, file.Length, ct);
            if (!upload.Success)
            {
                ModelState.AddModelError("Input.FeaturedImageFile",
                    upload.ErrorMessage ?? "Yükleme başarısız.");
                return Page();
            }
            featuredUrl = upload.RelativePath;
        }

        var tagNames = MakaleYeniModel.ParseTags(Input.TagsCsv);

        var input = new UserPostInput(
            Input.Title.Trim(),
            Input.Body,
            Input.CategoryId,
            featuredUrl,
            tagNames);

        var update = await _posts.UpdateAsync(id, uid, input, ct);
        if (!update.Success)
        {
            ModelState.AddModelError(string.Empty, update.ErrorMessage ?? "Güncelleme başarısız.");
            return Page();
        }

        // Durum geçişleri
        if (publish == true && !IsAlreadyPublished)
        {
            await _posts.PublishAsync(id, uid, ct);
            TempData["Success"] = "Makale yayınlandı.";
            return RedirectToPage("/Makalelerim", new { tab = "yayinda" });
        }
        if (publish == false && IsAlreadyPublished)
        {
            await _posts.UnpublishAsync(id, uid, ct);
            TempData["Success"] = "Makale taslağa alındı.";
            return RedirectToPage("/Makalelerim", new { tab = "taslak" });
        }

        TempData["Success"] = "Değişiklikler kaydedildi.";
        return RedirectToPage("/Makalelerim",
            new { tab = IsAlreadyPublished ? "yayinda" : "taslak" });
    }

    private bool TryGetUserId(out int userId)
    {
        var raw = _userManager.GetUserId(User);
        return int.TryParse(raw, out userId);
    }
}
