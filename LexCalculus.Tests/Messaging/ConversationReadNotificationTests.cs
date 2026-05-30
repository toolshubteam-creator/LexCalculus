using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Messaging;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LexCalculus.Tests.Messaging;

/// <summary>
/// Faz 6.7 (#37) — ConversationService.MarkAsReadAsync, multi-tab read-state için
/// IMessagingNotifier.NotifyConversationReadAsync'i tetikler (best-effort).
/// </summary>
public class ConversationReadNotificationTests : SqlServerTestBase
{
    private static ApplicationUser MakeUser(string s) => new()
    {
        UserName = $"{s}@x.com", NormalizedUserName = $"{s.ToUpperInvariant()}@X.COM",
        Email = $"{s}@x.com", NormalizedEmail = $"{s.ToUpperInvariant()}@X.COM",
        FullName = $"User {s}", CreatedAt = DateTime.UtcNow,
        IsActive = true, EmailConfirmed = true, SecurityStamp = Guid.NewGuid().ToString()
    };

    private (ConversationService svc, ApplicationDbContext ctx, Mock<IMessagingNotifier> notifier, int u1, int convId)
        Setup(bool notifierThrows = false)
    {
        var ctx = _db.Create();
        var notifier = new Mock<IMessagingNotifier>();
        var setup = notifier.Setup(n =>
            n.NotifyConversationReadAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()));
        if (notifierThrows) setup.ThrowsAsync(new InvalidOperationException("Hub down"));
        else setup.Returns(Task.CompletedTask);

        var svc = new ConversationService(
            ctx, new UserBlockService(ctx, new NullActivityLogService()),
            new FakeMediaStorage(), new NullActivityLogService(),
            logger: null, notifier: notifier.Object);

        var ua = MakeUser("a");
        var ub = MakeUser("b");
        ctx.Users.AddRange(ua, ub);
        ctx.SaveChanges();

        var u1 = Math.Min(ua.Id, ub.Id);
        var u2 = Math.Max(ua.Id, ub.Id);
        var conv = new Conversation { User1Id = u1, User2Id = u2, CreatedAt = DateTime.UtcNow };
        ctx.Conversations.Add(conv);
        ctx.SaveChanges();

        return (svc, ctx, notifier, u1, conv.Id);
    }

    [Fact]
    public async Task MarkAsReadAsync_NotifiesConversationReadOnce()
    {
        var (svc, _, notifier, u1, convId) = Setup();

        var result = await svc.MarkAsReadAsync(convId, u1);

        result.Success.Should().BeTrue();
        notifier.Verify(n => n.NotifyConversationReadAsync(u1, convId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkAsReadAsync_NotifierThrows_StillCommits()
    {
        var (svc, ctx, _, u1, convId) = Setup(notifierThrows: true);

        var result = await svc.MarkAsReadAsync(convId, u1);

        result.Success.Should().BeTrue();   // notifier exception MarkAsRead'i bozmaz
        var conv = await ctx.Conversations.AsNoTracking().FirstAsync(c => c.Id == convId);
        conv.User1LastReadAt.Should().NotBeNull();
    }

    private sealed class FakeMediaStorage : IMediaStorage
    {
        public Task<string> StoreAsync(Stream content, string subdirectory, string fileName, CancellationToken ct = default)
            => Task.FromResult($"{subdirectory}/{fileName}");
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) => Task.CompletedTask;
        public string GetPublicUrl(string relativePath) => "/" + relativePath;
    }
}
