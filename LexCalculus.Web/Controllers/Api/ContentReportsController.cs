using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Moderation;
using LexCalculus.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LexCalculus.Web.Controllers.Api;

/// <summary>
/// İçerik şikayet AJAX endpoint'i. POST /api/content-reports/create.
/// CSRF: X-CSRF-TOKEN header (Adım 4.8 pattern). Faz 4.10 P1.
/// </summary>
[ApiController]
[Route("api/content-reports")]
[Authorize]
public sealed class ContentReportsController : ControllerBase
{
    private readonly IContentReportService _reportService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ContentReportsController(
        IContentReportService reportService,
        UserManager<ApplicationUser> userManager)
    {
        _reportService = reportService;
        _userManager = userManager;
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("report")]
    public async Task<IActionResult> Create(
        [FromBody] CreateReportRequest? req, CancellationToken ct = default)
    {
        if (req is null || req.TargetId <= 0)
            return BadRequest(new { error = "Geçersiz istek." });

        if (!Enum.IsDefined(typeof(ContentReportTargetType), req.TargetType))
            return BadRequest(new { error = "Geçersiz hedef tipi." });

        if (!Enum.IsDefined(typeof(ContentReportReason), req.Reason))
            return BadRequest(new { error = "Geçersiz sebep." });

        var raw = _userManager.GetUserId(User);
        if (!int.TryParse(raw, out var uid))
            return Unauthorized();

        var result = await _reportService.CreateAsync(
            (ContentReportTargetType)req.TargetType,
            req.TargetId,
            uid,
            (ContentReportReason)req.Reason,
            req.Note,
            ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage ?? "Şikayet gönderilemedi." });

        return Ok(new { success = true });
    }
}

public sealed record CreateReportRequest(int TargetType, int TargetId, int Reason, string? Note);
