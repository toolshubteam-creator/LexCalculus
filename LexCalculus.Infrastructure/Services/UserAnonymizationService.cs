using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Extensions;
using LexCalculus.Core.Services;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Services;

/// <summary>
/// KVKK uyumlu hesap anonimize servisi. Faz 5 Adım 5.1, charter Karar 6.
/// Hard delete YOK — kullanıcı kayıtlarını koruyup sadece kişisel veriyi
/// temizler (DB integrity + içerik bağlamı korunur).
/// </summary>
public sealed class UserAnonymizationService : IUserAnonymizationService
{
    private readonly ApplicationDbContext _ctx;
    private readonly IMediaStorage _storage;
    private readonly IActivityLogService _activityLog;
    private readonly IPostTagService _tags;
    private readonly ILogger<UserAnonymizationService>? _logger;

    public UserAnonymizationService(
        ApplicationDbContext ctx,
        IMediaStorage storage,
        IActivityLogService activityLog,
        IPostTagService tags,
        ILogger<UserAnonymizationService>? logger = null)
    {
        _ctx = ctx;
        _storage = storage;
        _activityLog = activityLog;
        _tags = tags;
        _logger = logger;
    }

    public async Task<UserAnonymizationCheck> CanAnonymizeAsync(
        int userId, CancellationToken ct = default)
    {
        var user = await _ctx.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return new UserAnonymizationCheck(false, "Kullanıcı bulunamadı.", 0, 0, 0);

        if (!user.IsActive)
            return new UserAnonymizationCheck(false, "Kullanıcı zaten anonimize edilmiş.", 0, 0, 0);

        // Tek owner tenant guard: User bir veya daha fazla tenant'ın owner'ı,
        // ve tenant'ta başka aktif (IsActive=true) member varsa bloke.
        var ownedTenantIds = await _ctx.Tenants
            .Where(t => t.OwnerUserId == userId)
            .Select(t => t.Id)
            .ToListAsync(ct);

        foreach (var tenantId in ownedTenantIds)
        {
            var hasOtherActiveMember = await _ctx.Users.AnyAsync(
                u => u.TenantId == tenantId && u.Id != userId && u.IsActive, ct);
            if (hasOtherActiveMember)
            {
                var tenantName = await _ctx.Tenants
                    .Where(t => t.Id == tenantId)
                    .Select(t => t.Name)
                    .FirstOrDefaultAsync(ct);
                return new UserAnonymizationCheck(
                    false,
                    $"\"{tenantName}\" tenant'ının sahibisiniz ve başka aktif üyeler var. " +
                        "Önce sahipliği başka bir kullanıcıya devredin veya tenant'ı silin.",
                    0, 0, 0);
            }
        }

        var connectionCount = await _ctx.UserConnections
            .CountAsync(c => c.RequesterId == userId || c.TargetId == userId, ct);
        var postCount = await _ctx.UserPosts.CountAsync(p => p.UserId == userId, ct);
        var commentCount = await _ctx.PostComments.CountAsync(c => c.UserId == userId, ct);

        return new UserAnonymizationCheck(true, null, connectionCount, postCount, commentCount);
    }

