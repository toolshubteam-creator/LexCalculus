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
public class MesajlarPageTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public MesajlarPageTests(TestAuthWebApplicationFactory factory)
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

    private async Task<ApplicationUser> SeedUserAsync(string email, string? slug = null)
    {
        await CleanupUserAsync(email);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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

        if (slug is not null)
        {
            ctx.UserProfiles.Add(new UserProfile
            {
                UserId = u.Id,
                DisplayName = "Tester " + email,
                PublicSlug = slug,
                IsPublicProfile = true
            });
            await ctx.SaveChangesAsync();
        }

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

    private async Task<int> SeedConversationAsync(int userA, int userB, string body)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = userA,
            TargetId = userB,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            RespondedAt = DateTime.UtcNow.AddDays(-1)
        });
        var u1 = Math.Min(userA, userB);
        var u2 = Math.Max(userA, userB);
        var conv = new Conversation
        {
            User1Id = u1,
            User2Id = u2,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };
        ctx.Conversations.Add(conv);
        await ctx.SaveChangesAsync();
        ctx.Messages.Add(new Message
        {
            ConversationId = conv.Id,
            SenderId = userA,
            Body = body,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        });
        await ctx.SaveChangesAsync();
        return conv.Id;
    }

    [Fact]
    public async Task IndexPage_Anonymous_RedirectsToLogin()
    {
        using var client = CreateAnonClient();
        var resp = await client.GetAsync("/mesajlar");
        ((int)resp.StatusCode).Should().BeOneOf(
            (int)HttpStatusCode.Redirect,
            (int)HttpStatusCode.Found,
            (int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IndexPage_LoggedInNoConversations_RendersEmpty()
    {
        var u = await SeedUserAsync("mp-i-empty@example.com");
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!);
            var resp = await client.GetAsync("/mesajlar");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var html = await resp.Content.ReadAsStringAsync();
            // Türkçe karakter encoding: "Henüz" "ü" → entity. ASCII parça kullan.
            html.Should().Contain("Hen", "boş state metni var");
            html.Should().Contain("mesaj");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task IndexPage_LoggedInWithConversation_RendersList()
    {
        var a = await SeedUserAsync("mp-il-a@example.com");
        var b = await SeedUserAsync("mp-il-b@example.com");
        try
        {
            await SeedConversationAsync(a.Id, b.Id, "merhaba list");

            using var client = CreateAuthClient(a.Id, a.Email!);
            var resp = await client.GetAsync("/mesajlar");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var html = await resp.Content.ReadAsStringAsync();
            html.Should().Contain("merhaba list", "son mesaj preview render edilir");
        }
        finally
        {
            await CleanupUserAsync(a.Email!);
            await CleanupUserAsync(b.Email!);
        }
    }

    [Fact]
    public async Task DetailPage_NonParticipant_Returns404()
    {
        var a = await SeedUserAsync("mp-d-a@example.com");
        var b = await SeedUserAsync("mp-d-b@example.com");
        var c = await SeedUserAsync("mp-d-c@example.com");
        try
        {
            var convId = await SeedConversationAsync(a.Id, b.Id, "gizli");

            using var client = CreateAuthClient(c.Id, c.Email!);
            var resp = await client.GetAsync($"/mesajlar/{convId}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await CleanupUserAsync(a.Email!);
            await CleanupUserAsync(b.Email!);
            await CleanupUserAsync(c.Email!);
        }
    }

    [Fact]
    public async Task DetailPage_Participant_RendersMessages()
    {
        var a = await SeedUserAsync("mp-dp-a@example.com");
        var b = await SeedUserAsync("mp-dp-b@example.com");
        try
        {
            var convId = await SeedConversationAsync(a.Id, b.Id, "selam detail");

            using var client = CreateAuthClient(a.Id, a.Email!);
            var resp = await client.GetAsync($"/mesajlar/{convId}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var html = await resp.Content.ReadAsStringAsync();
            html.Should().Contain("selam detail");
            html.Should().Contain("data-conversation-id");
        }
        finally
        {
            await CleanupUserAsync(a.Email!);
            await CleanupUserAsync(b.Email!);
        }
    }

    [Fact]
    public async Task YeniPage_ExistingConversation_RedirectsToDetail()
    {
        var a = await SeedUserAsync("mp-ye-a@example.com");
        var b = await SeedUserAsync("mp-ye-b@example.com");
        try
        {
            var convId = await SeedConversationAsync(a.Id, b.Id, "var zaten");

            using var client = CreateAuthClient(a.Id, a.Email!);
            var resp = await client.GetAsync($"/mesajlar/yeni?recipient={b.Id}");
            ((int)resp.StatusCode).Should().BeOneOf(
                (int)HttpStatusCode.Redirect,
                (int)HttpStatusCode.Found);
            resp.Headers.Location?.OriginalString.Should().Contain($"/mesajlar/{convId}");
        }
        finally
        {
            await CleanupUserAsync(a.Email!);
            await CleanupUserAsync(b.Email!);
        }
    }

    [Fact]
    public async Task YeniPage_NewConversation_RendersForm()
    {
        var a = await SeedUserAsync("mp-yn-a@example.com");
        var b = await SeedUserAsync("mp-yn-b@example.com");
        try
        {
            using var client = CreateAuthClient(a.Id, a.Email!);
            var resp = await client.GetAsync($"/mesajlar/yeni?recipient={b.Id}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var html = await resp.Content.ReadAsStringAsync();
            html.Should().Contain("data-recipient-id");
        }
        finally
        {
            await CleanupUserAsync(a.Email!);
            await CleanupUserAsync(b.Email!);
        }
    }

    [Fact]
    public async Task YeniPage_SelfRecipient_RedirectsToIndex()
    {
        var a = await SeedUserAsync("mp-self@example.com");
        try
        {
            using var client = CreateAuthClient(a.Id, a.Email!);
            var resp = await client.GetAsync($"/mesajlar/yeni?recipient={a.Id}");
            ((int)resp.StatusCode).Should().BeOneOf(
                (int)HttpStatusCode.Redirect,
                (int)HttpStatusCode.Found);
            resp.Headers.Location?.OriginalString.Should().Contain("/mesajlar");
        }
        finally
        {
            await CleanupUserAsync(a.Email!);
        }
    }
}
