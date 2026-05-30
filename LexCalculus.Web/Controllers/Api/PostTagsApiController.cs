using LexCalculus.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LexCalculus.Web.Controllers.Api;

/// <summary>
/// Tag autocomplete public API (Faz 6.6, charter Karar 5). Tag isimleri public
/// (makale tag chip'leri herkese görünür) → AllowAnonymous. Debounce client'ta;
/// ek koruma için ajax-general rate limit (100/dk).
/// </summary>
[ApiController]
[Route("api/post-tags")]
[AllowAnonymous]
public sealed class PostTagsApiController : ControllerBase
{
    private readonly IPostTagService _tagService;

    public PostTagsApiController(IPostTagService tagService)
    {
        _tagService = tagService;
    }

    [HttpGet("search")]
    [EnableRateLimiting("ajax-general")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q, [FromQuery] int take = 10, CancellationToken ct = default)
    {
        var results = await _tagService.SearchByPrefixAsync(q ?? "", take, ct);
        return Ok(results.Select(t => new
        {
            id = t.Id,
            name = t.Name,
            usageCount = t.UsageCount
        }));
    }
}
