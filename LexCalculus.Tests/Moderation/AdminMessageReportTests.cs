using System.Net;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Entities.Moderation;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Moderation;

/// <summary>
/// Faz 5.7 — admin /admin/sikayetler sayfasında mesaj şikayetlerinin
/// render edildiği ve hide aksiyonunun doğru çalıştığı integration test.
/// </summary>
[Collection("AdminWebHost")]
public class AdminMessageReportTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public AdminMessageReportTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAdminClient(int userId, string email)
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-User", email);
        c.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        c.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");
        return c;
    }

    private async Task<ApplicationUser> SeedUserAsync(string email, bool isAdmin = false)
    {
        await CleanupUserAsync(email);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Tester " + email,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        var r = await um.CreateAsync(u, "ValidPass123!");
        r.Succeeded.Should().BeTrue();
        if (isAdmin) await um.AddToRoleAsync(u, "Admin");
        return u;
    }

    private async Task CleanupUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var u = await ctx.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (u is null) return;
        var reports = await ctx.ContentReports.Where(r => r.ReporterId == u.Id).ToListAsync();
        ctx.ContentReports.RemoveRange(reports);
        var convs = await ctx.Conversations
            .Where(c => c.User1Id == u.Id || c.User2Id == u.Id)
            .ToListAsync();
        var convIds = convs.Select(c => c.Id).ToList();
        var msgs = await ctx.Messages.Where(m => convIds.Contains(m.ConversationId)).ToListAsync();
        ctx.Messages.RemoveRange(msgs);
        ctx.Conversations.RemoveRange(convs);
        var conns = await ctx.UserConnections
            .Where(c => c.RequesterId == u.Id || c.TargetId == u.Id)
            .ToListAsync();
        ctx.UserConnections.RemoveRange(conns);
        var profile = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == u.Id);
        if (profile is not null) ctx.UserProfiles.Remove(profile);
        var roles = ctx.UserRoles.Where(ur => ur.UserId == u.Id);
        ctx.UserRoles.RemoveRange(roles);
        ctx.Users.Remove(u);
        await ctx.SaveChangesAsync();
    }

    private async Task<(int convId, int messageId)> SeedConversationWithReportAsync(
        int senderId, int recipientId, string body)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = senderId,
            TargetId = recipientId,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            RespondedAt = DateTime.UtcNow.AddDays(-1)
        });
        var u1 = Math.Min(senderId, recipientId);
        var u2 = Math.Max(senderId, recipientId);
        var conv = new Conversation
        {
            User1Id = u1, User2Id = u2,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };
        ctx.Conversations.Add(conv);
        await ctx.SaveChangesAsync();
        var msg = new Message
        {
            ConversationId = conv.Id,
            SenderId = senderId,
            Body = body,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
            IsModeratorHidden = false
        };
        ctx.Messages.Add(msg);
        await ctx.SaveChangesAsync();
        ctx.ContentReports.Add(new ContentReport
        {
            TargetType = ContentReportTargetType.Message,
            TargetId = msg.Id,
            ReporterId = recipientId,
            Reason = ContentReportReason.Harassment,
            Note = "taciz",
            Status = ContentReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        return (conv.Id, msg.Id);
    }

    [Fact]
    public async Task AdminIndex_MessageReport_RendersMessageBadge()
    {
        var sender = await SeedUserAsync("amr-s@example.com");
        var recipient = await SeedUserAsync("amr-r@example.com");
        var admin = await SeedUserAsync("amr-admin@example.com", isAdmin: true);
        try
        {
            await SeedConversationWithReportAsync(sender.Id, recipient.Id, "<p>Şikayet edilmiş mesaj içerik</p>");

            using var client = CreateAdminClient(admin.Id, admin.Email!);
            var resp = await client.GetAsync("/admin/sikayetler");

            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var html = await resp.Content.ReadAsStringAsync();
            html.Should().Contain("param-badge--message", "Mesaj badge CSS class render edildi");
        }
        finally
        {
            await CleanupUserAsync(sender.Email!);
            await CleanupUserAsync(recipient.Email!);
            await CleanupUserAsync(admin.Email!);
        }
    }

    [Fact]
    public async Task AdminDetail_MessageReport_RendersBody()
    {
        var sender = await SeedUserAsync("amrd-s@example.com");
        var recipient = await SeedUserAsync("amrd-r@example.com");
        var admin = await SeedUserAsync("amrd-admin@example.com", isAdmin: true);
        try
        {
            var (_, msgId) = await SeedConversationWithReportAsync(
                sender.Id, recipient.Id, "<p>kabaca mesaj</p>");

            using var client = CreateAdminClient(admin.Id, admin.Email!);
            var resp = await client.GetAsync(
                $"/admin/sikayetler/{(int)ContentReportTargetType.Message}/{msgId}");

            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var html = await resp.Content.ReadAsStringAsync();
            html.Should().Contain("kabaca mesaj");
        }
        finally
        {
            await CleanupUserAsync(sender.Email!);
            await CleanupUserAsync(recipient.Email!);
            await CleanupUserAsync(admin.Email!);
        }
    }

    [Fact]
    public async Task AdminHide_Message_SetsIsModeratorHidden()
    {
        var sender = await SeedUserAsync("amrh-s@example.com");
        var recipient = await SeedUserAsync("amrh-r@example.com");
        var admin = await SeedUserAsync("amrh-admin@example.com", isAdmin: true);
        try
        {
            var (_, msgId) = await SeedConversationWithReportAsync(
                sender.Id, recipient.Id, "<p>gizlenecek</p>");

            using var client = CreateAdminClient(admin.Id, admin.Email!);
            // CSRF token detail sayfasından
            var detailHtml = await client.GetStringAsync(
                $"/admin/sikayetler/{(int)ContentReportTargetType.Message}/{msgId}");
            var tokenMatch = System.Text.RegularExpressions.Regex.Match(detailHtml,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            tokenMatch.Success.Should().BeTrue();

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", tokenMatch.Groups[1].Value),
                new KeyValuePair<string, string>("reviewNote", "ihlal")
            });
            var resp = await client.PostAsync(
                $"/admin/sikayetler/{(int)ContentReportTargetType.Message}/{msgId}/hide", content);

            ((int)resp.StatusCode).Should().BeOneOf(
                (int)HttpStatusCode.Redirect,
                (int)HttpStatusCode.Found);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var msg = await ctx.Messages.FirstAsync(m => m.Id == msgId);
            msg.IsModeratorHidden.Should().BeTrue();
        }
        finally
        {
            await CleanupUserAsync(sender.Email!);
            await CleanupUserAsync(recipient.Email!);
            await CleanupUserAsync(admin.Email!);
        }
    }
}
