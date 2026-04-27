using System.Text.Json;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
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
}
