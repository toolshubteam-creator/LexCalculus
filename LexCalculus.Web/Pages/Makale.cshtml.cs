#nullable enable

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Extensions;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Core.Services;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LexCalculus.Web.Pages;

/// <summary>
/// /uye/{userSlug}/makale/{postSlug} — public makale görüntüleme sayfası.
/// AllowAnonymous. Yayında değilse sahip görür (preview banner + NoIndex),
/// başkaları 404. Faz 4.7. Charter §3.3 (Article JSON-LD), Karar 8 (disclaimer),
/// Karar 11 (slug).
/// </summary>
[AllowAnonymous]
public sealed class MakaleModel : PageModel
{
    private readonly ApplicationDbContext _ctx;
    private readonly IMediaStorage _storage;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPostCommentService _comments;
    private readonly IPostLikeService _likes;
    private readonly SeoSettings _seo;

    public MakaleModel(
        ApplicationDbContext ctx,
        IMediaStorage storage,
        UserManager<ApplicationUser> userManager,
        IPostCommentService comments,
        IPostLikeService likes,
        IOptions<SeoSettings> seoOptions)
    {
        _ctx = ctx;
        _storage = storage;
        _userManager = userManager;
        _comments = comments;
        _likes = likes;
        _seo = seoOptions.Value ?? new SeoSettings();
    }

    public UserPost Post { get; private set; } = null!;
    public string AuthorDisplayName { get; private set; } = "";
    public string? AuthorSlug { get; private set; }
    public string? AuthorAvatarUrl { get; private set; }
    public string? AuthorBio { get; private set; }
    public bool AuthorIsPublic { get; private set; }
    public string? FeaturedImageAbsoluteUrl { get; private set; }
    public string CanonicalUrl { get; private set; } = "";

    /// <summary>Sahip kendi taslağını ön izliyor.</summary>
    public bool IsOwnerPreview { get; private set; }

    /// <summary>
    /// İçerik admin tarafından gizlenmiş; sahip veya admin "Gizlendi" banner'lı
    /// önizleme görüyor (Faz 5.3 Karar 11).
    /// </summary>
    public bool IsHiddenPreview { get; private set; }

    // Faz 4.9 P2 — yorum + beğeni
    public IReadOnlyList<PostCommentViewModel> Comments { get; private set; }
        = Array.Empty<PostCommentViewModel>();
    public int CommentCount { get; private set; }
    public int LikeCount { get; private set; }
    public bool IsLikedByViewer { get; private set; }
    public bool ViewerCanComment { get; private set; }

    // Faz 4.10 P1 — şikayet
    public int? ViewerId { get; private set; }
    public bool CanReportPost { get; private set; }