    public async Task<UserAnonymizationResult> AnonymizeAsync(
        int userId, int actingAdminUserId, CancellationToken ct = default)
    {
        var check = await CanAnonymizeAsync(userId, ct);
        if (!check.CanProceed)
            return new UserAnonymizationResult(false, check.BlockerMessage ?? "İşlem yapılamaz.");

        var user = await _ctx.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return new UserAnonymizationResult(false, "Kullanıcı bulunamadı.");

        // 1) Avatar dosyası temizle (best-effort, hata anonimize'i durdurmaz)
        var avatarPath = user.Profile?.AvatarUrl;
        if (!string.IsNullOrEmpty(avatarPath))
        {
            try { await _storage.DeleteAsync(avatarPath, ct); }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "Avatar silme başarısız (anonimize devam): userId={UserId} path={Path}",
                    userId, avatarPath);
            }
        }

        // 2) Profile alanlarını temizle (Profile null olabilir — defansif)
        if (user.Profile is not null)
        {
            user.Profile.DisplayName = ApplicationUserDisplayExtensions.AnonymizedDisplayName;
            user.Profile.Bio = null;
            user.Profile.City = null;
            user.Profile.MeslekTuru = null;
            user.Profile.MeslekTuruDiger = null;
            user.Profile.AvatarUrl = null;
            user.Profile.PublicSlug = null;
            user.Profile.IsPublicProfile = false;
            user.Profile.ShowConnections = false;
            user.Profile.ShowTenant = false;
            user.Profile.BaroNo = null;
        }

        // 3) Identity alanları (login imkansız)
        var anonHandle = $"deleted-{userId}-{Guid.NewGuid():N}@anonymized.local";
        user.IsActive = false;
        user.UserName = anonHandle;
        user.NormalizedUserName = anonHandle.ToUpperInvariant();
        user.Email = anonHandle;
        user.NormalizedEmail = anonHandle.ToUpperInvariant();
        user.EmailConfirmed = false;
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.FullName = ApplicationUserDisplayExtensions.AnonymizedDisplayName;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.PasswordHash = null;
        user.SecurityStamp = Guid.NewGuid().ToString();

        // Tenant ilişkisi: tek-owner guard zaten geçti (yukarıda).
        // User'ın TenantId'si null'a çekilir — orphan tenant member kalmaz.
        user.TenantId = null;

        // 4) UserConnection + UserBlock hard delete (her iki yön)
        var connections = await _ctx.UserConnections
            .Where(c => c.RequesterId == userId || c.TargetId == userId)
            .ToListAsync(ct);
        if (connections.Count > 0)
            _ctx.UserConnections.RemoveRange(connections);

        var blocks = await _ctx.UserBlocks
            .Where(b => b.BlockerId == userId || b.BlockedId == userId)
            .ToListAsync(ct);
        if (blocks.Count > 0)
            _ctx.UserBlocks.RemoveRange(blocks);

        // 5) Yayında olan UserPost'ları unpublish + Tag UsageCount decrement
        // (post silinmez, "Silinmiş Kullanıcı" yazarına ait kalır ama public'te görünmez)
        var publishedPosts = await _ctx.UserPosts
            .Include(p => p.TagLinks)
            .Where(p => p.UserId == userId && p.IsPublished)
            .ToListAsync(ct);

        var tagDecrementIds = new List<int>();
        foreach (var post in publishedPosts)
        {
            post.IsPublished = false;
            post.UpdatedAt = DateTime.UtcNow;
            tagDecrementIds.AddRange(post.TagLinks.Select(l => l.TagId));
        }
        // Faz 6.11 #17 — ortak helper (ContentReportService ile paylaşılan floor-0
        // batch decrement). Aynı tag birden çok post'ta ise her kullanım için ayrı
        // azaltma (duplikalar korunur). SaveChanges helper'da değil → aşağıdaki tek
        // SaveChanges ile anonimize + decrement atomik kalır.
        if (tagDecrementIds.Count > 0)
            await _tags.DecrementUsageForTagIdsAsync(tagDecrementIds, ct);

        // 6) Atomik kaydet
        await _ctx.SaveChangesAsync(ct);

        // 7) Activity log
        await _activityLog.LogAsync(
            action: "User.Anonymize",
            entityType: nameof(ApplicationUser),
            entityId: userId,
            description: $"Kullanıcı anonimize edildi (admin {actingAdminUserId})",
            metadata: new
            {
                ActingAdminId = actingAdminUserId,
                TargetUserId = userId,
                ConnectionsRemoved = connections.Count,
                BlocksRemoved = blocks.Count,
                PostsUnpublished = publishedPosts.Count,
                check.PostCount,
                check.CommentCount
            },
            ct: ct);

        _logger?.LogInformation(
            "User anonimized: userId={UserId} byAdmin={AdminId} unpublishedPosts={Posts}",
            userId, actingAdminUserId, publishedPosts.Count);

        return new UserAnonymizationResult(true, null);
    }
}
