using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Extensions;
using LexCalculus.Core.Services;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Services;

/// <summary>
/// Conversation servisi. Yetki: bağlantı OR aynı aktif tenant + NOT engelleme.
/// Faz 5.4, charter Karar 3, 4.
/// </summary>
public sealed class ConversationService : IConversationService
{
    private const int LastMessagePreviewLength = 80;

    private readonly ApplicationDbContext _ctx;
    private readonly IUserBlockService _blockService;
    private readonly IMediaStorage _storage;
    private readonly IActivityLogService _activityLog;
    private readonly ILogger<ConversationService>? _logger;

    public ConversationService(
        ApplicationDbContext ctx,
        IUserBlockService blockService,
        IMediaStorage storage,
        IActivityLogService activityLog,
        ILogger<ConversationService>? logger = null)
    {
        _ctx = ctx;
        _blockService = blockService;
        _storage = storage;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<ConversationResult> GetOrCreateAsync(
        int userAId, int userBId, CancellationToken ct = default)
    {
        if (userAId == userBId)
            return new ConversationResult(false, "Kendinize mesaj gönderemezsiniz.", null);
        if (userAId <= 0 || userBId <= 0)
            return new ConversationResult(false, "Geçersiz kullanıcı.", null);

        var canMessage = await CanMessageAsync(userAId, userBId, ct);
        if (!canMessage)
            return new ConversationResult(false, "Bu kullanıcıya mesaj gönderilemiyor.", null);

        var u1 = Math.Min(userAId, userBId);
        var u2 = Math.Max(userAId, userBId);

        var existing = await _ctx.Conversations
            .Include(c => c.User1).ThenInclude(u => u!.Profile)
            .Include(c => c.User2).ThenInclude(u => u!.Profile)
            .FirstOrDefaultAsync(c => c.User1Id == u1 && c.User2Id == u2, ct);
        if (existing is not null)
            return new ConversationResult(true, null, existing);

        var now = DateTime.UtcNow;
        var conv = new Conversation
        {
            User1Id = u1,
            User2Id = u2,
            CreatedAt = now,
            LastMessageAt = now
        };
        _ctx.Conversations.Add(conv);
        try
        {
            await _ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Race koruması: composite unique (User1,User2) index yarış halinde
            // çift insert engeller. Mevcut'u tekrar fetch et.
            _logger?.LogWarning(ex,
                "Conversation race ile mükerrer insert; mevcut fetch ediliyor (u1={U1},u2={U2})", u1, u2);
            var raced = await _ctx.Conversations
                .Include(c => c.User1).ThenInclude(u => u!.Profile)
                .Include(c => c.User2).ThenInclude(u => u!.Profile)
                .FirstOrDefaultAsync(c => c.User1Id == u1 && c.User2Id == u2, ct);
            if (raced is not null) return new ConversationResult(true, null, raced);
            return new ConversationResult(false, "Sohbet oluşturulamadı.", null);
        }

        await _activityLog.LogAsync(
            action: "Conversation.Create",
            entityType: nameof(Conversation),
            entityId: conv.Id,
            description: $"Sohbet oluşturuldu u1={u1} u2={u2}",
            metadata: new { conv.Id, User1Id = u1, User2Id = u2 },
            ct: ct);

        return new ConversationResult(true, null, conv);
    }

    public async Task<Conversation?> GetByIdAsync(
        int conversationId, int viewerId, CancellationToken ct = default)
    {
        var conv = await _ctx.Conversations
            .Include(c => c.User1).ThenInclude(u => u!.Profile)
            .Include(c => c.User2).ThenInclude(u => u!.Profile)
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conv is null) return null;
        if (conv.User1Id != viewerId && conv.User2Id != viewerId) return null;
        return conv;
    }

    public async Task<IReadOnlyList<ConversationListItem>> GetForUserAsync(
        int userId, CancellationToken ct = default)
    {
        var convs = await _ctx.Conversations
            .Include(c => c.User1).ThenInclude(u => u!.Profile)
            .Include(c => c.User2).ThenInclude(u => u!.Profile)
            .Where(c => c.User1Id == userId || c.User2Id == userId)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync(ct);

        if (convs.Count == 0)
            return Array.Empty<ConversationListItem>();

        // Engelleyen filter: viewer karşı tarafı engellemiş ise listede gizli
        var blockedIds = await _ctx.UserBlocks
            .Where(b => b.BlockerId == userId)
            .Select(b => b.BlockedId)
            .ToListAsync(ct);

        var visible = convs
            .Where(c => !blockedIds.Contains(c.User1Id == userId ? c.User2Id : c.User1Id))
            .ToList();

        var result = new List<ConversationListItem>(visible.Count);
        foreach (var c in visible)
        {
            var other = c.User1Id == userId ? c.User2 : c.User1;
            var lastReadAt = c.User1Id == userId ? c.User1LastReadAt : c.User2LastReadAt;
            var otherUserId = c.User1Id == userId ? c.User2Id : c.User1Id;

            // Son mesaj preview — IsModeratorHidden mesajlar filter dışı
            // (alıcı için: hidden mesajı görmesin; sahip için kendi gizli
            // mesajı liste'de placeholder ile görünür). n+1 — Faz 6+ tech-debt.
            var lastMsg = await _ctx.Messages
                .Where(m => m.ConversationId == c.Id
                         && (!m.IsModeratorHidden || m.SenderId == userId))
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.Body, m.IsDeleted, m.IsModeratorHidden, m.SenderId })
                .FirstOrDefaultAsync(ct);

