using FluentAssertions;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Email;
using LexCalculus.Core.Email.Models;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Infrastructure.Notifications;
using LexCalculus.Jobs.DataFreshness;
using LexCalculus.Tests.TestHelpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LexCalculus.Tests.Jobs;

public class DataFreshnessCheckJobTests
{
    private static UserManager<ApplicationUser> MockUserManager(
        IList<ApplicationUser> adminUsers,
        IList<ApplicationUser>? allUsers = null)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mgr.Setup(x => x.GetUsersInRoleAsync("Admin")).ReturnsAsync(adminUsers);

        // FindByIdAsync — kullanıcı hedeflemesinde job kullanır
        var pool = (allUsers ?? adminUsers).Concat(adminUsers).Distinct().ToList();
        mgr.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
           .ReturnsAsync((string id) => pool.FirstOrDefault(u => u.Id.ToString() == id));

        return mgr.Object;
    }

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SeoSettings:SiteUrl"] = "https://test.local"
        }).Build();

    private static DataFreshnessCheckJob CreateJob(
        LexCalculus.Infrastructure.Data.ApplicationDbContext ctx,
        IList<ApplicationUser> admins,
        Mock<IEmailService> emailMock,
        Mock<IEmailTemplateRenderer> rendererMock,
        IList<ApplicationUser>? regularUsers = null)
    {
        var freshness = new FormulaFreshnessChecker();
        var notifications = new NotificationService(ctx, NullLogger<NotificationService>.Instance);

        rendererMock.Setup(r => r.RenderAsync(It.IsAny<string>(), It.IsAny<AdminFreshnessDigestModel>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("<html>admin digest</html>");
        rendererMock.Setup(r => r.RenderAsync(It.IsAny<string>(), It.IsAny<UserFreshnessDigestModel>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("<html>user digest</html>");

        var registryMock = new Mock<ICalculatorRegistry>();
        registryMock.Setup(r => r.GetAll()).Returns(Array.Empty<CalculatorMetadata>());

        var allUsers = admins.Concat(regularUsers ?? Array.Empty<ApplicationUser>()).ToList();

        return new DataFreshnessCheckJob(
            ctx,
            freshness,
            notifications,
            emailMock.Object,
            rendererMock.Object,
            MockUserManager(admins, allUsers),
            registryMock.Object,
            BuildConfig(),
            NullLogger<DataFreshnessCheckJob>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_NoStaleParameters_DoesNothing()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().Add(new FormulaParameter
        {
            ToolSlug = "test", Key = "rate", Value = 1m,
            EffectiveDate = DateTime.UtcNow,
            ExpectedUpdateFrequency = "Yearly",
            LastUpdatedDate = DateTime.UtcNow.AddDays(-30)  // çok taze
        });
        await ctx.SaveChangesAsync();

        var admins = new List<ApplicationUser>
        {
            new() { Id = 1, UserName = "admin", Email = "admin@test.local", NotificationsEmailEnabled = true }
        };

        var emailMock = new Mock<IEmailService>();
        var rendererMock = new Mock<IEmailTemplateRenderer>();
        var job = CreateJob(ctx, admins, emailMock, rendererMock);

        await job.ExecuteAsync();

        var notifCount = await ctx.Notifications.CountAsync();
        notifCount.Should().Be(0);
        emailMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_OneStaleParameter_CreatesNotificationForEachAdmin()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().Add(new FormulaParameter
        {
            ToolSlug = "kidem", Key = "tavan", Value = 50000m,
            EffectiveDate = DateTime.UtcNow.AddDays(-365),
            ExpectedUpdateFrequency = "Biannual",
            LastUpdatedDate = DateTime.UtcNow.AddDays(-200)  // 200 > 190 (Biannual tolerance)
        });
        await ctx.SaveChangesAsync();

        var admins = new List<ApplicationUser>
        {
            new() { Id = 1, UserName = "admin1", Email = "a1@test.local", NotificationsEmailEnabled = true },
            new() { Id = 2, UserName = "admin2", Email = "a2@test.local", NotificationsEmailEnabled = true }
        };

        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var rendererMock = new Mock<IEmailTemplateRenderer>();
        var job = CreateJob(ctx, admins, emailMock, rendererMock);

        await job.ExecuteAsync();

        var notifCount = await ctx.Notifications.CountAsync();
        notifCount.Should().Be(2);
        emailMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_AdminWithEmailDisabled_DoesNotReceiveEmail_ButGetsNotification()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().Add(new FormulaParameter
        {
            ToolSlug = "kidem", Key = "tavan", Value = 50000m,
            EffectiveDate = DateTime.UtcNow.AddDays(-365),
            ExpectedUpdateFrequency = "Biannual",
            LastUpdatedDate = DateTime.UtcNow.AddDays(-200)
        });
        await ctx.SaveChangesAsync();

        var admins = new List<ApplicationUser>
        {
            new() { Id = 1, UserName = "admin", Email = "admin@test.local", NotificationsEmailEnabled = false }
        };

        var emailMock = new Mock<IEmailService>();
        var rendererMock = new Mock<IEmailTemplateRenderer>();
        var job = CreateJob(ctx, admins, emailMock, rendererMock);

        await job.ExecuteAsync();

        var notifCount = await ctx.Notifications.CountAsync();
        notifCount.Should().Be(1);  // in-app notification yine var
        emailMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_RunTwiceInSameWindow_DedupPreventsSecondNotification()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().Add(new FormulaParameter
        {
            ToolSlug = "kidem", Key = "tavan", Value = 50000m,
            EffectiveDate = DateTime.UtcNow.AddDays(-365),
            ExpectedUpdateFrequency = "Biannual",
            LastUpdatedDate = DateTime.UtcNow.AddDays(-200)
        });
        await ctx.SaveChangesAsync();

        var admins = new List<ApplicationUser>
        {
            new() { Id = 1, UserName = "admin", Email = "admin@test.local", NotificationsEmailEnabled = true }
        };

        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var rendererMock = new Mock<IEmailTemplateRenderer>();
        var job = CreateJob(ctx, admins, emailMock, rendererMock);

        await job.ExecuteAsync();
        await job.ExecuteAsync();   // 2. çağrı dedup hit

        var notifCount = await ctx.Notifications.CountAsync();
        notifCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_GroupsBySlugAndKey_OnlyChecksLatestEffectiveDate()
    {
        await using var ctx = TestDbContextFactory.Create();
        // Aynı (slug, key) için 3 satır: en yeni 2026-04-01 fresh,
        // önceki 2 versiyon LastUpdatedDate eski (stale gibi görünür)
        // ama job sadece en yeniyi kontrol etmeli → 0 stale.
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter
            {
                ToolSlug = "kidem", Key = "tavan", Value = 30000m,
                EffectiveDate = new DateTime(2020, 1, 1),
                ExpectedUpdateFrequency = "Monthly",
                LastUpdatedDate = new DateTime(2020, 1, 1)  // çok eski
            },
            new FormulaParameter
            {
                ToolSlug = "kidem", Key = "tavan", Value = 40000m,
                EffectiveDate = new DateTime(2024, 1, 1),
                ExpectedUpdateFrequency = "Monthly",
                LastUpdatedDate = new DateTime(2024, 1, 1)  // 2 yıl eski
            },
            new FormulaParameter
            {
                ToolSlug = "kidem", Key = "tavan", Value = 50000m,
                EffectiveDate = new DateTime(2026, 4, 1),
                ExpectedUpdateFrequency = "Monthly",
                LastUpdatedDate = DateTime.UtcNow.AddDays(-10)  // FRESH
            }
        );
        await ctx.SaveChangesAsync();

        var admins = new List<ApplicationUser>
        {
            new() { Id = 1, UserName = "admin", Email = "admin@test.local", NotificationsEmailEnabled = true }
        };

        var emailMock = new Mock<IEmailService>();
        var rendererMock = new Mock<IEmailTemplateRenderer>();
        var job = CreateJob(ctx, admins, emailMock, rendererMock);

        await job.ExecuteAsync();

        // Latest (2026-04-01) fresh → 0 stale; 0 notification, 0 e-posta
        var notifCount = await ctx.Notifications.CountAsync();
        notifCount.Should().Be(0);
        emailMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NoUsersUsedAffectedTool_NoUserNotifications()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().Add(new FormulaParameter
        {
            ToolSlug = "kidem", Key = "tavan", Value = 50000m,
            EffectiveDate = DateTime.UtcNow.AddDays(-365),
            ExpectedUpdateFrequency = "Biannual",
            LastUpdatedDate = DateTime.UtcNow.AddDays(-200)
        });
        // Bir regular kullanıcı var ama hiçbir hesaplama geçmişi yok → user notif yok
        await ctx.SaveChangesAsync();

        var admins = new List<ApplicationUser>
        {
            new() { Id = 1, UserName = "admin", Email = "admin@test.local", NotificationsEmailEnabled = true }
        };
        var regulars = new List<ApplicationUser>
        {
            new() { Id = 2, UserName = "user", Email = "user@test.local", NotificationsEmailEnabled = true }
        };

        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var rendererMock = new Mock<IEmailTemplateRenderer>();
        var job = CreateJob(ctx, admins, emailMock, rendererMock, regulars);

        await job.ExecuteAsync();

        // Sadece admin notif + email
        var notifCount = await ctx.Notifications.CountAsync();
        notifCount.Should().Be(1);
        emailMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UserUsedAffectedToolInLast90Days_GetsNotification()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().Add(new FormulaParameter
        {
            ToolSlug = "kidem", Key = "tavan", Value = 50000m,
            EffectiveDate = DateTime.UtcNow.AddDays(-365),
            ExpectedUpdateFrequency = "Biannual",
            LastUpdatedDate = DateTime.UtcNow.AddDays(-200)
        });
        ctx.Set<CalculationHistory>().Add(new CalculationHistory
        {
            UserId = 2,
            CategorySlug = "is-hukuku",
            ToolSlug = "kidem",
            ToolTitle = "Kıdem Tazminatı",
            InputJson = "{}",
            OutputJson = "{}"
        });
        await ctx.SaveChangesAsync();

        var admins = new List<ApplicationUser>
        {
            new() { Id = 1, UserName = "admin", Email = "admin@test.local", NotificationsEmailEnabled = true }
        };
        var regulars = new List<ApplicationUser>
        {
            new() { Id = 2, UserName = "user", Email = "user@test.local", NotificationsEmailEnabled = true }
        };

        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var rendererMock = new Mock<IEmailTemplateRenderer>();
        var job = CreateJob(ctx, admins, emailMock, rendererMock, regulars);

        await job.ExecuteAsync();

        // 1 admin notif + 1 user notif = 2; her iki opt-in → 2 email
        var notifCount = await ctx.Notifications.CountAsync();
        notifCount.Should().Be(2);
        emailMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_UserUsedDifferentTool_NoNotification()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().Add(new FormulaParameter
        {
            ToolSlug = "kidem", Key = "tavan", Value = 50000m,
            EffectiveDate = DateTime.UtcNow.AddDays(-365),
            ExpectedUpdateFrequency = "Biannual",
            LastUpdatedDate = DateTime.UtcNow.AddDays(-200)
        });
        // Kullanıcı farklı (etkilenmeyen) bir tool kullandı
        ctx.Set<CalculationHistory>().Add(new CalculationHistory
        {
            UserId = 2,
            CategorySlug = "is-hukuku",
            ToolSlug = "ihbar",
            ToolTitle = "İhbar Tazminatı",
            InputJson = "{}",
            OutputJson = "{}"
        });
        await ctx.SaveChangesAsync();

        var admins = new List<ApplicationUser>
        {
            new() { Id = 1, UserName = "admin", Email = "admin@test.local", NotificationsEmailEnabled = true }
        };
        var regulars = new List<ApplicationUser>
        {
            new() { Id = 2, UserName = "user", Email = "user@test.local", NotificationsEmailEnabled = true }
        };

        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var rendererMock = new Mock<IEmailTemplateRenderer>();
        var job = CreateJob(ctx, admins, emailMock, rendererMock, regulars);

        await job.ExecuteAsync();

        // Sadece admin notif + email; "ihbar" stale değil → user notif yok
        var notifCount = await ctx.Notifications.CountAsync();
        notifCount.Should().Be(1);
        emailMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_GlobalStarStale_AlertsAllRecentUsers()
    {
        await using var ctx = TestDbContextFactory.Create();
        // Global stale parametre (slug="*"): tüm araçları etkiler
        ctx.Set<FormulaParameter>().Add(new FormulaParameter
        {
            ToolSlug = "*", Key = "TC_REGISTRY", Value = 1m,
            EffectiveDate = DateTime.UtcNow.AddDays(-365),
            ExpectedUpdateFrequency = "Yearly",
            LastUpdatedDate = DateTime.UtcNow.AddDays(-400)  // Yearly tolerance 380
        });
        ctx.Set<CalculationHistory>().AddRange(
            new CalculationHistory
            {
                UserId = 2, CategorySlug = "is-hukuku", ToolSlug = "kidem",
                ToolTitle = "Kıdem", InputJson = "{}", OutputJson = "{}"
            },
            new CalculationHistory
            {
                UserId = 3, CategorySlug = "faiz", ToolSlug = "yasal-faiz",
                ToolTitle = "Yasal Faiz", InputJson = "{}", OutputJson = "{}"
            }
        );
        await ctx.SaveChangesAsync();

        var admins = new List<ApplicationUser>
        {
            new() { Id = 1, UserName = "admin", Email = "admin@test.local", NotificationsEmailEnabled = true }
        };
        var regulars = new List<ApplicationUser>
        {
            new() { Id = 2, UserName = "user2", Email = "u2@test.local", NotificationsEmailEnabled = true },
            new() { Id = 3, UserName = "user3", Email = "u3@test.local", NotificationsEmailEnabled = true }
        };

        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var rendererMock = new Mock<IEmailTemplateRenderer>();
        var job = CreateJob(ctx, admins, emailMock, rendererMock, regulars);

        await job.ExecuteAsync();

        // 1 admin + 2 user = 3 notif; opt-in herkes → 3 email
        var notifCount = await ctx.Notifications.CountAsync();
        notifCount.Should().Be(3);
        emailMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
