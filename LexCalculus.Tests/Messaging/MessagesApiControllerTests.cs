using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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

// Adım 5.8 — SQL Server LocalDB integration test (SqlServerTestAuthWebApplicationFactory).
[Collection("AdminWebHost")]
public class MessagesApiControllerTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public MessagesApiControllerTests(SqlServerTestAuthWebApplicationFactory factory)
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
        var msgs = await ctx.Messages.Where(m => m.SenderId == u.Id).ToListAsync();
        ctx.Messages.RemoveRange(msgs);
        var convs = await ctx.Conversations
            .Where(c => c.User1Id == u.Id || c.User2Id == u.Id)
            .ToListAsync();
        // Convs içindeki tüm mesajları sil
        var convIds = convs.Select(c => c.Id).ToList();
        var nestedMsgs = await ctx.Messages.Where(m => convIds.Contains(m.ConversationId)).ToListAsync();
        ctx.Messages.RemoveRange(nestedMsgs);
        ctx.Conversations.RemoveRange(convs);
        var conns = await ctx.UserConnections
            .Where(c => c.RequesterId == u.Id || c.TargetId == u.Id)
            .ToListAsync();
        ctx.UserConnections.RemoveRange(conns);
        var blocks = await ctx.UserBlocks
            .Where(b => b.BlockerId == u.Id || b.BlockedId == u.Id)
            .ToListAsync();
        ctx.UserBlocks.RemoveRange(blocks);
        var profile = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == u.Id);
        if (profile is not null) ctx.UserProfiles.Remove(profile);
        var roles = ctx.UserRoles.Where(ur => ur.UserId == u.Id);
        ctx.UserRoles.RemoveRange(roles);
        ctx.Users.Remove(u);
        await ctx.SaveChangesAsync();
    }

    private async Task SeedConnectionAsync(int userA, int userB)
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
        await ctx.SaveChangesAsync();
    }

    private static async Task<string> GetCsrfAsync(HttpClient client, string url = "/profil")
    {
        var html = await client.GetStringAsync(url);
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!m.Success) throw new InvalidOperationException($"CSRF token not found at {url}");
        return m.Groups[1].Value;
    }

    [Fact]
    public async Task Send_Anonymous_ReturnsUnauthorizedOrRedirect()
    {
        using var client = CreateAnonClient();
        var resp = await client.PostAsJsonAsync("/api/messages/send",
            new { recipientId = 1, body = "test" });
        ((int)resp.StatusCode).Should().BeOneOf(
            (int)HttpStatusCode.Unauthorized,
            (int)HttpStatusCode.Redirect,
            (int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Send_Valid_ReturnsSuccessAndHtml()
    {
        var sender = await SeedUserAsync("msg-send-a@example.com");
        var recipient = await SeedUserAsync("msg-send-b@example.com");
        try
        {
            await SeedConnectionAsync(sender.Id, recipient.Id);

            using var client = CreateAuthClient(sender.Id, sender.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var resp = await client.PostAsJsonAsync("/api/messages/send",
                new { recipientId = recipient.Id, body = "Merhaba dünya" });

            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var data = await resp.Content.ReadFromJsonAsync<SendResp>();
            data.Should().NotBeNull();
            data!.success.Should().BeTrue();
            data.messageId.Should().BeGreaterThan(0);
            data.conversationId.Should().BeGreaterThan(0);
            data.html.Should().Contain("mesaj");
            data.html.Should().Contain("Merhaba");
        }
        finally
        {
            await CleanupUserAsync(sender.Email!);
            await CleanupUserAsync(recipient.Email!);
        }
    }

    [Fact]
    public async Task Send_NoPermission_ReturnsBadRequest()
    {
        // Bağlantısız + farklı tenant → CanMessage=false
        var sender = await SeedUserAsync("msg-nope-a@example.com");
        var recipient = await SeedUserAsync("msg-nope-b@example.com");
        try
        {
            using var client = CreateAuthClient(sender.Id, sender.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var resp = await client.PostAsJsonAsync("/api/messages/send",
                new { recipientId = recipient.Id, body = "selam" });

            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await CleanupUserAsync(sender.Email!);
            await CleanupUserAsync(recipient.Email!);
        }
    }

    [Fact]
    public async Task Send_EmptyBody_ReturnsBadRequest()
    {
        var sender = await SeedUserAsync("msg-empty-a@example.com");
        var recipient = await SeedUserAsync("msg-empty-b@example.com");
        try
        {
            await SeedConnectionAsync(sender.Id, recipient.Id);

            using var client = CreateAuthClient(sender.Id, sender.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var resp = await client.PostAsJsonAsync("/api/messages/send",
                new { recipientId = recipient.Id, body = "   " });

            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await CleanupUserAsync(sender.Email!);
            await CleanupUserAsync(recipient.Email!);
        }
    }

    [Fact]
    public async Task Delete_Owner_ReturnsSuccess()
    {
        var sender = await SeedUserAsync("msg-del-a@example.com");
        var recipient = await SeedUserAsync("msg-del-b@example.com");
        try
        {
            await SeedConnectionAsync(sender.Id, recipient.Id);

            using var client = CreateAuthClient(sender.Id, sender.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var sendResp = await client.PostAsJsonAsync("/api/messages/send",
                new { recipientId = recipient.Id, body = "silinecek" });
            var sendData = await sendResp.Content.ReadFromJsonAsync<SendResp>();

            var delResp = await client.PostAsync(
                $"/api/messages/{sendData!.messageId}/delete", null);
            delResp.StatusCode.Should().Be(HttpStatusCode.OK);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var msg = await ctx.Messages.FirstOrDefaultAsync(m => m.Id == sendData.messageId);
            msg.Should().NotBeNull();
            msg!.IsDeleted.Should().BeTrue("soft delete — Body korunur, IsDeleted true");
            msg.Body.Should().Contain("silinecek");
        }
        finally
        {
            await CleanupUserAsync(sender.Email!);
            await CleanupUserAsync(recipient.Email!);
        }
    }

    [Fact]
    public async Task Delete_NonOwner_ReturnsBadRequest()
    {
        var sender = await SeedUserAsync("msg-do-a@example.com");
        var recipient = await SeedUserAsync("msg-do-b@example.com");
        try
        {
            await SeedConnectionAsync(sender.Id, recipient.Id);

            using var clientA = CreateAuthClient(sender.Id, sender.Email!);
            var tokenA = await GetCsrfAsync(clientA);
            clientA.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenA);

            var sendResp = await clientA.PostAsJsonAsync("/api/messages/send",
                new { recipientId = recipient.Id, body = "kendi mesajı" });
            var sendData = await sendResp.Content.ReadFromJsonAsync<SendResp>();

            // recipient (other) silmeye çalışır → BadRequest
            using var clientB = CreateAuthClient(recipient.Id, recipient.Email!);
            var tokenB = await GetCsrfAsync(clientB);
            clientB.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenB);

            var delResp = await clientB.PostAsync(
                $"/api/messages/{sendData!.messageId}/delete", null);
            delResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await CleanupUserAsync(sender.Email!);
            await CleanupUserAsync(recipient.Email!);
        }
    }

    [Fact]
    public async Task GetByConversation_NonParticipant_Returns404()
    {
        var a = await SeedUserAsync("msg-gc-a@example.com");
        var b = await SeedUserAsync("msg-gc-b@example.com");
        var c = await SeedUserAsync("msg-gc-c@example.com");
        try
        {
            await SeedConnectionAsync(a.Id, b.Id);

            // a, b'ye mesaj at → conv kuruldu
            using var clientA = CreateAuthClient(a.Id, a.Email!);
            var tokenA = await GetCsrfAsync(clientA);
            clientA.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenA);
            var sendResp = await clientA.PostAsJsonAsync("/api/messages/send",
                new { recipientId = b.Id, body = "merhaba" });
            var sendData = await sendResp.Content.ReadFromJsonAsync<SendResp>();

            // c (3. kişi) bu konuşmayı erişmeye çalışır
            using var clientC = CreateAuthClient(c.Id, c.Email!);
            var resp = await clientC.GetAsync($"/api/messages/{sendData!.conversationId}");
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
    public async Task GetByConversation_Participant_ReturnsMessages()
    {
        var a = await SeedUserAsync("msg-gp-a@example.com");
        var b = await SeedUserAsync("msg-gp-b@example.com");
        try
        {
            await SeedConnectionAsync(a.Id, b.Id);

            using var clientA = CreateAuthClient(a.Id, a.Email!);
            var tokenA = await GetCsrfAsync(clientA);
            clientA.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenA);
            var sendResp = await clientA.PostAsJsonAsync("/api/messages/send",
                new { recipientId = b.Id, body = "ilk mesaj" });
            var sendData = await sendResp.Content.ReadFromJsonAsync<SendResp>();

            var resp = await clientA.GetAsync($"/api/messages/{sendData!.conversationId}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var json = await resp.Content.ReadAsStringAsync();
            json.Should().Contain("ilk mesaj");
            json.Should().Contain("messages");
        }
        finally
        {
            await CleanupUserAsync(a.Email!);
            await CleanupUserAsync(b.Email!);
        }
    }

    [Fact]
    public async Task GetNewSince_ReturnsOnlyOtherSenderMessages()
    {
        var a = await SeedUserAsync("msg-ns-a@example.com");
        var b = await SeedUserAsync("msg-ns-b@example.com");
        try
        {
            await SeedConnectionAsync(a.Id, b.Id);

            // a → b mesajı
            using var clientA = CreateAuthClient(a.Id, a.Email!);
            var tokenA = await GetCsrfAsync(clientA);
            clientA.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenA);
            var firstSend = await clientA.PostAsJsonAsync("/api/messages/send",
                new { recipientId = b.Id, body = "a→b ilk" });
            var firstData = await firstSend.Content.ReadFromJsonAsync<SendResp>();

            var since = DateTime.UtcNow.AddSeconds(-30).ToString("o");

            // b → a mesajı
            using var clientB = CreateAuthClient(b.Id, b.Email!);
            var tokenB = await GetCsrfAsync(clientB);
            clientB.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenB);
            await clientB.PostAsJsonAsync("/api/messages/send",
                new { recipientId = a.Id, body = "b→a yanıt" });

            // a'nın bakış açısından polling: yalnızca b'nin (other sender)
            // gönderdiği yeni mesaj görünmeli, kendi 'a→b ilk' dönmemeli
            var resp = await clientA.GetAsync(
                $"/api/messages/{firstData!.conversationId}/new?since={Uri.EscapeDataString(since)}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var json = await resp.Content.ReadAsStringAsync();
            json.Should().Contain("b→a yanıt");
            json.Should().NotContain("a→b ilk", "kendi mesajım polling'de DOM zaten var");
        }
        finally
        {
            await CleanupUserAsync(a.Email!);
            await CleanupUserAsync(b.Email!);
        }
    }

    [Fact]
    public async Task MarkRead_UpdatesLastReadAt()
    {
        var a = await SeedUserAsync("msg-mr-a@example.com");
        var b = await SeedUserAsync("msg-mr-b@example.com");
        try
        {
            await SeedConnectionAsync(a.Id, b.Id);

            using var clientA = CreateAuthClient(a.Id, a.Email!);
            var tokenA = await GetCsrfAsync(clientA);
            clientA.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenA);
            var sendResp = await clientA.PostAsJsonAsync("/api/messages/send",
                new { recipientId = b.Id, body = "okumamış mı" });
            var sendData = await sendResp.Content.ReadFromJsonAsync<SendResp>();

            // b okur
            using var clientB = CreateAuthClient(b.Id, b.Email!);
            var tokenB = await GetCsrfAsync(clientB);
            clientB.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenB);

            var resp = await clientB.PostAsync(
                $"/api/messages/{sendData!.conversationId}/mark-read", null);
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var conv = await ctx.Conversations.FirstAsync(c => c.Id == sendData.conversationId);
            // b is one of User1/User2; lastReadAt should be set
            var lastReadAt = conv.User1Id == b.Id ? conv.User1LastReadAt : conv.User2LastReadAt;
            lastReadAt.Should().NotBeNull();
        }
        finally
        {
            await CleanupUserAsync(a.Email!);
            await CleanupUserAsync(b.Email!);
        }
    }

    private sealed class SendResp
    {
        public bool success { get; set; }
        public int messageId { get; set; }
        public int conversationId { get; set; }
        public string html { get; set; } = "";
    }
}
