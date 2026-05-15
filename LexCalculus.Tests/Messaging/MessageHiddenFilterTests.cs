using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Messaging;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Xunit;

namespace LexCalculus.Tests.Messaging;

/// <summary>
/// Faz 5.7 — IsModeratorHidden filter davranışı:
/// - Recipient için hidden mesaj liste'den filter (görünmez)
/// - Sender için hidden mesaj listede kalır (view'da placeholder)
/// - Polling (GetNewerThanAsync) hidden mesajları hiç dönmez (alıcı için)
/// </summary>
public class MessageHiddenFilterTests : SqlServerTestBase
{
    // Setup sonrası seed edilen kullanıcıların DB-generated Id'leri.
    private int _user1Id, _user2Id;

    private (MessageService msgSvc, ApplicationDbContext ctx) Setup()
    {
        var ctx = _db.Create();
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        var storage = new FakeMediaStorage();
        var convSvc = new ConversationService(ctx, blockSvc, storage, new NullActivityLogService());
        var msgSvc = new MessageService(ctx, convSvc, new CommentSanitizer(),
            new NullActivityLogService(), new NoOpMessagingNotifier());

        var u1 = MakeUser("a");
        var u2 = MakeUser("b");
        ctx.Users.AddRange(u1, u2);
        ctx.SaveChanges();
        _user1Id = u1.Id;
        _user2Id = u2.Id;

        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = _user1Id, TargetId = _user2Id,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        ctx.SaveChanges();
        return (msgSvc, ctx);
    }

    private static ApplicationUser MakeUser(string suffix) => new()
    {
        UserName = $"u{suffix}@x.com",
        NormalizedUserName = $"U{suffix.ToUpperInvariant()}@X.COM",
        Email = $"u{suffix}@x.com",
        NormalizedEmail = $"U{suffix.ToUpperInvariant()}@X.COM",
        FullName = $"User {suffix}",
        CreatedAt = DateTime.UtcNow,
        IsActive = true,
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    [Fact]
    public async Task GetByConversation_HiddenForRecipient_FiltersOut()
    {
        var (svc, ctx) = Setup();
        var send = await svc.SendAsync(senderId: _user1Id, recipientId: _user2Id, "Gizlenecek mesaj");
        send.Success.Should().BeTrue();

        // Admin gizledi
        var msg = ctx.Messages.First();
        msg.IsModeratorHidden = true;
        await ctx.SaveChangesAsync();

        // Recipient (2) listeden çekiyor: hidden mesaj filter
        var list = await svc.GetByConversationAsync(msg.ConversationId, viewerId: _user2Id, 0, 50);
        list.Should().BeEmpty("recipient hidden mesajı görmemeli");
    }

    [Fact]
    public async Task GetByConversation_HiddenForSender_StaysInList()
    {
        var (svc, ctx) = Setup();
        var send = await svc.SendAsync(senderId: _user1Id, recipientId: _user2Id, "Sahip görmeli");
        var msg = ctx.Messages.First();
        msg.IsModeratorHidden = true;
        await ctx.SaveChangesAsync();

        // Sender (1) listede mesajını görür (view placeholder render edecek)
        var list = await svc.GetByConversationAsync(msg.ConversationId, viewerId: _user1Id, 0, 50);
        list.Should().HaveCount(1);
        list[0].IsModeratorHidden.Should().BeTrue();
    }

    [Fact]
    public async Task GetByConversation_NotHidden_ReturnsBody()
    {
        var (svc, ctx) = Setup();
        await svc.SendAsync(_user1Id, _user2Id, "Normal mesaj");
        var msg = ctx.Messages.First();

        var list = await svc.GetByConversationAsync(msg.ConversationId, viewerId: _user2Id, 0, 50);
        list.Should().HaveCount(1);
        list[0].IsModeratorHidden.Should().BeFalse();
        list[0].Body.Should().Contain("Normal mesaj");
    }

    [Fact]
    public async Task GetNewerThan_HiddenForRecipient_FiltersOut()
    {
        var (svc, ctx) = Setup();
        var since = DateTime.UtcNow.AddSeconds(-1);
        await svc.SendAsync(_user1Id, _user2Id, "Yeni mesaj"); // since sonrası
        var msg = ctx.Messages.First();
        msg.IsModeratorHidden = true;
        await ctx.SaveChangesAsync();

        // Recipient (2) polling: hidden filter dışı
        var list = await svc.GetNewerThanAsync(msg.ConversationId, viewerId: _user2Id, since);
        list.Should().BeEmpty("polling hidden mesajları alıcıya iletmez");
    }

    private sealed class FakeMediaStorage : IMediaStorage
    {
        public Task<string> StoreAsync(Stream content, string subdirectory,
            string fileName, CancellationToken ct = default)
            => Task.FromResult($"{subdirectory}/{fileName}");
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) => Task.CompletedTask;
        public string GetPublicUrl(string relativePath) => "/" + relativePath;
    }
}
