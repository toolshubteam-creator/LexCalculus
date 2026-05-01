using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Core.Storage;
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
    private readonly IMediaStorage _storage;
    private readonly UserManager<ApplicationUser> _userManager;

    public PostImagesController(
        IMediaUploadService mediaUpload,
        IMediaStorage storage,
        UserManager<ApplicationUser> userManager)
    {
        _mediaUpload = mediaUpload;
        _storage = storage;
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

        // Faz 4.8 fix: IMediaStorage.GetPublicUrl '/' prefix garantisi verir.
        // Avatar/Featured render pattern reuse — Quill img src'i de aynı format.
        var url = _storage.GetPublicUrl(result.RelativePath ?? string.Empty);
        return Ok(new { url });
    }
}
