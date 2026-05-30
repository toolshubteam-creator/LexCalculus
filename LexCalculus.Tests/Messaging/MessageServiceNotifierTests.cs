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
public class MessageServiceNotifierTests : SqlServerTestBase
{
    // Setup sonrası seed edilen kullanıcıların DB-generated Id'leri.
    private int _u1, _u2, _u3;

    private (MessageService svc, ApplicationDbContext ctx, RecordingNotifier notifier) Setup(
        IMessagingNotifier? overrideNotifier = null)
    {
        var ctx = _db.Create();
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        var storage = new FakeMediaStorage();
        var convSvc = new ConversationService(ctx, blockSvc, storage, new NullActivityLogService());
        var notifier = overrideNotifier as RecordingNotifier ?? new RecordingNotifier();
        var msgSvc = new MessageService(ctx, convSvc, new CommentSanitizer(),
            new NullActivityLogService(), overrideNotifier ?? notifier);

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
        return (msgSvc, ctx, notifier);
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
    public async Task SendAsync_Valid_CallsNotifyMessageReceivedOnce()
    {
        var (svc, _, notifier) = Setup();

        var result = await svc.SendAsync(senderId: _u1, recipientId: _u2, "selam");

        result.Success.Should().BeTrue();
        notifier.ReceivedCalls.Should().HaveCount(1);
        notifier.ReceivedCalls[0].RecipientId.Should().Be(_u2);
        notifier.ReceivedCalls[0].MessageId.Should().Be(result.Message!.Id);
    }

    [Fact]
    public async Task SendAsync_NoPermission_DoesNotCallNotifier()
    {
        // u1 ↔ u3 arasında ne bağlantı ne tenant — yetki yok
        var (svc, _, notifier) = Setup();

        var result = await svc.SendAsync(senderId: _u1, recipientId: _u3, "yetkisiz");

        result.Success.Should().BeFalse();
        notifier.ReceivedCalls.Should().BeEmpty();
        notifier.DeletedCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_Owner_CallsNotifyMessageDeletedOnce()
    {
        var (svc, _, notifier) = Setup();
        var send = await svc.SendAsync(_u1, _u2, "silinecek");

        notifier.Reset();
        var del = await svc.DeleteAsync(send.Message!.Id, actingUserId: _u1);

        del.Success.Should().BeTrue();
        notifier.DeletedCalls.Should().HaveCount(1);
        notifier.DeletedCalls[0].RecipientId.Should().Be(_u2);
        notifier.DeletedCalls[0].ConversationId.Should().Be(send.Message.ConversationId);
        notifier.DeletedCalls[0].MessageId.Should().Be(send.Message.Id);
    }

    [Fact]
    public async Task DeleteAsync_NonOwner_DoesNotCallNotifier()
    {
        var (svc, _, notifier) = Setup();
        var send = await svc.SendAsync(_u1, _u2, "kendi mesajım");

        notifier.Reset();
        var del = await svc.DeleteAsync(send.Message!.Id, actingUserId: _u2);

        del.Success.Should().BeFalse();
        notifier.DeletedCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_NotifierThrows_DoesNotBreakSend()
    {
        var throwing = new ThrowingNotifier();
        var (svc, ctx, _) = Setup(throwing);

        var result = await svc.SendAsync(_u1, _u2, "fırlatan notifier");

        // Notifier hata fırlatsa bile mesaj DB'ye yazılmış olmalı, sonuç success
        result.Success.Should().BeTrue();
        result.Message.Should().NotBeNull();
        ctx.Messages.Should().HaveCount(1);
    }

    private sealed class RecordingNotifier : IMessagingNotifier
    {
        public List<(int RecipientId, int MessageId)> ReceivedCalls { get; } = new();
        public List<(int RecipientId, int ConversationId, int MessageId)> DeletedCalls { get; } = new();
        public List<(int SenderId, int RecipientId, int ConversationId, int MessageId)> HiddenCalls { get; } = new();

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

        public Task NotifyMessageHiddenAsync(int senderId, int recipientId, int conversationId, int messageId, CancellationToken ct = default)
        {
            HiddenCalls.Add((senderId, recipientId, conversationId, messageId));
            return Task.CompletedTask;
        }

        public List<(int UserId, int ConversationId)> ReadCalls { get; } = new();

        public Task NotifyConversationReadAsync(int userId, int conversationId, CancellationToken ct = default)
        {
            ReadCalls.Add((userId, conversationId));
            return Task.CompletedTask;
        }

        public void Reset()
        {
            ReceivedCalls.Clear();
            DeletedCalls.Clear();
            HiddenCalls.Clear();
            ReadCalls.Clear();
        }
    }

    private sealed class ThrowingNotifier : IMessagingNotifier
    {
        public Task NotifyMessageReceivedAsync(int recipientId, int messageId, CancellationToken ct = default)
            => throw new InvalidOperationException("Hub down");

        public Task NotifyMessageDeletedAsync(int recipientId, int conversationId, int messageId, CancellationToken ct = default)
            => throw new InvalidOperationException("Hub down");

        public Task NotifyMessageHiddenAsync(int senderId, int recipientId, int conversationId, int messageId, CancellationToken ct = default)
            => throw new InvalidOperationException("Hub down");

        public Task NotifyConversationReadAsync(int userId, int conversationId, CancellationToken ct = default)
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
