using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Controllers.Api;

/// <summary>
/// Quill editor inline image upload endpoint. AJAX endpoint, CSRF korumalı.
/// Faz 4.8. Charter §3.3 görsel altyapısı.
/// </summary>
[ApiController]
[Route("api/post-images")]
[Authorize]
public sealed class PostImagesController : ControllerBase
{
    private readonly IMediaUploadService _mediaUpload;
    private readonly UserManager<ApplicationUser> _userManager;

    public PostImagesController(
        IMediaUploadService mediaUpload,
        UserManager<ApplicationUser> userManager)
    {
        _mediaUpload = mediaUpload;
        _userManager = userManager;
    }

    [HttpPost("upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Dosya boş veya seçilmedi." });

        var raw = _userManager.GetUserId(User);
        if (!int.TryParse(raw, out var uid))
            return Unauthorized();

        await using var stream = file.OpenReadStream();
        var result = await _mediaUpload.UploadInlineImageAsync(
            uid, stream, file.FileName, file.ContentType, file.Length, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage ?? "Yükleme başarısız." });

        return Ok(new { url = result.RelativePath });
    }
}
