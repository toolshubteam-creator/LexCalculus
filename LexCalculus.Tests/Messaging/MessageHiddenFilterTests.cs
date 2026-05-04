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
public class MessageHiddenFilterTests
{
    private static (MessageService msgSvc, ApplicationDbContext ctx) Setup()
    {
        var ctx = TestDbContextFactory.Create();
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        var storage = new FakeMediaStorage();
        var convSvc = new ConversationService(ctx, blockSvc, storage, new NullActivityLogService());
        var msgSvc = new MessageService(ctx, convSvc, new CommentSanitizer(),
            new NullActivityLogService(), new NoOpMessagingNotifier());

        ctx.Users.AddRange(MakeUser(1), MakeUser(2));
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = 1, TargetId = 2,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        ctx.SaveChanges();
        return (msgSvc, ctx);
    }

    private static ApplicationUser MakeUser(int id) => new()
    {
        Id = id,
        UserName = $"u{id}@x.com",
        NormalizedUserName = $"U{id}@X.COM",
        Email = $"u{id}@x.com",
        NormalizedEmail = $"U{id}@X.COM",
        FullName = $"User {id}",
        CreatedAt = DateTime.UtcNow,
        IsActive = true,
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    [Fact]
    public async Task GetByConversation_HiddenForRecipient_FiltersOut()
    {
        var (svc, ctx) = Setup();
        var send = await svc.SendAsync(senderId: 1, recipientId: 2, "Gizlenecek mesaj");
        send.Success.Should().BeTrue();

        // Admin gizledi
        var msg = ctx.Messages.First();
        msg.IsModeratorHidden = true;
        await ctx.SaveChangesAsync();

        // Recipient (2) listeden çekiyor: hidden mesaj filter
        var list = await svc.GetByConversationAsync(msg.ConversationId, viewerId: 2, 0, 50);
        list.Should().BeEmpty("recipient hidden mesajı görmemeli");
    }

    [Fact]
    public async Task GetByConversation_HiddenForSender_StaysInList()
    {
        var (svc, ctx) = Setup();
        var send = await svc.SendAsync(senderId: 1, recipientId: 2, "Sahip görmeli");
        var msg = ctx.Messages.First();
        msg.IsModeratorHidden = true;
        await ctx.SaveChangesAsync();

        // Sender (1) listede mesajını görür (view placeholder render edecek)
        var list = await svc.GetByConversationAsync(msg.ConversationId, viewerId: 1, 0, 50);
        list.Should().HaveCount(1);
        list[0].IsModeratorHidden.Should().BeTrue();
    }

    [Fact]
    public async Task GetByConversation_NotHidden_ReturnsBody()
    {
        var (svc, ctx) = Setup();
        await svc.SendAsync(1, 2, "Normal mesaj");
        var msg = ctx.Messages.First();

        var list = await svc.GetByConversationAsync(msg.ConversationId, viewerId: 2, 0, 50);
        list.Should().HaveCount(1);
        list[0].IsModeratorHidden.Should().BeFalse();
        list[0].Body.Should().Contain("Normal mesaj");
    }

    [Fact]
    public async Task GetNewerThan_HiddenForRecipient_FiltersOut()
    {
        var (svc, ctx) = Setup();
        var since = DateTime.UtcNow.AddSeconds(-1);
        await svc.SendAsync(1, 2, "Yeni mesaj"); // since sonrası
        var msg = ctx.Messages.First();
        msg.IsModeratorHidden = true;
        await ctx.SaveChangesAsync();

        // Recipient (2) polling: hidden filter dışı
        var list = await svc.GetNewerThanAsync(msg.ConversationId, viewerId: 2, since);
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
