using LexCalculus.Core.Calculators;
using LexCalculus.Core.Email;
using LexCalculus.Core.Email.Models;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Data;
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
    private readonly IConfiguration _config;
    private readonly ILogger<DataFreshnessCheckJob> _logger;

    public DataFreshnessCheckJob(
        ApplicationDbContext ctx,
        IFormulaFreshnessChecker freshness,
        INotificationService notifications,
        IEmailService email,
        IEmailTemplateRenderer renderer,
        UserManager<ApplicationUser> userManager,
        IConfiguration config,
        ILogger<DataFreshnessCheckJob> logger)
    {
        _ctx = ctx;
        _freshness = freshness;
        _notifications = notifications;
        _email = email;
        _renderer = renderer;
        _userManager = userManager;
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

            if (notification != null) notificationsCreated++;

            // 5b. E-posta (opt-in)
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

        _logger.LogInformation(
            "DataFreshnessCheckJob bitti: {Notifications} bildirim, {Emails} e-posta " +
            "({Admins} admin'e), {StaleCount} stale parametre.",
            notificationsCreated, emailsSent, admins.Count, staleLatest.Count);
    }
}
