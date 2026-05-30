using FluentAssertions;
using LexCalculus.Core.Entities.Email;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Messaging;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Messaging;

public class MessageServiceTests : SqlServerTestBase
{
    // Setup sonrası seed edilen kullanıcıların DB-generated Id'leri.
    private int _u1, _u2, _u3;

    private (MessageService msgSvc, ConversationService convSvc, ApplicationDbContext ctx) Setup()
    {
        var ctx = _db.Create();
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        var storage = new FakeMediaStorage();
        var convSvc = new ConversationService(ctx, blockSvc, storage, new NullActivityLogService());
        var msgSvc = new MessageService(ctx, convSvc, new CommentSanitizer(),
            new NullActivityLogService(), new NoOpMessagingNotifier());

        var u1 = MakeUser("a");
        var u2 = MakeUser("b");
        var u3 = MakeUser("c");
        ctx.Users.AddRange(u1, u2, u3);
        ctx.SaveChanges();
        _u1 = u1.Id;
        _u2 = u2.Id;
        _u3 = u3.Id;

        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = _u1, TargetId = _u2,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        ctx.SaveChanges();
        return (msgSvc, convSvc, ctx);
    }

    private static ApplicationUser MakeUser(string suffix) => new()
    {
        UserName = $"u{suffix}@x.com", NormalizedUserName = $"U{suffix.ToUpperInvariant()}@X.COM",
        Email = $"u{suffix}@x.com", NormalizedEmail = $"U{suffix.ToUpperInvariant()}@X.COM",
        FullName = $"User {suffix}", CreatedAt = DateTime.UtcNow,
        IsActive = true, EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    [Fact]
    public async Task SendAsync_Valid_PersistsAndUpdatesLastMessageAt()
    {
        var (msgSvc, _, ctx) = Setup();

        var beforeNow = DateTime.UtcNow;
        var result = await msgSvc.SendAsync(_u1, _u2, "Merhaba dünya");

        result.Success.Should().BeTrue();
        result.Message.Should().NotBeNull();

        var conv = await ctx.Conversations.FirstAsync();
        conv.LastMessageAt.Should().BeAfter(beforeNow.AddSeconds(-1));
        (await ctx.Messages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_BodySanitized_StripsScriptTag()
    {
        var (msgSvc, _, _) = Setup();

        var result = await msgSvc.SendAsync(_u1, _u2, "<script>alert(1)</script>Hello");

        result.Success.Should().BeTrue();
        result.Message!.Body.Should().NotContain("<script>");
        result.Message.Body.Should().Contain("Hello");
    }

    [Fact]
    public async Task SendAsync_BodyAutoLink_ConvertsUrl()
    {
        var (msgSvc, _, _) = Setup();

        var result = await msgSvc.SendAsync(_u1, _u2, "Bak: https://example.com adresine git");

        result.Success.Should().BeTrue();
        result.Message!.Body.Should().Contain("<a");
        result.Message.Body.Should().Contain("https://example.com");
        result.Message.Body.Should().Contain("nofollow");
    }

    [Fact]
    public async Task SendAsync_EmptyBody_ReturnsError()
    {
        var (msgSvc, _, _) = Setup();
        var result = await msgSvc.SendAsync(_u1, _u2, "   ");
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("boş");
    }

    [Fact]
    public async Task SendAsync_TooLongBody_ReturnsError()
    {
        var (msgSvc, _, _) = Setup();
        var huge = new string('a', 1001);
        var result = await msgSvc.SendAsync(_u1, _u2, huge);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("aşamaz");
    }

    [Fact]
    public async Task SendAsync_NoPermission_ReturnsError()
    {
        var (msgSvc, _, _) = Setup();
        // u1↔u3 bağlantı yok → mesaj atılamaz
        var result = await msgSvc.SendAsync(_u1, _u3, "Merhaba");
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("gönderilemiyor");
    }

    [Fact]
    public async Task DeleteAsync_NonOwner_ReturnsError()
    {
        var (msgSvc, _, _) = Setup();
        var sent = await msgSvc.SendAsync(_u1, _u2, "Mesaj");

        var result = await msgSvc.DeleteAsync(sent.Message!.Id, actingUserId: _u2);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("kendi");
    }

    [Fact]
    public async Task DeleteAsync_Owner_SetsIsDeletedTrueBodyPreserved()
    {
        var (msgSvc, _, ctx) = Setup();
        var sent = await msgSvc.SendAsync(_u1, _u2, "Mesaj");
        var bodyBefore = sent.Message!.Body;

        var result = await msgSvc.DeleteAsync(sent.Message.Id, actingUserId: _u1);

        result.Success.Should().BeTrue();
        var msg = await ctx.Messages.FirstAsync(m => m.Id == sent.Message.Id);
        msg.IsDeleted.Should().BeTrue();
        msg.Body.Should().Be(bodyBefore); // Body korunur
    }

    [Fact]
    public async Task GetByConversationAsync_NonParticipant_ReturnsEmpty()
    {
        var (msgSvc, _, _) = Setup();
        var sent = await msgSvc.SendAsync(_u1, _u2, "Mesaj");

        var list = await msgSvc.GetByConversationAsync(
            sent.Message!.ConversationId, viewerId: _u3);

        list.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByConversationAsync_OrdersByCreatedAtDesc()
    {
        var (msgSvc, _, _) = Setup();
        var first = await msgSvc.SendAsync(_u1, _u2, "İlk");
        await Task.Delay(20);
        var second = await msgSvc.SendAsync(_u2, _u1, "İkinci");

        var list = await msgSvc.GetByConversationAsync(
            first.Message!.ConversationId, viewerId: _u1);

        list.Should().HaveCount(2);
        list[0].Id.Should().Be(second.Message!.Id);
        list[1].Id.Should().Be(first.Message.Id);
    }

    [Fact]
    public async Task GetByConversationAsync_WithSkipTake_PaginatesCorrectly()
    {
        var (msgSvc, _, _) = Setup();
        var first = await msgSvc.SendAsync(_u1, _u2, "M1");
        await msgSvc.SendAsync(_u1, _u2, "M2");
        await msgSvc.SendAsync(_u1, _u2, "M3");
        await msgSvc.SendAsync(_u1, _u2, "M4");

        var page1 = await msgSvc.GetByConversationAsync(
            first.Message!.ConversationId, viewerId: _u1, skip: 0, take: 2);
        var page2 = await msgSvc.GetByConversationAsync(
            first.Message.ConversationId, viewerId: _u1, skip: 2, take: 2);

        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page1.Select(m => m.Id).Should().NotIntersectWith(page2.Select(m => m.Id));
    }

    [Fact]
    public async Task SendAsync_RecipientDigestPrefOn_CreatesDigestEntry()
    {
        var (msgSvc, _, ctx) = Setup();
        // Alıcı (u2) için profil + dijest tercihi açık (master default true)
        ctx.UserProfiles.Add(new UserProfile { UserId = _u2, DisplayName = "User b", EmailOnMessageDigest = true });
        await ctx.SaveChangesAsync();

        await msgSvc.SendAsync(_u1, _u2, "Selam");

        var entries = await ctx.EmailDigestEntries.Where(e => e.UserId == _u2).ToListAsync();
        entries.Should().HaveCount(1);
        entries[0].Type.Should().Be(EmailDigestType.Message);
        entries[0].IsSent.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_RecipientDigestPrefOff_DoesNotCreateEntry()
    {
        var (msgSvc, _, ctx) = Setup();
        ctx.UserProfiles.Add(new UserProfile { UserId = _u2, DisplayName = "User b", EmailOnMessageDigest = false });
        await ctx.SaveChangesAsync();

        await msgSvc.SendAsync(_u1, _u2, "Selam");

        (await ctx.EmailDigestEntries.CountAsync()).Should().Be(0);
    }

    private sealed class FakeMediaStorage : IMediaStorage
    {
        public Task<string> StoreAsync(Stream content, string subdirectory,
            string fileName, CancellationToken ct = default) => Task.FromResult($"{subdirectory}/{fileName}");
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) => Task.CompletedTask;
        public string GetPublicUrl(string relativePath) => "/" + relativePath;
    }
}