            string? preview = null;
            if (lastMsg is not null)
            {
                if (lastMsg.IsDeleted)
                {
                    preview = "(silindi)";
                }
                else if (lastMsg.IsModeratorHidden && lastMsg.SenderId == userId)
                {
                    preview = "(yönetim tarafından gizlendi)";
                }
                else
                {
                    var plain = StripHtmlForPreview(lastMsg.Body);
                    preview = plain.Length > LastMessagePreviewLength
                        ? plain.Substring(0, LastMessagePreviewLength) + "…"
                        : plain;
                }
            }

            // Okunmamış sayım — hidden mesajlar dahil edilmez (alıcı zaten görmez)
            var threshold = lastReadAt ?? DateTime.MinValue;
            var unread = await _ctx.Messages
                .CountAsync(m => m.ConversationId == c.Id
                              && m.SenderId == otherUserId
                              && m.CreatedAt > threshold
                              && !m.IsDeleted
                              && !m.IsModeratorHidden, ct);

            result.Add(new ConversationListItem(
                ConversationId: c.Id,
                OtherUserId: otherUserId,
                OtherDisplayName: other.GetDisplayNameOrAnonymized(),
                OtherSlug: other.GetPublicSlugOrNull(),
                OtherAvatarUrl: other.IsAnonymizedOrInactive() || string.IsNullOrEmpty(other.Profile?.AvatarUrl)
                    ? null
                    : _storage.GetPublicUrl(other.Profile.AvatarUrl),
                LastMessageAt: c.LastMessageAt,
                LastMessagePreview: preview,
                UnreadCount: unread));
        }

        return result;
    }

    public async Task<ConversationResult> MarkAsReadAsync(
        int conversationId, int userId, CancellationToken ct = default)
    {
        var conv = await _ctx.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conv is null)
            return new ConversationResult(false, "Sohbet bulunamadı.", null);
        if (conv.User1Id != userId && conv.User2Id != userId)
            return new ConversationResult(false, "Yetkisiz işlem.", null);

        var now = DateTime.UtcNow;
        if (conv.User1Id == userId) conv.User1LastReadAt = now;
        else conv.User2LastReadAt = now;

        await _ctx.SaveChangesAsync(ct);
        return new ConversationResult(true, null, conv);
    }

    public async Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default)
    {
        // n+1 var (Faz 6+ optimize: tek query ile toplama)
        var convs = await _ctx.Conversations
            .Where(c => c.User1Id == userId || c.User2Id == userId)
            .Select(c => new
            {
                ConvId = c.Id,
                LastReadAt = c.User1Id == userId ? c.User1LastReadAt : c.User2LastReadAt,
                OtherUserId = c.User1Id == userId ? c.User2Id : c.User1Id
            })
            .ToListAsync(ct);

        var total = 0;
        foreach (var c in convs)
        {
            var threshold = c.LastReadAt ?? DateTime.MinValue;
            total += await _ctx.Messages.CountAsync(m =>
                m.ConversationId == c.ConvId
                && m.SenderId == c.OtherUserId
                && m.CreatedAt > threshold
                && !m.IsDeleted
                && !m.IsModeratorHidden, ct);
        }
        return total;
    }

    // ─── helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Yetki: bağlantı OR aynı aktif tenant member; AND NOT engelleme.
    /// Faz 5 charter Karar 3. UI guard için /uye/{slug} 'Mesaj Gönder' butonu
    /// görünürlüğünde reuse — Faz 5.5'te public hâle getirildi.
    /// </summary>
    public async Task<bool> CanMessageAsync(int a, int b, CancellationToken ct = default)
    {
        var blocked = await _blockService.IsEitherDirectionBlockedAsync(a, b, ct);
        if (blocked) return false;

        var hasConnection = await _ctx.UserConnections.AnyAsync(c =>
            ((c.RequesterId == a && c.TargetId == b) ||
             (c.RequesterId == b && c.TargetId == a)) &&
            c.Status == UserConnectionStatus.Accepted, ct);
        if (hasConnection) return true;

        // Aynı aktif tenant member?
        var users = await _ctx.Users
            .Where(u => (u.Id == a || u.Id == b) && u.IsActive && u.TenantId.HasValue)
            .Select(u => new { u.Id, u.TenantId })
            .ToListAsync(ct);
        if (users.Count == 2 && users[0].TenantId == users[1].TenantId)
            return true;

        return false;
    }

    private static string StripHtmlForPreview(string html)
    {
        // Basit strip: <br> → \n, sonra <tag> kaldır, HTML entity decode'a gerek yok
        // (preview'da görsel; gerçek decode UI tarafında)
        var noBr = System.Text.RegularExpressions.Regex.Replace(html, @"<br\s*/?>", " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var noTags = System.Text.RegularExpressions.Regex.Replace(noBr, @"<[^>]+>", "");
        return System.Net.WebUtility.HtmlDecode(noTags).Trim();
    }
}
