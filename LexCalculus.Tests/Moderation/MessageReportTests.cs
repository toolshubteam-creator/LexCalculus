using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Entities.Moderation;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Messaging;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Moderation;

/// <summary>
/// Faz 5.7 — mesaj şikayet (ContentReportTargetType.Message) servis akışı.
/// Yetki kontrolü (conversation participant), self-report engel,
/// Hide IsModeratorHidden toggle, real-time broadcast notifier kontratı.
/// </summary>
public class MessageReportTests
{
    private static (ContentReportService svc, ApplicationDbContext ctx,
                    RecordingNotificationService notif, RecordingMessagingNotifier msgNotif)
        Setup()
    {
        var ctx = TestDbContextFactory.Create();
        var notif = new RecordingNotificationService();
        var msgNotif = new RecordingMessagingNotifier();
        var svc = new ContentReportService(ctx, notif, new NullActivityLogService(), msgNotif);

        ctx.Users.AddRange(
            MakeUser(1, "sender@x.com"),
            MakeUser(2, "recipient@x.com"),
            MakeUser(3, "stranger@x.com"),
            MakeUser(4, "admin@x.com"));
        ctx.SaveChanges();
        return (svc, ctx, notif, msgNotif);
    }

    private static ApplicationUser MakeUser(int id, string email) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        FullName = $"User {id}",
        CreatedAt = DateTime.UtcNow,
        IsActive = true,
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static async Task<(int convId, int messageId)> SeedConversationAsync(
        ApplicationDbContext ctx, int senderId, int recipientId)
    {
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
            Body = "<p>Test mesaj body</p>",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
            IsModeratorHidden = false
        };
        ctx.Messages.Add(msg);
        await ctx.SaveChangesAsync();
        return (conv.Id, msg.Id);
    }

    [Fact]
    public async Task CreateAsync_MessageTarget_PersistsReport()
    {
        var (svc, ctx, _, _) = Setup();
        var (_, msgId) = await SeedConversationAsync(ctx, senderId: 1, recipientId: 2);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Message, msgId, reporterId: 2,
            ContentReportReason.Harassment, "Taciz edici mesaj");

        result.Success.Should().BeTrue();
        var saved = await ctx.ContentReports
            .FirstOrDefaultAsync(r => r.TargetType == ContentReportTargetType.Message
                                   && r.TargetId == msgId);
        saved.Should().NotBeNull();
        saved!.ReporterId.Should().Be(2);
        saved.Reason.Should().Be(ContentReportReason.Harassment);
    }

    [Fact]
    public async Task CreateAsync_OwnMessage_ReturnsSelfReportError()
    {
        var (svc, ctx, _, _) = Setup();
        var (_, msgId) = await SeedConversationAsync(ctx, senderId: 1, recipientId: 2);

        // sender (1) kendi mesajını şikayet etmeye çalışır
        var result = await svc.CreateAsync(
            ContentReportTargetType.Message, msgId, reporterId: 1,
            ContentReportReason.Spam, null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kendi");
    }

    [Fact]
    public async Task CreateAsync_NonParticipant_ReturnsError()
    {
        var (svc, ctx, _, _) = Setup();
        var (_, msgId) = await SeedConversationAsync(ctx, senderId: 1, recipientId: 2);

        // 3. kişi (stranger) konuşmada değil
        var result = await svc.CreateAsync(
            ContentReportTargetType.Message, msgId, reporterId: 3,
            ContentReportReason.Spam, null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Yetkisiz");
    }

    [Fact]
    public async Task CreateAsync_DuplicateReport_ReturnsError()
    {
        var (svc, ctx, _, _) = Setup();
        var (_, msgId) = await SeedConversationAsync(ctx, senderId: 1, recipientId: 2);

        var first = await svc.CreateAsync(
            ContentReportTargetType.Message, msgId, reporterId: 2,
            ContentReportReason.Spam, null);
        first.Success.Should().BeTrue();

        var second = await svc.CreateAsync(
            ContentReportTargetType.Message, msgId, reporterId: 2,
            ContentReportReason.Harassment, "tekrar");
        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("zaten");
    }

    [Fact]
    public async Task HideAsync_Message_SetsIsModeratorHidden()
    {
        var (svc, ctx, _, _) = Setup();
        var (_, msgId) = await SeedConversationAsync(ctx, senderId: 1, recipientId: 2);
        await svc.CreateAsync(ContentReportTargetType.Message, msgId, reporterId: 2,
            ContentReportReason.Harassment, "şikayet");

        var result = await svc.HideAsync(
            ContentReportTargetType.Message, msgId, adminUserId: 4, "ihlal");

        result.Success.Should().BeTrue();
        var msg = await ctx.Messages.FirstAsync(m => m.Id == msgId);
        msg.IsModeratorHidden.Should().BeTrue();
        msg.Body.Should().Contain("Test mesaj body", "Hide pattern: body korunur");
    }

    [Fact]
    public async Task HideAsync_Message_NotifiesParticipants()
    {
        var (svc, ctx, _, msgNotif) = Setup();
        var (convId, msgId) = await SeedConversationAsync(ctx, senderId: 1, recipientId: 2);
        await svc.CreateAsync(ContentReportTargetType.Message, msgId, reporterId: 2,
            ContentReportReason.Harassment, "şikayet");

        await svc.HideAsync(ContentReportTargetType.Message, msgId, adminUserId: 4, null);

        msgNotif.HiddenCalls.Should().HaveCount(1);
        var call = msgNotif.HiddenCalls[0];
        call.SenderId.Should().Be(1);
        call.RecipientId.Should().Be(2);
        call.ConversationId.Should().Be(convId);
        call.MessageId.Should().Be(msgId);
    }

    [Fact]
    public async Task UnhideAsync_Message_ResetsIsModeratorHidden()
    {
        var (svc, ctx, _, _) = Setup();
        var (_, msgId) = await SeedConversationAsync(ctx, senderId: 1, recipientId: 2);
        await svc.CreateAsync(ContentReportTargetType.Message, msgId, reporterId: 2,
            ContentReportReason.Harassment, "şikayet");
        await svc.HideAsync(ContentReportTargetType.Message, msgId, adminUserId: 4, null);

        var result = await svc.UnhideAsync(ContentReportTargetType.Message, msgId, adminUserId: 4);

        result.Success.Should().BeTrue();
        var msg = await ctx.Messages.FirstAsync(m => m.Id == msgId);
        msg.IsModeratorHidden.Should().BeFalse();
    }

    [Fact]
    public async Task GetHiddenContent_IncludesHiddenMessages()
    {
        var (svc, ctx, _, _) = Setup();
        var (_, msgId) = await SeedConversationAsync(ctx, senderId: 1, recipientId: 2);
        await svc.CreateAsync(ContentReportTargetType.Message, msgId, reporterId: 2,
            ContentReportReason.Spam, null);
        await svc.HideAsync(ContentReportTargetType.Message, msgId, adminUserId: 4, null);

        var hidden = await svc.GetHiddenContentAsync();

        hidden.Should().Contain(h => h.TargetType == ContentReportTargetType.Message
                                  && h.TargetId == msgId);
        var item = hidden.First(h => h.TargetType == ContentReportTargetType.Message);
        item.TargetTitle.Should().StartWith("Mesaj:");
        item.TargetUrl.Should().BeNull("mesaj public URL'i yok");
    }

    [Fact]
    public async Task ActionAsync_Message_DeletesMessage()
    {
        var (svc, ctx, _, _) = Setup();
        var (_, msgId) = await SeedConversationAsync(ctx, senderId: 1, recipientId: 2);
        await svc.CreateAsync(ContentReportTargetType.Message, msgId, reporterId: 2,
            ContentReportReason.Legal, "telif ihlali");

        var result = await svc.ActionAsync(
            ContentReportTargetType.Message, msgId, adminUserId: 4, "kaldırıldı");

        result.Success.Should().BeTrue();
        (await ctx.Messages.AnyAsync(m => m.Id == msgId)).Should().BeFalse();
    }

    private sealed class RecordingMessagingNotifier : IMessagingNotifier
    {
        public List<(int SenderId, int RecipientId, int ConversationId, int MessageId)> HiddenCalls { get; } = new();

        public Task NotifyMessageReceivedAsync(int recipientId, int messageId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task NotifyMessageDeletedAsync(int recipientId, int conversationId, int messageId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task NotifyMessageHiddenAsync(int senderId, int recipientId, int conversationId, int messageId, CancellationToken ct = default)
        {
            HiddenCalls.Add((senderId, recipientId, conversationId, messageId));
            return Task.CompletedTask;
        }
    }
}
