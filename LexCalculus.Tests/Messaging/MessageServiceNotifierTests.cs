using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Messaging;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Xunit;

namespace LexCalculus.Tests.Messaging;

/// <summary>
/// MessageService → IMessagingNotifier kontrat doğrulamaları (Faz 5.6).
/// Notifier mock üzerinden recipientId/messageId/conversationId argümanları
/// ve davranışlar (sessiz fail, izinsizde no-call) test edilir.
/// </summary>
public class MessageServiceNotifierTests
{
    private static (MessageService svc, ApplicationDbContext ctx, RecordingNotifier notifier) Setup(
        IMessagingNotifier? overrideNotifier = null)
    {
        var ctx = TestDbContextFactory.Create();
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        var storage = new FakeMediaStorage();
        var convSvc = new ConversationService(ctx, blockSvc, storage, new NullActivityLogService());
        var notifier = overrideNotifier as RecordingNotifier ?? new RecordingNotifier();
        var msgSvc = new MessageService(ctx, convSvc, new CommentSanitizer(),
            new NullActivityLogService(), overrideNotifier ?? notifier);

        ctx.Users.AddRange(MakeUser(1), MakeUser(2), MakeUser(3));
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = 1, TargetId = 2,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        ctx.SaveChanges();
        return (msgSvc, ctx, notifier);
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
    public async Task SendAsync_Valid_CallsNotifyMessageReceivedOnce()
    {
        var (svc, _, notifier) = Setup();

        var result = await svc.SendAsync(senderId: 1, recipientId: 2, "selam");

        result.Success.Should().BeTrue();
        notifier.ReceivedCalls.Should().HaveCount(1);
        notifier.ReceivedCalls[0].RecipientId.Should().Be(2);
        notifier.ReceivedCalls[0].MessageId.Should().Be(result.Message!.Id);
    }

    [Fact]
    public async Task SendAsync_NoPermission_DoesNotCallNotifier()
    {
        // 1 ↔ 3 arasında ne bağlantı ne tenant — yetki yok
        var (svc, _, notifier) = Setup();

        var result = await svc.SendAsync(senderId: 1, recipientId: 3, "yetkisiz");

        result.Success.Should().BeFalse();
        notifier.ReceivedCalls.Should().BeEmpty();
        notifier.DeletedCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_Owner_CallsNotifyMessageDeletedOnce()
    {
        var (svc, _, notifier) = Setup();
        var send = await svc.SendAsync(1, 2, "silinecek");

        notifier.Reset();
        var del = await svc.DeleteAsync(send.Message!.Id, actingUserId: 1);

        del.Success.Should().BeTrue();
        notifier.DeletedCalls.Should().HaveCount(1);
        notifier.DeletedCalls[0].RecipientId.Should().Be(2);
        notifier.DeletedCalls[0].ConversationId.Should().Be(send.Message.ConversationId);
        notifier.DeletedCalls[0].MessageId.Should().Be(send.Message.Id);
    }

    [Fact]
    public async Task DeleteAsync_NonOwner_DoesNotCallNotifier()
    {
        var (svc, _, notifier) = Setup();
        var send = await svc.SendAsync(1, 2, "kendi mesajım");

        notifier.Reset();
        var del = await svc.DeleteAsync(send.Message!.Id, actingUserId: 2);

        del.Success.Should().BeFalse();
        notifier.DeletedCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_NotifierThrows_DoesNotBreakSend()
    {
        var throwing = new ThrowingNotifier();
        var (svc, ctx, _) = Setup(throwing);

        var result = await svc.SendAsync(1, 2, "fırlatan notifier");

        // Notifier hata fırlatsa bile mesaj DB'ye yazılmış olmalı, sonuç success
        result.Success.Should().BeTrue();
        result.Message.Should().NotBeNull();
        ctx.Messages.Should().HaveCount(1);
    }

    private sealed class RecordingNotifier : IMessagingNotifier
    {
        public List<(int RecipientId, int MessageId)> ReceivedCalls { get; } = new();
        public List<(int RecipientId, int ConversationId, int MessageId)> DeletedCalls { get; } = new();

        public Task NotifyMessageReceivedAsync(int recipientId, int messageId, CancellationToken ct = default)
        {
            ReceivedCalls.Add((recipientId, messageId));
            return Task.CompletedTask;
        }

        public Task NotifyMessageDeletedAsync(int recipientId, int conversationId, int messageId, CancellationToken ct = default)
        {
            DeletedCalls.Add((recipientId, conversationId, messageId));
            return Task.CompletedTask;
        }

        public void Reset()
        {
            ReceivedCalls.Clear();
            DeletedCalls.Clear();
        }
    }

    private sealed class ThrowingNotifier : IMessagingNotifier
    {
        public Task NotifyMessageReceivedAsync(int recipientId, int messageId, CancellationToken ct = default)
            => throw new InvalidOperationException("Hub down");

        public Task NotifyMessageDeletedAsync(int recipientId, int conversationId, int messageId, CancellationToken ct = default)
            => throw new InvalidOperationException("Hub down");
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
