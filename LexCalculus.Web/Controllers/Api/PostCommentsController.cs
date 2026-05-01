using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Web.Infrastructure.Rendering;
using LexCalculus.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Web.Controllers.Api;

/// <summary>
/// Yorum CRUD AJAX endpoint'i. Create + Update server-side render edilmiş
/// _PostComment partial HTML döndürür (XSS güvenli, sanitize servis tarafında).
/// CSRF: X-CSRF-TOKEN header (Adım 4.8 pattern). Faz 4.9 P2.
/// </summary>
[ApiController]
[Route("api/post-comments")]
[Authorize]
public sealed class PostCommentsController : ControllerBase
{
    private readonly IPostCommentService _comments;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPartialRenderer _partial;
    private readonly ApplicationDbContext _ctx;
    private readonly IMediaStorage _storage;

    public PostCommentsController(
        IPostCommentService comments,
        UserManager<ApplicationUser> userManager,
        IPartialRenderer partial,
        ApplicationDbContext ctx,
        IMediaStorage storage)
    {
        _comments = comments;
        _userManager = userManager;
        _partial = partial;
        _ctx = ctx;
        _storage = storage;
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateCommentRequest? req,
        CancellationToken ct = default)
    {
        if (req is null || req.PostId <= 0 || string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { error = "Geçersiz istek." });

        if (!TryGetUserId(out var uid))
            return Unauthorized();

        var result = await _comments.CreateAsync(req.PostId, uid, req.Body, ct);
        if (!result.Success || result.Comment is null)
            return BadRequest(new { error = result.ErrorMessage ?? "Yorum oluşturulamadı." });

        var vm = await BuildViewModelAsync(result.Comment.Id, uid, ct);
        var html = await _partial.RenderAsync("_PostComment", vm, ct);

        return Ok(new { html, commentId = result.Comment.Id });
    }

    [HttpPost("{id:int}/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id,
        [FromBody] UpdateCommentRequest? req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { error = "Yorum boş olamaz." });

        if (!TryGetUserId(out var uid))
            return Unauthorized();

        var result = await _comments.UpdateAsync(id, uid, req.Body, ct);
        if (!result.Success || result.Comment is null)
            return BadRequest(new { error = result.ErrorMessage ?? "Yorum güncellenemedi." });

        var vm = await BuildViewModelAsync(result.Comment.Id, uid, ct);
        var html = await _partial.RenderAsync("_PostComment", vm, ct);

        return Ok(new { html });
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var uid))
            return Unauthorized();

        var isAdmin = User.IsInRole("Admin");
        var result = await _comments.DeleteAsync(id, uid, isAdmin, ct);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage ?? "Silme başarısız." });

        return Ok(new { success = true });
    }

    private bool TryGetUserId(out int userId)
    {
        var raw = _userManager.GetUserId(User);
        return int.TryParse(raw, out userId);
    }

    private async Task<PostCommentViewModel> BuildViewModelAsync(
        int commentId, int viewerId, CancellationToken ct)
    {
        var data = await _ctx.PostComments
            .Where(c => c.Id == commentId)
            .Select(c => new
            {
                c.Id, c.PostId, c.Body, c.CreatedAt, c.IsEdited,
                c.UserId,
                PostOwnerId = c.Post != null ? c.Post.UserId : 0,
                AuthorDisplayName = c.User!.Profile != null
                    ? c.User.Profile.DisplayName
                    : c.User.UserName,
                AuthorSlug = c.User.Profile != null ? c.User.Profile.PublicSlug : null,
                AuthorAvatar = c.User.Profile != null ? c.User.Profile.AvatarUrl : null
            })
            .FirstAsync(ct);

        var isAdmin = User.IsInRole("Admin");
        return new PostCommentViewModel
        {
            Id = data.Id,
            PostId = data.PostId,
            Body = data.Body,
            CreatedAt = data.CreatedAt,
            IsEdited = data.IsEdited,
            AuthorDisplayName = string.IsNullOrWhiteSpace(data.AuthorDisplayName)
                ? "Kullanıcı" : data.AuthorDisplayName,
            AuthorSlug = data.AuthorSlug,
            AuthorAvatarUrl = string.IsNullOrEmpty(data.AuthorAvatar)
                ? null : _storage.GetPublicUrl(data.AuthorAvatar),
            CanEdit = data.UserId == viewerId,
            CanDelete = data.UserId == viewerId
                || data.PostOwnerId == viewerId
                || isAdmin
        };
    }
}

public sealed record CreateCommentRequest(int PostId, string Body);
public sealed record UpdateCommentRequest(string Body);
