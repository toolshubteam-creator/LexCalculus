using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Services;

public sealed class UserBlockService : IUserBlockService
{
    private readonly ApplicationDbContext _ctx;
    private readonly IActivityLogService _activityLog;

    public UserBlockService(ApplicationDbContext ctx, IActivityLogService activityLog)
    {
        _ctx = ctx;
        _activityLog = activityLog;
    }

    public async Task<UserBlockResult> BlockAsync(
        int blockerId, int blockedId, CancellationToken ct = default)
    {
        if (blockerId == blockedId)
            return new UserBlockResult(false, "Kendinizi engelleyemezsiniz.");

        var target = await _ctx.Users.AsAdminQuery()
            .Where(u => u.Id == blockedId)
            .Select(u => new { u.Id, u.IsActive })
            .FirstOrDefaultAsync(ct);

        if (target is null || !target.IsActive)
            return new UserBlockResult(false, "Hedef kullanıcı bulunamadı.");

        var alreadyBlocked = await _ctx.UserBlocks
            .AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, ct);
        if (alreadyBlocked)
            return new UserBlockResult(false, "Bu kullanıcıyı zaten engellediniz.");

        var block = new UserBlock
        {
            BlockerId = blockerId,
            BlockedId = blockedId,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.UserBlocks.Add(block);

        // Cascade: mevcut Accepted bağlantı varsa hard delete (Karar 5).
        // Pending/Rejected/Cancelled kayıtlar dokunulmaz — durumları zaten
        // bağlı değil.
        var existingAccepted = await _ctx.UserConnections
            .Where(c => ((c.RequesterId == blockerId && c.TargetId == blockedId)
                      || (c.RequesterId == blockedId && c.TargetId == blockerId))
                     && c.Status == UserConnectionStatus.Accepted)
            .FirstOrDefaultAsync(ct);

        int? cascadedConnectionId = null;
        if (existingAccepted is not null)
        {
            cascadedConnectionId = existingAccepted.Id;
            _ctx.UserConnections.Remove(existingAccepted);
        }

        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "UserBlock.Create",
            entityType: nameof(UserBlock),
            entityId: block.Id,
            description: $"Engelleme: {blockerId} → {blockedId}",
            metadata: new { BlockerId = blockerId, BlockedId = blockedId },
            ct: ct);

        if (cascadedConnectionId.HasValue)
        {
            await _activityLog.LogAsync(
                action: "UserConnection.Remove",
                entityType: nameof(UserConnection),
                entityId: cascadedConnectionId.Value,
                description: $"Engelleme cascade: bağlantı kaldırıldı ({blockerId} ↔ {blockedId})",
                metadata: new { BlockerId = blockerId, BlockedId = blockedId, Reason = "Block" },
                ct: ct);
        }

        return new UserBlockResult(true, null);
    }

    public async Task<UserBlockResult> UnblockAsync(
        int blockerId, int blockedId, CancellationToken ct = default)
    {
        var block = await _ctx.UserBlocks
            .FirstOrDefaultAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, ct);
        if (block is null)
            return new UserBlockResult(false, "Engelleme kaydı bulunamadı.");

        var blockId = block.Id;
        _ctx.UserBlocks.Remove(block);
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "UserBlock.Remove",
            entityType: nameof(UserBlock),
            entityId: blockId,
            description: $"Engelleme kaldırıldı: {blockerId} → {blockedId}",
            metadata: new { BlockerId = blockerId, BlockedId = blockedId },
            ct: ct);

        return new UserBlockResult(true, null);
    }

    public Task<bool> IsBlockedAsync(int blockerId, int blockedId, CancellationToken ct = default)
        => _ctx.UserBlocks
            .AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, ct);

    public Task<bool> IsEitherDirectionBlockedAsync(int userA, int userB, CancellationToken ct = default)
        => _ctx.UserBlocks.AnyAsync(b =>
            (b.BlockerId == userA && b.BlockedId == userB)
            || (b.BlockerId == userB && b.BlockedId == userA), ct);

    public async Task<IReadOnlyList<UserBlock>> GetBlockedByUserAsync(
        int userId, CancellationToken ct = default)
    {
        return await _ctx.UserBlocks
            .Include(b => b.Blocked).ThenInclude(u => u!.Profile)
            .Where(b => b.BlockerId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<int> GetBlockedCountAsync(int userId, CancellationToken ct = default)
        => _ctx.UserBlocks.CountAsync(b => b.BlockerId == userId, ct);
}
