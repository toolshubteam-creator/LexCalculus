using LexCalculus.Core.Calculators;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Email;
using LexCalculus.Core.Email.Models;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Jobs.DataFreshness;

/// <summary>
/// Günlük olarak tüm parametreleri tarar; (slug, key) bazlı en yeni effective-dated
/// satırlardan tazelik tolerance'ını aşanları tespit eder. Admin'lere digest
/// bildirim + (opt-in ise) e-posta gönderir.
///
/// 7 günlük dedup penceresi: aynı (slug, key) için aynı admin'e 7 gün içinde
/// 2. bildirim oluşturulmaz (NotificationService.CreateAsync dedup'ıyla).
///
/// Cron: 0 6 * * * Europe/Istanbul.
/// </summary>
public sealed class DataFreshnessCheckJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly IFormulaFreshnessChecker _freshness;
    private readonly INotificationService _notifications;
    private readonly IEmailService _email;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICalculatorRegistry _registry;
    private readonly IConfiguration _config;
    private readonly ILogger<DataFreshnessCheckJob> _logger;

    public DataFreshnessCheckJob(
        ApplicationDbContext ctx,
        IFormulaFreshnessChecker freshness,
        INotificationService notifications,
        IEmailService email,
        IEmailTemplateRenderer renderer,
        UserManager<ApplicationUser> userManager,
        ICalculatorRegistry registry,
        IConfiguration config,
        ILogger<DataFreshnessCheckJob> logger)
    {
        _ctx = ctx;
        _freshness = freshness;
        _notifications = notifications;
        _email = email;
        _renderer = renderer;
        _userManager = userManager;
        _registry = registry;
        _config = config;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        _logger.LogInformation("DataFreshnessCheckJob başladı: {Time}", now);

        // 1. Tüm parametreleri çek (soft-deleted hariç global filter ile)
        var allParameters = await _ctx.Set<FormulaParameter>().ToListAsync(ct);

        // 2. (ToolSlug, Key) bazında grupla; her grubun en yeni effective-dated
        //    satırını al; stale olanları seç
        var staleLatest = allParameters
            .GroupBy(p => (p.ToolSlug, p.Key))
            .Select(g => g.OrderByDescending(p => p.EffectiveDate).First())
            .Where(latest => _freshness.IsStale(latest, now))
            .OrderBy(p => p.ToolSlug).ThenBy(p => p.Key)
            .ToList();

        _logger.LogInformation(
            "Stale tespit: {StaleCount} (slug,key) çifti / {TotalCount} toplam parametre",
            staleLatest.Count, allParameters.Count);

        if (staleLatest.Count == 0)
        {
            _logger.LogInformation("Stale yok — bildirim/e-posta gönderilmeyecek.");
            return;
        }

        // 3. Admin kullanıcıları bul
        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        if (admins.Count == 0)
        {
            _logger.LogWarning("Admin rolünde kullanıcı yok — bildirim gönderilemiyor.");
            return;
        }

        // 4. Digest model hazırla (admin'e ortak)
        var siteUrl = _config["SeoSettings:SiteUrl"] ?? "https://lexcalculus.local";
        var dashboardUrl = $"{siteUrl.TrimEnd('/')}/admin/parametreler?OnlyStale=true";

        var rows = staleLatest.Select(p => new AdminFreshnessDigestRow
        {
            ToolSlug = p.ToolSlug,
            Key = p.Key,
            LastUpdatedDate = p.LastUpdatedDate,
            DaysOverdue = Math.Abs(_freshness.DaysUntilStale(p, now) ?? 0),
            Source = p.Source,
            FrequencyLabelTr = FrequencyLabels.ToTurkish(p.ExpectedUpdateFrequency)
        }).ToList();

        var notifyDedupWindow = TimeSpan.FromDays(7);
        var notificationsCreated = 0;
        var emailsSent = 0;

        // 5. Her admin için: tek digest notification + opt-in ise e-posta
        foreach (var admin in admins)
        {
            // 5a. In-app notification (digest başlığında özet)
            var notification = await _notifications.CreateAsync(
                type: NotificationType.DataFreshness,
                userId: admin.Id,
                title: $"{staleLatest.Count} parametre kontrol gerektiriyor",
                body: $"Veri tazelik raporu: {staleLatest.Count} (slug, anahtar) " +
                      $"çifti tolerance'ını aşmış. Yönetim paneline gidin: {dashboardUrl}",
                link: "/admin/parametreler?OnlyStale=true",
                relatedEntityType: "DataFreshnessDigest",
                relatedEntityId: now.DayOfYear,    // her gün benzersiz dedup id
                iconHint: "alert",
                dedupWindow: notifyDedupWindow,
                ct: ct);

            if (notification != null)
            {
                notificationsCreated++;

                // 5b. E-posta sadece YENİ notification için — dedup hit'te gönderilmiyor
                // (Adım 3.3.4b bug fix: manuel trigger spam'i önlendi)
                if (admin.NotificationsEmailEnabled && !string.IsNullOrWhiteSpace(admin.Email))
                {
                    try
                    {
                        var model = new AdminFreshnessDigestModel
                        {
                            AdminName = admin.FullName ?? admin.UserName ?? "Admin",
                            StaleRows = rows,
                            DashboardUrl = dashboardUrl,
                            GeneratedAt = now,
                            TotalParameterCount = allParameters.Count
                        };

                        var html = await _renderer.RenderAsync("AdminFreshnessDigest", model, ct);

                        var sent = await _email.SendAsync(new EmailMessage(
                            admin.Email,
                            admin.FullName,
                            $"Veri Tazelik Raporu — {staleLatest.Count} parametre kontrol gerekli",
                            html), ct);

                        if (sent) emailsSent++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Admin digest e-posta hatası: AdminId={AdminId}", admin.Id);
                    }
                }
            }
        }

        // 6. Kullanıcı hedefleme — son 90 gün CalculationHistory join
        var (userNotifsCreated, userEmailsSent, userCount, affectedUserCount) =
            await NotifyAffectedUsersAsync(staleLatest, siteUrl, now, ct);

        _logger.LogInformation(
            "DataFreshnessCheckJob bitti: admin={AdminNotifs} bildirim/{AdminEmails} e-posta, " +
            "user={UserNotifs} bildirim/{UserEmails} e-posta ({UserCount} aktif/{AffectedCount} etkilenen), " +
            "{StaleCount} stale.",
            notificationsCreated, emailsSent,
            userNotifsCreated, userEmailsSent, userCount, affectedUserCount,
            staleLatest.Count);
    }

    private async Task<(int notifications, int emails, int totalUsers, int affectedUsers)>
        NotifyAffectedUsersAsync(
            IReadOnlyList<FormulaParameter> staleLatest,
            string siteUrl,
            DateTime now,
            CancellationToken ct)
    {
        var historyWindow = now.AddDays(-90);
        var staleSlugs = staleLatest.Select(p => p.ToolSlug).Distinct().ToHashSet();
        var hasGlobalStale = staleSlugs.Contains("*");

        // Background job: tüm kullanıcıların geçmişini taramak gerek (tenant bypass).
        IQueryable<CalculationHistory> historyQuery = _ctx.Set<CalculationHistory>()
            .AsAdminQuery()
            .Where(h => !h.IsDeleted && h.CreatedAt > historyWindow);

        if (!hasGlobalStale)
        {
            historyQuery = historyQuery.Where(h => staleSlugs.Contains(h.ToolSlug));
        }

        var historyData = await historyQuery
            .GroupBy(h => new { h.UserId, h.ToolSlug })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.ToolSlug,
                LastUsed = g.Max(x => x.CreatedAt)
            })
            .ToListAsync(ct);

        if (historyData.Count == 0)
        {
            _logger.LogInformation("Son 90 günde hesap yapan kullanıcı yok — kullanıcı bildirimi atlandı.");
            return (0, 0, 0, 0);
        }

        var titleMap = _registry.GetAll()
            .ToDictionary(m => m.Slug, m => m.Title, StringComparer.OrdinalIgnoreCase);

        var staleByToolSlug = staleLatest.ToLookup(p => p.ToolSlug, StringComparer.OrdinalIgnoreCase);
        var globalStaleParams = staleByToolSlug["*"].ToList();

        var userToRows = new Dictionary<int, List<UserFreshnessDigestRow>>();
        var userToLastUsed = new Dictionary<int, DateTime>();

        foreach (var ut in historyData)
        {
            // En son hesap zamanı (kullanıcı bazında — sadece kullanıcı bildiriminde tek değer)
            if (!userToLastUsed.TryGetValue(ut.UserId, out var existingLastUsed) || ut.LastUsed > existingLastUsed)
                userToLastUsed[ut.UserId] = ut.LastUsed;

            var matchingStale = new List<FormulaParameter>();
            matchingStale.AddRange(staleByToolSlug[ut.ToolSlug]);
            matchingStale.AddRange(globalStaleParams);

            if (matchingStale.Count == 0) continue;

            if (!userToRows.TryGetValue(ut.UserId, out var rows2))
            {
                rows2 = new List<UserFreshnessDigestRow>();
                userToRows[ut.UserId] = rows2;
            }

            foreach (var stale in matchingStale)
            {
                if (rows2.Any(r => r.ToolSlug == stale.ToolSlug && r.ParameterKey == stale.Key))
                    continue;

                var displaySlug = stale.ToolSlug == "*" ? ut.ToolSlug : stale.ToolSlug;
                var displayTitle = titleMap.TryGetValue(displaySlug, out var t) ? t : displaySlug;

                rows2.Add(new UserFreshnessDigestRow
                {
                    ToolSlug = displaySlug,
                    ToolTitle = displayTitle,
                    ParameterKey = stale.Key,
                    LastUpdatedDate = stale.LastUpdatedDate,
                    DaysOverdue = Math.Abs(_freshness.DaysUntilStale(stale, now) ?? 0),
                    FrequencyLabelTr = FrequencyLabels.ToTurkish(stale.ExpectedUpdateFrequency),
                    CalculatorUrl = $"{siteUrl.TrimEnd('/')}/hesapla/{ut.ToolSlug}",
                    LastUserCalculation = ut.LastUsed
                });
            }
        }

        var settingsUrl = $"{siteUrl.TrimEnd('/')}/hesap/ayarlar";
        var totalUsers = historyData.Select(h => h.UserId).Distinct().Count();
        var notifications = 0;
        var emails = 0;

        foreach (var (userId, rows) in userToRows)
        {
            if (rows.Count == 0) continue;

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) continue;

            var userNotif = await _notifications.CreateAsync(
                type: NotificationType.DataFreshness,
                userId: userId,
                title: rows.Count == 1
                    ? "Kullandığınız bir hesaplayıcı güncellenmiş olabilir"
                    : $"Kullandığınız {rows.Count} hesaplayıcı güncellenmiş olabilir",
                body: "Geçtiğimiz aylarda kullandığınız hesaplayıcılarda parametre " +
                      "güncellemeleri var. Detay e-postanızda; hesap ayarlarınızdan " +
                      "bildirim tercihlerinizi yönetebilirsiniz.",
                link: "/bildirimler",
                relatedEntityType: "UserFreshnessDigest",
                relatedEntityId: now.DayOfYear,
                iconHint: "info",
                dedupWindow: TimeSpan.FromDays(7),
                ct: ct);

            if (userNotif != null)
            {
                notifications++;

                if (user.NotificationsEmailEnabled && !string.IsNullOrWhiteSpace(user.Email))
                {
                    try
                    {
                        var model = new UserFreshnessDigestModel
                        {
                            UserName = user.FullName ?? user.UserName ?? "Kullanıcı",
                            AffectedTools = rows,
                            SettingsUrl = settingsUrl,
                            GeneratedAt = now
                        };

                        var html = await _renderer.RenderAsync("UserFreshnessDigest", model, ct);

                        var sent = await _email.SendAsync(new EmailMessage(
                            user.Email,
                            user.FullName,
                            rows.Count == 1
                                ? "Kullandığınız hesaplayıcı güncellenmiş olabilir"
                                : $"Kullandığınız {rows.Count} hesaplayıcı güncellenmiş olabilir",
                            html), ct);

                        if (sent) emails++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "User digest e-posta hatası: UserId={UserId}", userId);
                    }
                }
            }
        }

        return (notifications, emails, totalUsers, userToRows.Count);
    }
}
