using System.Text.Json;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Services;

public sealed class CalculationHistoryService : ICalculationHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ApplicationDbContext _ctx;
    private readonly ILogger<CalculationHistoryService> _logger;

    public CalculationHistoryService(ApplicationDbContext ctx, ILogger<CalculationHistoryService> logger)
    {
        _ctx = ctx;
        _logger = logger;
    }

    public async Task LogAsync<TInput, TResult>(
        int? userId,
        string categorySlug,
        string toolSlug,
        string toolTitle,
        TInput input,
        TResult result,
        decimal? totalAmount,
        string? unit,
        CancellationToken cancellationToken = default)
    {
        if (userId is null or <= 0)
            return;

        try
        {
            var inputJson = JsonSerializer.Serialize(input, JsonOptions);
            var outputJson = JsonSerializer.Serialize(result, JsonOptions);

            if (inputJson.Length > 200_000 || outputJson.Length > 500_000)
            {
                _logger.LogWarning(
                    "CalculationHistory payload too large for {Tool}; user={User}, in={In}KB, out={Out}KB. Skipping.",
                    toolSlug, userId, inputJson.Length / 1024, outputJson.Length / 1024);
                return;
            }

            var entry = new CalculationHistory
            {
                UserId = userId.Value,
                CategorySlug = categorySlug,
                ToolSlug = toolSlug,
                ToolTitle = toolTitle,
                InputJson = inputJson,
                OutputJson = outputJson,
                TotalAmount = totalAmount,
                Unit = unit
            };

            _ctx.Set<CalculationHistory>().Add(entry);
            await _ctx.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Calculation logged: user={User}, tool={Tool}, total={Total} {Unit}",
                userId, toolSlug, totalAmount, unit);
        }
        catch (Exception ex)
        {
            // Logging MUST NOT break the calculation. Swallow + log.
            _logger.LogError(ex,
                "Failed to log calculation history for user={User}, tool={Tool}",
                userId, toolSlug);
        }
    }

    public async Task<CalculationHistoryPage> GetForUserAsync(
        int userId,
        int page,
        int pageSize,
        string? toolSlugFilter = null,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        CancellationToken ct = default)
    {
        if (userId <= 0)
            return new CalculationHistoryPage(Array.Empty<CalculationHistory>(), 0, 1, pageSize);
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 25;

        var q = _ctx.Set<CalculationHistory>().Where(h => h.UserId == userId);

        if (!string.IsNullOrWhiteSpace(toolSlugFilter))
            q = q.Where(h => h.ToolSlug == toolSlugFilter);
        if (startDateUtc.HasValue)
            q = q.Where(h => h.CreatedAt >= startDateUtc.Value);
        if (endDateUtc.HasValue)
            q = q.Where(h => h.CreatedAt < endDateUtc.Value.AddDays(1));

        var totalCount = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(h => h.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new CalculationHistoryPage(items, totalCount, page, pageSize);
    }

    public async Task<CalculationHistory?> GetByIdForUserAsync(
        int historyId, int userId, CancellationToken ct = default)
    {
        if (userId <= 0) return null;
        return await _ctx.Set<CalculationHistory>()
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId, ct);
    }

    public async Task<IReadOnlyList<string>> GetUsedToolSlugsForUserAsync(
        int userId, CancellationToken ct = default)
    {
        if (userId <= 0) return Array.Empty<string>();
        return await _ctx.Set<CalculationHistory>()
            .Where(h => h.UserId == userId)
            .Select(h => h.ToolSlug)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);
    }

    public async Task<CalculationHistoryPage> GetAllPaginatedAsync(
        int page, int pageSize,
        string? toolSlugFilter = null,
        int? userIdFilter = null,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 25;

        var q = _ctx.Set<CalculationHistory>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(toolSlugFilter))
            q = q.Where(h => h.ToolSlug == toolSlugFilter);
        if (userIdFilter.HasValue)
            q = q.Where(h => h.UserId == userIdFilter.Value);
        if (startDateUtc.HasValue)
            q = q.Where(h => h.CreatedAt >= startDateUtc.Value);
        if (endDateUtc.HasValue)
            q = q.Where(h => h.CreatedAt < endDateUtc.Value.AddDays(1));

        var totalCount = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(h => h.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new CalculationHistoryPage(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<int>> GetUsersWithHistoryAsync(CancellationToken ct = default)
    {
        return await _ctx.Set<CalculationHistory>()
            .Select(h => h.UserId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(ct);
    }

    public async Task<CalculationHistory?> GetByIdForAdminAsync(int historyId, CancellationToken ct = default)
    {
        return await _ctx.Set<CalculationHistory>()
            .FirstOrDefaultAsync(h => h.Id == historyId, ct);
    }
}
