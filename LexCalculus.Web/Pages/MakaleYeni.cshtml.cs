#nullable enable

using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LexCalculus.Web.Pages;

/// <summary>
/// /makalelerim/yeni — yeni makale oluşturma formu. Quill editor + tag chip +
/// featured image. Faz 4.6 P3/3. İki POST handler: SaveDraft, Publish.
/// </summary>
[Authorize]
public sealed class MakaleYeniModel : PageModel
{
    private readonly IUserPostService _posts;
    private readonly IPostCategoryService _categories;
    private readonly IMediaUploadService _mediaUpload;
    private readonly UserManager<ApplicationUser> _userManager;

    public MakaleYeniModel(
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

    [BindProperty] public MakaleInputModel Input { get; set; } = new();
    public IReadOnlyList<PostCategory> Categories { get; private set; } = Array.Empty<PostCategory>();

    public sealed class MakaleInputModel
    {
        [Required(ErrorMessage = "Başlık zorunludur.")]
        [StringLength(200)]
        [Display(Name = "Başlık")]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "Kategori seçmelisiniz.")]
        [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir kategori seçin.")]
        [Display(Name = "Kategori")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "İçerik boş olamaz.")]
        [Display(Name = "İçerik")]
        public string Body { get; set; } = "";

        [Display(Name = "Etiketler")]
        public string? TagsCsv { get; set; }

        public IFormFile? FeaturedImageFile { get; set; }

        public string? FeaturedImageUrl { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        Categories = await _categories.GetActiveAsync(ct);
        return Page();
    }

    public Task<IActionResult> OnPostSaveDraftAsync(CancellationToken ct = default)
        => SaveAsync(publish: false, ct);

    public Task<IActionResult> OnPostPublishAsync(CancellationToken ct = default)
        => SaveAsync(publish: true, ct);

    private async Task<IActionResult> SaveAsync(bool publish, CancellationToken ct)
    {
        Categories = await _categories.GetActiveAsync(ct);

        if (!ModelState.IsValid) return Page();

        var raw = _userManager.GetUserId(User);
        if (!int.TryParse(raw, out var uid)) return Challenge();

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

        var tagNames = ParseTags(Input.TagsCsv);

        var input = new UserPostInput(
            Input.Title.Trim(),
            Input.Body,
            Input.CategoryId,
            featuredUrl,
            tagNames);

        var draft = await _posts.CreateDraftAsync(uid, input, ct);
        if (!draft.Success)
        {
            ModelState.AddModelError(string.Empty, draft.ErrorMessage ?? "Kaydetme başarısız.");
            return Page();
        }

        if (publish)
        {
            var pub = await _posts.PublishAsync(draft.Post!.Id, uid, ct);
            if (!pub.Success)
            {
                TempData["Error"] = "Makale taslak kaydedildi ama yayınlanamadı: " +
                    (pub.ErrorMessage ?? "Bilinmeyen hata.");
                return RedirectToPage("/Makalelerim", new { tab = "taslak" });
            }
            TempData["Success"] = "Makale yayınlandı.";
            return RedirectToPage("/Makalelerim", new { tab = "yayinda" });
        }

        TempData["Success"] = "Makale taslak olarak kaydedildi.";
        return RedirectToPage("/Makalelerim", new { tab = "taslak" });
    }

    internal static IReadOnlyList<string> ParseTags(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<string>();
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(5)
            .ToList();
    }
}
