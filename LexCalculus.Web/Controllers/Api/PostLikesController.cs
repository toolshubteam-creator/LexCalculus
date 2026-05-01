using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Controllers.Api;

/// <summary>
/// Beğeni toggle AJAX endpoint'i. CSRF: X-CSRF-TOKEN header. Notification YOK
/// (charter Karar 22 — like sessiz). Faz 4.9 P2.
/// </summary>
[ApiController]
[Route("api/post-likes")]
[Authorize]
public sealed class PostLikesController : ControllerBase
{
    private readonly IPostLikeService _likes;
    private readonly UserManager<ApplicationUser> _userManager;

    public PostLikesController(
        IPostLikeService likes,
        UserManager<ApplicationUser> userManager)
    {
        _likes = likes;
        _userManager = userManager;
    }

    [HttpPost("toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle([FromBody] TogglePostLikeRequest? req,
        CancellationToken ct = default)
    {
        if (req is null || req.PostId <= 0)
            return BadRequest(new { error = "Geçersiz istek." });

        var raw = _userManager.GetUserId(User);
        if (!int.TryParse(raw, out var uid))
            return Unauthorized();

        var result = await _likes.ToggleAsync(req.PostId, uid, ct);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage ?? "İşlem başarısız." });

        return Ok(new { isLiked = result.IsLiked, likeCount = result.LikeCount });
    }
}

public sealed record TogglePostLikeRequest(int PostId);
