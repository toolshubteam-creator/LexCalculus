using FluentAssertions;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Notifications;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Admin.Dashboard;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Infrastructure.Notifications;
using LexCalculus.Tests.TestHelpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LexCalculus.Tests.Admin.Dashboard;

public class DashboardSummaryServiceTests : SqlServerTestBase
{
    private static UserManager<ApplicationUser> MockUserManagerWithDbUsers(
        LexCalculus.Infrastructure.Data.ApplicationDbContext ctx)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        // Users IQueryable real DbSet'e bağlı → .CountAsync() çalışır
        mgr.Setup(x => x.Users).Returns(ctx.Users);
        return mgr.Object;
    }

    private static DashboardSummaryService CreateService(
        LexCalculus.Infrastructure.Data.ApplicationDbContext ctx,
        IFormulaFreshnessChecker? freshness = null)
    {
        var notifications = new NotificationService(ctx, NullLogger<NotificationService>.Instance);
        var registry = new Mock<ICalculatorRegistry>();
        registry.Setup(r => r.GetAll()).Returns(Array.Empty<CalculatorMetadata>());

        return new DashboardSummaryService(
            ctx,
            freshness ?? new FormulaFreshnessChecker(),
            notifications,
            MockUserManagerWithDbUsers(ctx),
            registry.Object,
            NullLogger<DashboardSummaryService>.Instance);
    }

    [Fact]
    public async Task GetSummaryAsync_HappyPath_ReturnsAllDbWidgets()
    {
        await using var ctx = _db.Create();

        // 2 aktif kullanıcı — önce kaydet, üretilen Id'leri al
        var u1 = new ApplicationUser { UserName = "u1", IsActive = true, LastLoginAt = DateTime.UtcNow.AddDays(-5) };
        var u2 = new ApplicationUser { UserName = "u2", IsActive = true, LastLoginAt = DateTime.UtcNow.AddDays(-40) };
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        // 5 parametre, 1'i stale (Biannual + 200 gün eski)
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "kidem", Key = "tavan", Value = 1m,
                EffectiveDate = DateTime.UtcNow.AddDays(-300),
                ExpectedUpdateFrequency = "Biannual",
                LastUpdatedDate = DateTime.UtcNow.AddDays(-200) },  // STALE
            new FormulaParameter { ToolSlug = "kidem", Key = "asgari", Value = 1m,
                EffectiveDate = DateTime.UtcNow.AddDays(-30),
                ExpectedUpdateFrequency = "Yearly",
                LastUpdatedDate = DateTime.UtcNow.AddDays(-30) },
            new FormulaParameter { ToolSlug = "ihbar", Key = "katsayi", Value = 1m,
                EffectiveDate = DateTime.UtcNow.AddDays(-30),
                ExpectedUpdateFrequency = "Monthly",
                LastUpdatedDate = DateTime.UtcNow.AddDays(-10) },
            new FormulaParameter { ToolSlug = "yasal-faiz", Key = "rate", Value = 1m,
                EffectiveDate = DateTime.UtcNow.AddDays(-30),
                ExpectedUpdateFrequency = "Quarterly",
                LastUpdatedDate = DateTime.UtcNow.AddDays(-30) },
            new FormulaParameter { ToolSlug = "*", Key = "TC_REGISTRY", Value = 1m,
                EffectiveDate = DateTime.UtcNow.AddDays(-30),
                ExpectedUpdateFrequency = "OnLawChange",
                LastUpdatedDate = DateTime.UtcNow.AddDays(-30) }
        );

        // 3 hesap geçmişi (son 7 gün — auditor CreatedAt=now)
        ctx.Set<CalculationHistory>().AddRange(
            new CalculationHistory { UserId = u1.Id, CategorySlug = "is-hukuku", ToolSlug = "kidem",
                ToolTitle = "Kıdem", InputJson = "{}", OutputJson = "{}" },
            new CalculationHistory { UserId = u1.Id, CategorySlug = "is-hukuku", ToolSlug = "kidem",
                ToolTitle = "Kıdem", InputJson = "{}", OutputJson = "{}" },
            new CalculationHistory { UserId = u2.Id, CategorySlug = "faiz", ToolSlug = "yasal-faiz",
                ToolTitle = "Yasal Faiz", InputJson = "{}", OutputJson = "{}" }
        );

        // 1 okunmamış bildirim (admin u1 için)
        ctx.Notifications.Add(new Notification
        {
            UserId = u1.Id, Type = NotificationType.SystemAlert,
            Title = "T", Body = "B", IsRead = false, CreatedAt = DateTime.UtcNow
        });

        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var summary = await svc.GetSummaryAsync(currentAdminUserId: u1.Id);

        // Veri Tazelik
        summary.Freshness.Should().NotBeNull();
        summary.Freshness!.TotalParameters.Should().Be(5);
        summary.Freshness.StaleCount.Should().Be(1);

        // Hesap Aktivitesi (son 7 gün)
        summary.Activity.Should().NotBeNull();
        summary.Activity!.TotalLast7Days.Should().Be(3);
        summary.Activity.TopTools.Should().HaveCountGreaterThan(0);
        summary.Activity.TopTools.First().UsageCount.Should().Be(2);   // kidem en çok kullanılan

        // Kullanıcılar
        summary.Users.Should().NotBeNull();
        summary.Users!.TotalActiveUsers.Should().Be(2);
        summary.Users.ActiveLast30Days.Should().Be(1);   // sadece u1 son 30 günde

        // Bildirimler
        summary.Notifications.Should().NotBeNull();
        summary.Notifications!.UnreadForCurrentAdmin.Should().Be(1);

        // Jobs widget Hangfire static API'sine bağlı; test ortamında configure
        // edilmemişse SafeRun yakalar ve null döner. Defensive davranışın bir
        // parçası — null ya da non-null kabul, fail etmiyor olmalı.
    }

    [Fact]
    public async Task GetSummaryAsync_FreshnessFails_OtherWidgetsStillReturn()
    {
        await using var ctx = _db.Create();
        ctx.Set<FormulaParameter>().Add(new FormulaParameter
        {
            ToolSlug = "kidem", Key = "tavan", Value = 1m,
            EffectiveDate = DateTime.UtcNow.AddDays(-30),
            ExpectedUpdateFrequency = "Monthly",
            LastUpdatedDate = DateTime.UtcNow.AddDays(-10)
        });
        var user = new ApplicationUser { UserName = "u", IsActive = true };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var freshnessMock = new Mock<IFormulaFreshnessChecker>();
        freshnessMock.Setup(x => x.IsStale(It.IsAny<FormulaParameter>(), It.IsAny<DateTime>()))
                     .Throws<InvalidOperationException>();

        var svc = CreateService(ctx, freshnessMock.Object);
        var summary = await svc.GetSummaryAsync(currentAdminUserId: user.Id);

        summary.Freshness.Should().BeNull();         // throw → defensive null
        summary.Activity.Should().NotBeNull();        // bağımsız çalıştı
        summary.Users.Should().NotBeNull();           // bağımsız çalıştı
        summary.Notifications.Should().NotBeNull();   // bağımsız çalıştı
        // Jobs Hangfire static'e bağlı — test ortamında null ya da non-null
    }
}