    public async Task<IActionResult> OnGetAsync(string userSlug, string postSlug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userSlug) || string.IsNullOrWhiteSpace(postSlug))
            return NotFound();

        var author = await _ctx.UserProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PublicSlug == userSlug, ct);
        if (author is null || author.User is null || !author.User.IsActive)
            return NotFound();

        var post = await _ctx.UserPosts
            .Include(p => p.Category)
            .Include(p => p.TagLinks).ThenInclude(l => l.Tag)
            .FirstOrDefaultAsync(p => p.UserId == author.UserId && p.Slug == postSlug, ct);
        if (post is null) return NotFound();

        var viewerIdRaw = _userManager.GetUserId(User);
        var viewerId = int.TryParse(viewerIdRaw, out var vid) ? (int?)vid : null;
        ViewerId = viewerId;

        IsOwnerPreview = !post.IsPublished && viewerId == post.UserId;

        if (!post.IsPublished && !IsOwnerPreview)
            return NotFound();

        // Faz 5.3 — Hide moderation: sahip + admin görür (banner ile), diğerleri 404.
        var isAdmin = User.IsInRole("Admin");
        var isOwner = viewerId.HasValue && viewerId.Value == post.UserId;
        if (post.IsModeratorHidden && !isOwner && !isAdmin)
            return NotFound();
        IsHiddenPreview = post.IsModeratorHidden && (isOwner || isAdmin);

        // ViewCount: sadece yayında, gizlenmemiş, sahip dışı görüntüleyiciler için
        if (post.IsPublished && !post.IsModeratorHidden && viewerId != post.UserId)
        {
            post.ViewCount += 1;
            await _ctx.SaveChangesAsync(ct);
        }

        Post = post;
        AuthorDisplayName = string.IsNullOrWhiteSpace(author.DisplayName)
            ? (author.User.UserName ?? "Kullanıcı")
            : author.DisplayName;
        AuthorSlug = author.PublicSlug;
        AuthorAvatarUrl = string.IsNullOrEmpty(author.AvatarUrl)
            ? null
            : _storage.GetPublicUrl(author.AvatarUrl);
        AuthorBio = author.IsPublicProfile ? author.Bio : null;
        AuthorIsPublic = author.IsPublicProfile;

        FeaturedImageAbsoluteUrl = string.IsNullOrEmpty(post.FeaturedImageUrl)
            ? null
            : ToAbsolute(_storage.GetPublicUrl(post.FeaturedImageUrl));

        var siteUrl = (_seo.SiteUrl ?? "").TrimEnd('/');
        CanonicalUrl = $"{siteUrl}/uye/{userSlug}/makale/{postSlug}";

        if (IsOwnerPreview || IsHiddenPreview)
            ViewData["NoIndex"] = true;

        // Faz 4.9 P2 — yorum + beğeni state
        // Faz 5.3 — hidden post'ta yorum/beğeni etkileşimi kapalı (sahip+admin sadece okur).
        if (post.IsPublished && !post.IsModeratorHidden)
        {
            CommentCount = await _comments.GetCountForPostAsync(post.Id, ct);
            LikeCount = await _likes.GetCountForPostAsync(post.Id, ct);
            IsLikedByViewer = viewerId.HasValue
                && await _likes.IsLikedByAsync(post.Id, viewerId.Value, ct);
            ViewerCanComment = viewerId.HasValue;

            // Faz 4.10 P1 — şikayet linki: login + sahip değil
            CanReportPost = viewerId.HasValue && viewerId.Value != post.UserId;

            var commentEntities = await _comments.GetByPostIdAsync(post.Id, includeHidden: isAdmin, ct);
            var postOwnerId = post.UserId;
            Comments = commentEntities.Select(c => new PostCommentViewModel
            {
                Id = c.Id,
                PostId = c.PostId,
                Body = c.Body,
                CreatedAt = c.CreatedAt,
                IsEdited = c.IsEdited,
                AuthorDisplayName = c.User.GetDisplayNameOrAnonymized(),
                AuthorSlug = c.User.GetPublicSlugOrNull(),
                AuthorAvatarUrl = c.User.IsAnonymizedOrInactive() || string.IsNullOrEmpty(c.User?.Profile?.AvatarUrl)
                    ? null
                    : _storage.GetPublicUrl(c.User.Profile.AvatarUrl),
                CanEdit = viewerId.HasValue && c.UserId == viewerId.Value,
                CanDelete = viewerId.HasValue && (
                    c.UserId == viewerId.Value
                    || postOwnerId == viewerId.Value
                    || isAdmin),
                CanReport = viewerId.HasValue && c.UserId != viewerId.Value
            }).ToList();
        }

        ViewData["Title"] = $"{post.Title} — {AuthorDisplayName}";

        var description = GetDescription(160);
        var meta = new SeoMeta
        {
            Title = $"{post.Title} — {AuthorDisplayName}",
            Description = description,
            CanonicalUrl = CanonicalUrl,
            OgType = "article",
            OgImage = FeaturedImageAbsoluteUrl,
            // Featured image yoksa twitter:card 'summary' (default summary_large_image yerine)
            TwitterCard = string.IsNullOrEmpty(FeaturedImageAbsoluteUrl) ? "summary" : "summary_large_image"
        };

        // JSON-LD yalnızca yayında — preview taslağında üretmiyoruz
        if (!IsOwnerPreview)
            meta.JsonLdBlocks.Add(GetArticleJsonLd());

        ViewData["PageMeta"] = meta;

        return Page();
    }

    /// <summary>Body'den HTML strip + truncate (description için).</summary>
    public string GetDescription(int maxLen)
    {
        var raw = Post.Body ?? string.Empty;
        var text = Regex.Replace(raw, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length <= maxLen) return text;
        return text.Substring(0, maxLen).TrimEnd() + "…";
    }

    /// <summary>
    /// Schema.org Article JSON-LD üretir. KVKK: yazarın e-posta/telefon/baroNo
    /// hiçbir koşulda dahil edilmez.
    /// </summary>
    public string GetArticleJsonLd()
    {
        var siteUrl = (_seo.SiteUrl ?? "").TrimEnd('/');

        var data = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Article",
            ["headline"] = Post.Title,
            ["description"] = GetDescription(160),
            ["url"] = CanonicalUrl,
            ["datePublished"] = (Post.PublishedAt ?? Post.CreatedAt).ToString("o"),
            ["dateModified"] = Post.UpdatedAt.ToString("o")
        };

        var authorBlock = new Dictionary<string, object>
        {
            ["@type"] = "Person",
            ["name"] = AuthorDisplayName
        };
        if (!string.IsNullOrEmpty(AuthorSlug) && AuthorIsPublic)
            authorBlock["url"] = $"{siteUrl}/uye/{AuthorSlug}";
        data["author"] = authorBlock;

        data["publisher"] = new Dictionary<string, object>
        {
            ["@type"] = "Organization",
            ["name"] = "Lex Calculus"
        };

        if (Post.Category is not null)
            data["articleSection"] = Post.Category.Name;

        if (!string.IsNullOrEmpty(FeaturedImageAbsoluteUrl))
            data["image"] = FeaturedImageAbsoluteUrl;

        if (Post.TagLinks.Count > 0)
            data["keywords"] = string.Join(", ", Post.TagLinks.Select(l => l.Tag.Name));

        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        });
    }

    private string ToAbsolute(string relativeOrAbsolute)
    {
        if (relativeOrAbsolute.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return relativeOrAbsolute;
        var siteUrl = (_seo.SiteUrl ?? "").TrimEnd('/');
        if (string.IsNullOrEmpty(siteUrl)) return relativeOrAbsolute;
        var path = relativeOrAbsolute.StartsWith('/') ? relativeOrAbsolute : "/" + relativeOrAbsolute;
        return siteUrl + path;
    }
}
