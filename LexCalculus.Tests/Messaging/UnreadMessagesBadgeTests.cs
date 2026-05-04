using System.Net;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Messaging;

[Collection("AdminWebHost")]
public class UnreadMessagesBadgeTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public UnreadMessagesBadgeTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAnonClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient CreateAuthClient(int userId, string email)
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-User", email);
        c.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return c;
    }

    private async Task<ApplicationUser> SeedUserAsync(string email)
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
        return u;
    }

    private async Task CleanupUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var u = await ctx.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (u is null) return;
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

    private async Task SeedUnreadMessageAsync(int senderId, int recipientId, string body)
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
            User1Id = u1,
            User2Id = u2,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
            // LastReadAt'lar null → recipient için unread
        };
        ctx.Conversations.Add(conv);
        await ctx.SaveChangesAsync();
        ctx.Messages.Add(new Message
        {
            ConversationId = conv.Id,
            SenderId = senderId,
            Body = body,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task UnreadBadge_AnonymousUser_NoBadge()
    {
        using var client = CreateAnonClient();
        var resp = await client.GetAsync("/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();
        html.Should().NotContain("nav-badge--unread", "anonim için badge render edilmez");
    }

    [Fact]
    public async Task UnreadBadge_NoUnreadMessages_NoBadge()
    {
        var u = await SeedUserAsync("ub-none@example.com");
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!);
            // [Authorize] sayfası → _Layout render edilirken authenticated user için
            // header-meta blokuna girer (SignInManager IsSignedIn check'i atlamadan).
            var resp = await client.GetAsync("/profil");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var html = await resp.Content.ReadAsStringAsync();
            html.Should().NotContain("nav-badge--unread", "0 unread → badge yok");
            html.Should().Contain("/mesajlar", "Mesajlar nav linki render edilmiş");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task UnreadBadge_WithUnreadMessages_RendersCount()
    {
        var sender = await SeedUserAsync("ub-s@example.com");
        var recipient = await SeedUserAsync("ub-r@example.com");
        try
        {
            await SeedUnreadMessageAsync(sender.Id, recipient.Id, "okumadın");

            using var client = CreateAuthClient(recipient.Id, recipient.Email!);
            var resp = await client.GetAsync("/profil");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var html = await resp.Content.ReadAsStringAsync();
            html.Should().Contain("nav-badge--unread", "unread > 0 → badge görünür");
        }
        finally
        {
            await CleanupUserAsync(sender.Email!);
            await CleanupUserAsync(recipient.Email!);
        }
    }
}
