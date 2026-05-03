using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Moderation;
using LexCalculus.Core.Extensions;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Web.Areas.Admin.Models.ContentReports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin moderasyon paneli — bekleyen şikayetler, içerik incelemesi, dismiss/action.
/// Kullanıcı şikayet AJAX endpoint'i Controllers/Api/ContentReportsController'da
/// (P1). Bu controller P2: admin UI. Faz 4.10 P2.
/// </summary>
[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
[Route("admin/sikayetler")]
public sealed class ContentReportsController : Controller
{
    private readonly IContentReportService _reportService;
    private readonly ApplicationDbContext _ctx;
    private readonly UserManager<ApplicationUser> _userManager;

    public ContentReportsController(
        IContentReportService reportService,
        ApplicationDbContext ctx,
        UserManager<ApplicationUser> userManager)
    {
        _reportService = reportService;
        _ctx = ctx;
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var groups = await _reportService.GetPendingGroupedAsync(ct);

        var items = new List<ContentReportListItem>(groups.Count);
        foreach (var g in groups)
        {
            items.Add(new ContentReportListItem
            {
                TargetType = g.TargetType,
                TargetId = g.TargetId,
                ReportCount = g.ReportCount,
                LatestReportAt = g.LatestReportAt,
                TargetTitle = g.TargetTitle ?? "(başlık yok)",
                AuthorDisplayName = g.AuthorDisplayName ?? "(yazar bulunamadı)",
                TargetUrl = await BuildTargetUrlAsync(g.TargetType, g.TargetId, ct)
            });
        }

        var vm = new ContentReportListVm
        {
            Items = items,
            TotalPending = await _reportService.GetPendingCountAsync(ct)
        };

        ViewData["Title"] = "Şikayetler";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Şikayetler", null)
        };

        return View(vm);
    }

    [HttpGet("{type:int}/{id:int}")]
    public async Task<IActionResult> Detail(int type, int id, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(typeof(ContentReportTargetType), type))
            return NotFound();

        var targetType = (ContentReportTargetType)type;
        var reports = await _reportService.GetByTargetAsync(targetType, id, ct);
        if (reports.Count == 0) return NotFound();

        var vm = await BuildDetailVmAsync(targetType, id, reports, ct);

        ViewData["Title"] = "Şikayet İncelemesi";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Şikayetler", Url.Action(nameof(Index))),
            ("İnceleme", null)
        };

        return View(vm);
    }

    [HttpPost("{type:int}/{id:int}/dismiss")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(
        int type, int id, string? reviewNote, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(typeof(ContentReportTargetType), type))
            return NotFound();

        var targetType = (ContentReportTargetType)type;
        var raw = _userManager.GetUserId(User);
        if (!int.TryParse(raw, out var adminId))
            return Unauthorized();

        var result = await _reportService.DismissAsync(targetType, id, adminId, reviewNote, ct);

        if (result.Success)
            TempData["AdminSuccess"] = "✓ Şikayetler reddedildi.";
        else
            TempData["AdminError"] = result.ErrorMessage ?? "İşlem başarısız.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{type:int}/{id:int}/action")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Action(
        int type, int id, string? reviewNote, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(typeof(ContentReportTargetType), type))
            return NotFound();

        var targetType = (ContentReportTargetType)type;
        var raw = _userManager.GetUserId(User);
        if (!int.TryParse(raw, out var adminId))
            return Unauthorized();

        var result = await _reportService.ActionAsync(targetType, id, adminId, reviewNote, ct);

        if (result.Success)
            TempData["AdminSuccess"] = "✓ İçerik kaldırıldı.";
        else
            TempData["AdminError"] = result.ErrorMessage ?? "İşlem başarısız.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{type:int}/{id:int}/hide")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hide(
        int type, int id, string? reviewNote, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(typeof(ContentReportTargetType), type))
            return NotFound();

        var targetType = (ContentReportTargetType)type;
        var raw = _userManager.GetUserId(User);
        if (!int.TryParse(raw, out var adminId))
            return Unauthorized();

        var result = await _reportService.HideAsync(targetType, id, adminId, reviewNote, ct);

        if (result.Success)
            TempData["AdminSuccess"] = "✓ İçerik gizlendi. Sahip ve admin görür; geri alınabilir.";
        else
            TempData["AdminError"] = result.ErrorMessage ?? "İşlem başarısız.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("gizlenenler")]
    public async Task<IActionResult> Hidden(CancellationToken ct = default)
    {
        var hidden = await _reportService.GetHiddenContentAsync(ct);

        ViewData["Title"] = "Gizlenen İçerikler";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Şikayetler", Url.Action(nameof(Index))),
            ("Gizlenenler", null)
        };

        return View("Hidden", hidden);
    }

    [HttpPost("{type:int}/{id:int}/unhide")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unhide(
        int type, int id, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(typeof(ContentReportTargetType), type))
            return NotFound();

        var targetType = (ContentReportTargetType)type;
        var raw = _userManager.GetUserId(User);
        if (!int.TryParse(raw, out var adminId))
            return Unauthorized();

        var result = await _reportService.UnhideAsync(targetType, id, adminId, ct);

        if (result.Success)
            TempData["AdminSuccess"] = "✓ İçerik geri yüklendi.";
        else
            TempData["AdminError"] = result.ErrorMessage ?? "İşlem başarısız.";

        return RedirectToAction(nameof(Hidden));
    }

    // ─── helpers ──────────────────────────────────────────────────────────

    private async Task<string?> BuildTargetUrlAsync(
        ContentReportTargetType targetType, int targetId, CancellationToken ct)
    {
        if (targetType == ContentReportTargetType.Post)
        {
            var post = await _ctx.UserPosts
                .Where(p => p.Id == targetId)
                .Select(p => new
                {
                    p.Slug,
                    AuthorSlug = p.User.Profile != null ? p.User.Profile.PublicSlug : null
                })
                .FirstOrDefaultAsync(ct);
            if (post is null || string.IsNullOrEmpty(post.AuthorSlug)) return null;
            return $"/uye/{post.AuthorSlug}/makale/{post.Slug}";
        }

        var comment = await _ctx.PostComments
            .Where(c => c.Id == targetId)
            .Select(c => new
            {
                PostSlug = c.Post.Slug,
                AuthorSlug = c.Post.User.Profile != null ? c.Post.User.Profile.PublicSlug : null,
                CommentId = c.Id
            })
            .FirstOrDefaultAsync(ct);
        if (comment is null || string.IsNullOrEmpty(comment.AuthorSlug)) return null;
        return $"/uye/{comment.AuthorSlug}/makale/{comment.PostSlug}#yorum-{comment.CommentId}";
    }

    private async Task<ContentReportDetailVm> BuildDetailVmAsync(
        ContentReportTargetType targetType, int targetId,
        IReadOnlyList<ContentReport> reports, CancellationToken ct)
    {
        var vm = new ContentReportDetailVm
        {
            TargetType = targetType,
            TargetId = targetId,
            Reports = reports.Select(r => new ContentReportListReport
            {
                Id = r.Id,
                ReporterDisplayName = r.Reporter.GetDisplayNameOrAnonymized(),
                Reason = r.Reason,
                ReasonLabel = ReasonToLabel(r.Reason),
                Note = r.Note,
                CreatedAt = r.CreatedAt
            }).ToList()
        };

        if (targetType == ContentReportTargetType.Post)
        {
            var post = await _ctx.UserPosts
                .Include(p => p.User).ThenInclude(u => u!.Profile)
                .FirstOrDefaultAsync(p => p.Id == targetId, ct);
            if (post is not null)
            {
                vm.TargetTitle = post.Title;
                vm.TargetBodyHtml = post.Body;
                vm.TargetCreatedAt = post.CreatedAt;
                vm.AuthorDisplayName = post.User?.Profile?.DisplayName
                    ?? post.User?.UserName ?? "";
                vm.AuthorSlug = post.User?.Profile?.PublicSlug ?? "";
                vm.TargetPublicUrl = !string.IsNullOrEmpty(vm.AuthorSlug)
                    ? $"/uye/{vm.AuthorSlug}/makale/{post.Slug}"
                    : null;
            }
            else
            {
                vm.TargetTitle = "(makale silinmiş)";
            }
        }
        else
        {
            var comment = await _ctx.PostComments
                .Include(c => c.User).ThenInclude(u => u!.Profile)
                .Include(c => c.Post).ThenInclude(p => p.User).ThenInclude(u => u!.Profile)
                .FirstOrDefaultAsync(c => c.Id == targetId, ct);
            if (comment is not null)
            {
                vm.TargetTitle = "Yorum: " + (comment.Body.Length > 50
                    ? comment.Body.Substring(0, 50) + "..."
                    : comment.Body);
                vm.TargetBodyHtml = comment.Body;
                vm.TargetCreatedAt = comment.CreatedAt;
                vm.AuthorDisplayName = comment.User?.Profile?.DisplayName
                    ?? comment.User?.UserName ?? "";
                vm.AuthorSlug = comment.User?.Profile?.PublicSlug ?? "";
                var postAuthorSlug = comment.Post?.User?.Profile?.PublicSlug;
                vm.TargetPublicUrl = !string.IsNullOrEmpty(postAuthorSlug) && comment.Post is not null
                    ? $"/uye/{postAuthorSlug}/makale/{comment.Post.Slug}#yorum-{comment.Id}"
                    : null;
            }
            else
            {
                vm.TargetTitle = "(yorum silinmiş)";
            }
        }

        return vm;
    }

    private static string ReasonToLabel(ContentReportReason reason) => reason switch
    {
        ContentReportReason.Spam => "Spam",
        ContentReportReason.Harassment => "Taciz",
        ContentReportReason.Misleading => "Yanıltıcı",
        ContentReportReason.Legal => "Hukuki",
        ContentReportReason.Obscene => "Müstehcen",
        ContentReportReason.Other => "Diğer",
        _ => "Bilinmeyen"
    };
}
