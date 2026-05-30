using FluentAssertions;
using LexCalculus.Core.Email;
using LexCalculus.Core.Email.Models;
using LexCalculus.Core.Entities.Email;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Jobs.Messaging;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LexCalculus.Tests.Jobs;

/// <summary>
/// Faz 6.2 P2 — ProcessMessageDigestJob: 5 dk eşik + user-level all-pending
/// gruplama + master/granüler gating. Render Moq'lanır.
/// </summary>
public class ProcessMessageDigestJobTests : SqlServerTestBase
{
    private static ApplicationUser MakeUser(string suffix, bool active = true, bool master = true) => new()
    {
        UserName = $"{suffix}@x.com", NormalizedUserName = $"{suffix.ToUpperInvariant()}@X.COM",
        Email = $"{suffix}@x.com", NormalizedEmail = $"{suffix.ToUpperInvariant()}@X.COM",
        FullName = $"User {suffix}", CreatedAt = DateTime.UtcNow,
        IsActive = active, EmailConfirmed = true, NotificationsEmailEnabled = master,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static int SeedUser(ApplicationDbContext ctx, bool master = true, bool pref = true, bool active = true)
    {
        var user = MakeUser("digest", active, master);
        ctx.Users.Add(user); ctx.SaveChanges();
        ctx.UserProfiles.Add(new UserProfile { UserId = user.Id, DisplayName = "Digest User", EmailOnMessageDigest = pref });
        ctx.SaveChanges();
        return user.Id;
    }

    private static void AddEntry(ApplicationDbContext ctx, int userId, DateTime createdAt) =>
        ctx.EmailDigestEntries.Add(new EmailDigestEntry
        {
            UserId = userId, Type = EmailDigestType.Message,
            RelatedEntityId = 1, CreatedAt = createdAt, IsSent = false
        });

    private static (Mock<IEmailService> email, ProcessMessageDigestJob job) Build(ApplicationDbContext ctx)
    {
        var email = new Mock<IEmailService>();
        email.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var renderer = new Mock<IEmailTemplateRenderer>();
        renderer.Setup(r => r.RenderAsync(It.IsAny<string>(), It.IsAny<MessageDigestEmailModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("<html>digest</html>");

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SeoSettings:SiteUrl"] = "https://test.local"
        }).Build();

        var job = new ProcessMessageDigestJob(ctx, email.Object, renderer.Object, config,
            NullLogger<ProcessMessageDigestJob>.Instance);
        return (email, job);
    }

    [Fact]
    public async Task ExecuteAsync_NoEntries_DoesNothing()
    {
        await using var ctx = _db.Create();
        SeedUser(ctx);
        var (email, job) = Build(ctx);

        await job.ExecuteAsync();

        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_EntriesUnderThreshold_Skips()
    {
        await using var ctx = _db.Create();
        var userId = SeedUser(ctx);
        AddEntry(ctx, userId, DateTime.UtcNow);   // taze (< 5 dk)
        await ctx.SaveChangesAsync();

        var (email, job) = Build(ctx);
        await job.ExecuteAsync();

        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        (await ctx.EmailDigestEntries.CountAsync(e => !e.IsSent)).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_EntriesOverThreshold_SendsAndMarksSent()
    {
        await using var ctx = _db.Create();
        var userId = SeedUser(ctx);
        AddEntry(ctx, userId, DateTime.UtcNow.AddMinutes(-6));
        await ctx.SaveChangesAsync();

        var (email, job) = Build(ctx);
        await job.ExecuteAsync();

        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        (await ctx.EmailDigestEntries.CountAsync(e => !e.IsSent)).Should().Be(0);
        (await ctx.EmailDigestEntries.FirstAsync()).SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_MasterSwitchOff_SkipsUser()
    {
        await using var ctx = _db.Create();
        var userId = SeedUser(ctx, master: false);
        AddEntry(ctx, userId, DateTime.UtcNow.AddMinutes(-6));
        await ctx.SaveChangesAsync();

        var (email, job) = Build(ctx);
        await job.ExecuteAsync();

        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        // Kuyruğu şişirmemek için kalemler "işlendi" işaretlenir (tekrar denenmez)
        (await ctx.EmailDigestEntries.CountAsync(e => !e.IsSent)).Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_PreferenceOff_SkipsUser()
    {
        await using var ctx = _db.Create();
        var userId = SeedUser(ctx, pref: false);
        AddEntry(ctx, userId, DateTime.UtcNow.AddMinutes(-6));
        await ctx.SaveChangesAsync();

        var (email, job) = Build(ctx);
        await job.ExecuteAsync();

        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        (await ctx.EmailDigestEntries.CountAsync(e => !e.IsSent)).Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_GroupsAllPendingPerUser()
    {
        await using var ctx = _db.Create();
        var userId = SeedUser(ctx);
        AddEntry(ctx, userId, DateTime.UtcNow.AddMinutes(-6));   // eşiği geçti (tetikleyici)
        AddEntry(ctx, userId, DateTime.UtcNow.AddMinutes(-1));   // eşik altında ama aynı user → dahil
        await ctx.SaveChangesAsync();

        var (email, job) = Build(ctx);
        await job.ExecuteAsync();

        // Tek e-posta, 2 mesaj birlikte
        email.Verify(e => e.SendAsync(
            It.Is<EmailMessage>(m => m.Subject == "2 yeni mesajınız var"),
            It.IsAny<CancellationToken>()), Times.Once);
        (await ctx.EmailDigestEntries.CountAsync(e => !e.IsSent)).Should().Be(0);
    }
}
