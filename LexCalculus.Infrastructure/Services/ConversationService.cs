using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Extensions;
using LexCalculus.Core.Messaging;
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
    private readonly IMessagingNotifier? _notifier;

    public ConversationService(
        ApplicationDbContext ctx,
        IUserBlockService blockService,
        IMediaStorage storage,
        IActivityLogService activityLog,
        ILogger<ConversationService>? logger = null,
        IMessagingNotifier? notifier = null)
    {
        _ctx = ctx;
        _blockService = blockService;
        _storage = storage;
        _activityLog = activityLog;
        _logger = logger;
        _notifier = notifier;
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
        // Faz 6.10 (#31) — tek query. Önceden: 1 conv + 1 block + N son-mesaj +
        // N unread = (2N+2) round-trip. Şimdi: tek SELECT; engelleme filtresi
        // korelasyonlu EXISTS, son mesaj + unread korelasyonlu alt sorgu (APPLY).
        // Görüntüleme alanları (isim/slug/avatar) skalar projekte edilip in-memory
        // extension method'larla map'lenir — anonimize/inactive davranışı korunur.
        var rows = await (
            from c in _ctx.Conversations
            where (c.User1Id == userId || c.User2Id == userId)
                  // Engelleyen filter: viewer karşı tarafı engellemiş ise gizli.
                  && !_ctx.UserBlocks.Any(b => b.BlockerId == userId
                        && b.BlockedId == (c.User1Id == userId ? c.User2Id : c.User1Id))
            let other = c.User1Id == userId ? c.User2 : c.User1
            let otherId = c.User1Id == userId ? c.User2Id : c.User1Id
            let myReadAt = c.User1Id == userId ? c.User1LastReadAt : c.User2LastReadAt
            orderby c.LastMessageAt descending
            select new ListRow
            {
                ConvId = c.Id,
                LastMessageAt = c.LastMessageAt,
                OtherUserId = otherId,
                OtherIsActive = other.IsActive,
                OtherUserName = other.UserName,
                OtherDisplayName = other.Profile != null ? other.Profile.DisplayName : null,
                OtherSlug = other.Profile != null ? other.Profile.PublicSlug : null,
                OtherAvatarUrl = other.Profile != null ? other.Profile.AvatarUrl : null,
                // Son mesaj preview kaynağı — IsModeratorHidden mesajlar filter dışı
                // (alıcı görmez; sahip kendi gizli mesajını placeholder ile görür).
                // IsDeleted mesaj son mesaj olabilir → '(silindi)' gösterilir.
                Last = c.Messages
                    .Where(m => !m.IsModeratorHidden || m.SenderId == userId)
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new LastMsgRow
                    {
                        Body = m.Body,
                        IsDeleted = m.IsDeleted,
                        IsModeratorHidden = m.IsModeratorHidden,
                        SenderId = m.SenderId
                    })
                    .FirstOrDefault(),
                // Okunmamış sayım — hidden/deleted mesajlar hariç (alıcı zaten görmez).
                UnreadCount = c.Messages.Count(m =>
                    m.SenderId == otherId
                    && (myReadAt == null || m.CreatedAt > myReadAt)
                    && !m.IsDeleted
                    && !m.IsModeratorHidden)
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return Array.Empty<ConversationListItem>();

        var result = new List<ConversationListItem>(rows.Count);
        foreach (var r in rows)
        {
            // Skalar alanlardan hafif ApplicationUser yeniden kur — görüntüleme
            // extension'larını (anonimize/inactive fallback) tek-kaynak reuse et.
            var other = new ApplicationUser
            {
                IsActive = r.OtherIsActive,
                UserName = r.OtherUserName,
                Profile = (r.OtherDisplayName ?? r.OtherSlug ?? r.OtherAvatarUrl) != null
                    ? new UserProfile
                    {
                        DisplayName = r.OtherDisplayName ?? "",
                        PublicSlug = r.OtherSlug,
                        AvatarUrl = r.OtherAvatarUrl
                    }
                    : null
            };

            string? preview = null;
            if (r.Last is not null)
            {
                if (r.Last.IsDeleted)
                {
                    preview = "(silindi)";
                }
                else if (r.Last.IsModeratorHidden && r.Last.SenderId == userId)
                {
                    preview = "(yönetim tarafından gizlendi)";
                }
                else
                {
                    var plain = StripHtmlForPreview(r.Last.Body);
                    preview = plain.Length > LastMessagePreviewLength
                        ? plain.Substring(0, LastMessagePreviewLength) + "…"
                        : plain;
                }
            }

            result.Add(new ConversationListItem(
                ConversationId: r.ConvId,
                OtherUserId: r.OtherUserId,
                OtherDisplayName: other.GetDisplayNameOrAnonymized(),
                OtherSlug: other.GetPublicSlugOrNull(),
                OtherAvatarUrl: other.IsAnonymizedOrInactive() || string.IsNullOrEmpty(other.Profile?.AvatarUrl)
                    ? null
                    : _storage.GetPublicUrl(other.Profile.AvatarUrl),
                LastMessageAt: r.LastMessageAt,
                LastMessagePreview: preview,
                UnreadCount: r.UnreadCount));
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

        // Faz 6.7 (#37) — multi-tab read-state: kullanıcının diğer oturumlarına bildir.
        // Sessiz fail: mark-read kaydı zaten yapıldı, broadcast best-effort.
        if (_notifier is not null)
        {
            try
            {
                await _notifier.NotifyConversationReadAsync(userId, conversationId, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "ConversationRead notify başarısız: user={UserId} conv={ConvId}",
                    userId, conversationId);
            }
        }

        return new ConversationResult(true, null, conv);
    }

    public async Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default)
    {
        // Faz 6.10 (#32) — tek SELECT COUNT. Önceden: 1 conv + N count = (N+1).
        // Message.Conversation navigation üzerinden filtre; karşı taraftan gelen,
        // okuma anından sonraki, silinmemiş/gizlenmemiş mesajlar sayılır. Viewer
        // conversation'ın User1 veya User2'si olduğundan ilgili LastReadAt seçilir.
        return await _ctx.Messages
            .Where(m => !m.IsDeleted
                     && !m.IsModeratorHidden
                     && m.SenderId != userId
                     && (m.Conversation.User1Id == userId || m.Conversation.User2Id == userId))
            .CountAsync(m =>
                (m.Conversation.User1Id == userId
                    && (m.Conversation.User1LastReadAt == null
                        || m.CreatedAt > m.Conversation.User1LastReadAt))
                || (m.Conversation.User2Id == userId
                    && (m.Conversation.User2LastReadAt == null
                        || m.CreatedAt > m.Conversation.User2LastReadAt)),
                ct);
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

    // ─── projection DTO'ları (Faz 6.10 #31 tek-query) ─────────────────────

    /// <summary>GetForUserAsync tek-query projeksiyon satırı (SQL → in-memory map).</summary>
    private sealed class ListRow
    {
        public int ConvId { get; init; }
        public DateTime LastMessageAt { get; init; }
        public int OtherUserId { get; init; }
        public bool OtherIsActive { get; init; }
        public string? OtherUserName { get; init; }
        public string? OtherDisplayName { get; init; }
        public string? OtherSlug { get; init; }
        public string? OtherAvatarUrl { get; init; }
        public LastMsgRow? Last { get; init; }
        public int UnreadCount { get; init; }
    }

    /// <summary>Son mesaj preview için gereken minimal alanlar (korelasyonlu alt sorgu).</summary>
    private sealed class LastMsgRow
    {
        public string Body { get; init; } = "";
        public bool IsDeleted { get; init; }
        public bool IsModeratorHidden { get; init; }
        public int SenderId { get; init; }
    }
}
