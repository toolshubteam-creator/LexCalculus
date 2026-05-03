using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Messaging;

public class MessageServiceTests
{
    private static (MessageService msgSvc, ConversationService convSvc, ApplicationDbContext ctx) Setup()
    {
        var ctx = TestDbContextFactory.Create();
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        var storage = new FakeMediaStorage();
        var convSvc = new ConversationService(ctx, blockSvc, storage, new NullActivityLogService());
        var msgSvc = new MessageService(ctx, convSvc, new CommentSanitizer(), new NullActivityLogService());

        ctx.Users.AddRange(MakeUser(1), MakeUser(2), MakeUser(3));
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = 1, TargetId = 2,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        ctx.SaveChanges();
        return (msgSvc, convSvc, ctx);
    }

    private static ApplicationUser MakeUser(int id) => new()
    {
        Id = id,
        UserName = $"u{id}@x.com", NormalizedUserName = $"U{id}@X.COM",
        Email = $"u{id}@x.com", NormalizedEmail = $"U{id}@X.COM",
        FullName = $"User {id}", CreatedAt = DateTime.UtcNow,
        IsActive = true, EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    [Fact]
    public async Task SendAsync_Valid_PersistsAndUpdatesLastMessageAt()
    {
        var (msgSvc, _, ctx) = Setup();

        var beforeNow = DateTime.UtcNow;
        var result = await msgSvc.SendAsync(1, 2, "Merhaba dünya");

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

        var result = await msgSvc.SendAsync(1, 2, "<script>alert(1)</script>Hello");

        result.Success.Should().BeTrue();
        result.Message!.Body.Should().NotContain("<script>");
        result.Message.Body.Should().Contain("Hello");
    }

    [Fact]
    public async Task SendAsync_BodyAutoLink_ConvertsUrl()
    {
        var (msgSvc, _, _) = Setup();

        var result = await msgSvc.SendAsync(1, 2, "Bak: https://example.com adresine git");

        result.Success.Should().BeTrue();
        result.Message!.Body.Should().Contain("<a");
        result.Message.Body.Should().Contain("https://example.com");
        result.Message.Body.Should().Contain("nofollow");
    }

    [Fact]
    public async Task SendAsync_EmptyBody_ReturnsError()
    {
        var (msgSvc, _, _) = Setup();
        var result = await msgSvc.SendAsync(1, 2, "   ");
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("boş");
    }

    [Fact]
    public async Task SendAsync_TooLongBody_ReturnsError()
    {
        var (msgSvc, _, _) = Setup();
        var huge = new string('a', 1001);
        var result = await msgSvc.SendAsync(1, 2, huge);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("aşamaz");
    }

    [Fact]
    public async Task SendAsync_NoPermission_ReturnsError()
    {
        var (msgSvc, _, _) = Setup();
        // 1↔3 bağlantı yok → mesaj atılamaz
        var result = await msgSvc.SendAsync(1, 3, "Merhaba");
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("gönderilemiyor");
    }

    [Fact]
    public async Task DeleteAsync_NonOwner_ReturnsError()
    {
        var (msgSvc, _, _) = Setup();
        var sent = await msgSvc.SendAsync(1, 2, "Mesaj");

        var result = await msgSvc.DeleteAsync(sent.Message!.Id, actingUserId: 2);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("kendi");
    }

    [Fact]
    public async Task DeleteAsync_Owner_SetsIsDeletedTrueBodyPreserved()
    {
        var (msgSvc, _, ctx) = Setup();
        var sent = await msgSvc.SendAsync(1, 2, "Mesaj");
        var bodyBefore = sent.Message!.Body;

        var result = await msgSvc.DeleteAsync(sent.Message.Id, actingUserId: 1);

        result.Success.Should().BeTrue();
        var msg = await ctx.Messages.FirstAsync(m => m.Id == sent.Message.Id);
        msg.IsDeleted.Should().BeTrue();
        msg.Body.Should().Be(bodyBefore); // Body korunur
    }

    [Fact]
    public async Task GetByConversationAsync_NonParticipant_ReturnsEmpty()
    {
        var (msgSvc, _, _) = Setup();
        var sent = await msgSvc.SendAsync(1, 2, "Mesaj");

        var list = await msgSvc.GetByConversationAsync(
            sent.Message!.ConversationId, viewerId: 3);

        list.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByConversationAsync_OrdersByCreatedAtDesc()
    {
        var (msgSvc, _, _) = Setup();
        var first = await msgSvc.SendAsync(1, 2, "İlk");
        await Task.Delay(20);
        var second = await msgSvc.SendAsync(2, 1, "İkinci");

        var list = await msgSvc.GetByConversationAsync(
            first.Message!.ConversationId, viewerId: 1);

        list.Should().HaveCount(2);
        list[0].Id.Should().Be(second.Message!.Id);
        list[1].Id.Should().Be(first.Message.Id);
    }

    [Fact]
    public async Task GetByConversationAsync_WithSkipTake_PaginatesCorrectly()
    {
        var (msgSvc, _, _) = Setup();
        var first = await msgSvc.SendAsync(1, 2, "M1");
        await msgSvc.SendAsync(1, 2, "M2");
        await msgSvc.SendAsync(1, 2, "M3");
        await msgSvc.SendAsync(1, 2, "M4");

        var page1 = await msgSvc.GetByConversationAsync(
            first.Message!.ConversationId, viewerId: 1, skip: 0, take: 2);
        var page2 = await msgSvc.GetByConversationAsync(
            first.Message.ConversationId, viewerId: 1, skip: 2, take: 2);

        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page1.Select(m => m.Id).Should().NotIntersectWith(page2.Select(m => m.Id));
    }

    private sealed class FakeMediaStorage : IMediaStorage
    {
        public Task<string> StoreAsync(Stream content, string subdirectory,
            string fileName, CancellationToken ct = default) => Task.FromResult($"{subdirectory}/{fileName}");
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) => Task.CompletedTask;
        public string GetPublicUrl(string relativePath) => "/" + relativePath;
    }
}
