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

public class ConversationServiceTests
{
    private static (ConversationService svc, ApplicationDbContext ctx) Setup()
    {
        var ctx = TestDbContextFactory.Create();
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        var storage = new FakeMediaStorage();
        var svc = new ConversationService(ctx, blockSvc, storage, new NullActivityLogService());

        // 4 user seed
        ctx.Users.AddRange(
            MakeUser(1), MakeUser(2), MakeUser(3), MakeUser(4));
        ctx.SaveChanges();
        return (svc, ctx);
    }

    private static ApplicationUser MakeUser(int id, int? tenantId = null) => new()
    {
        Id = id,
        UserName = $"u{id}@x.com", NormalizedUserName = $"U{id}@X.COM",
        Email = $"u{id}@x.com", NormalizedEmail = $"U{id}@X.COM",
        FullName = $"User {id}", CreatedAt = DateTime.UtcNow,
        IsActive = true, EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString(),
        TenantId = tenantId
    };

    private static async Task SeedAcceptedConnectionAsync(
        ApplicationDbContext ctx, int requester, int target)
    {
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = requester, TargetId = target,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            RespondedAt = DateTime.UtcNow.AddDays(-1)
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task GetOrCreateAsync_SelfMessage_ReturnsError()
    {
        var (svc, _) = Setup();
        var result = await svc.GetOrCreateAsync(1, 1);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kendinize");
    }

    [Fact]
    public async Task GetOrCreateAsync_NoConnectionNoTenant_ReturnsError()
    {
        var (svc, _) = Setup();
        var result = await svc.GetOrCreateAsync(1, 2);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("gönderilemiyor");
    }

    [Fact]
    public async Task GetOrCreateAsync_AcceptedConnection_CreatesConversation()
    {
        var (svc, ctx) = Setup();
        await SeedAcceptedConnectionAsync(ctx, 1, 2);

        var result = await svc.GetOrCreateAsync(1, 2);

        result.Success.Should().BeTrue();
        result.Conversation.Should().NotBeNull();
        result.Conversation!.User1Id.Should().Be(1);  // Math.Min
        result.Conversation.User2Id.Should().Be(2);
    }

    [Fact]
    public async Task GetOrCreateAsync_DeterministicOrder_BothDirectionsSameConversation()
    {
        var (svc, ctx) = Setup();
        await SeedAcceptedConnectionAsync(ctx, 1, 2);

        var first = await svc.GetOrCreateAsync(1, 2);
        var second = await svc.GetOrCreateAsync(2, 1);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        first.Conversation!.Id.Should().Be(second.Conversation!.Id);
        (await ctx.Conversations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_SameTenant_AllowsCreate()
    {
        var ctx = TestDbContextFactory.Create();
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        var storage = new FakeMediaStorage();
        var svc = new ConversationService(ctx, blockSvc, storage, new NullActivityLogService());

        ctx.Tenants.Add(new Tenant
        {
            Id = 100, Name = "T", Slug = "t",
            CreatedAt = DateTime.UtcNow, OwnerUserId = 1
        });
        ctx.Users.AddRange(MakeUser(1, 100), MakeUser(2, 100));
        await ctx.SaveChangesAsync();

        var result = await svc.GetOrCreateAsync(1, 2);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrCreateAsync_BlockedEitherWay_ReturnsError()
    {
        var (svc, ctx) = Setup();
        await SeedAcceptedConnectionAsync(ctx, 1, 2);
        ctx.UserBlocks.Add(new UserBlock
        {
            BlockerId = 2, BlockedId = 1, CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var result = await svc.GetOrCreateAsync(1, 2);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("gönderilemiyor");
    }

    [Fact]
    public async Task GetByIdAsync_NonParticipant_ReturnsNull()
    {
        var (svc, ctx) = Setup();
        await SeedAcceptedConnectionAsync(ctx, 1, 2);
        var created = await svc.GetOrCreateAsync(1, 2);

        var result = await svc.GetByIdAsync(created.Conversation!.Id, viewerId: 3);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_Participant_ReturnsConversation()
    {
        var (svc, ctx) = Setup();
        await SeedAcceptedConnectionAsync(ctx, 1, 2);
        var created = await svc.GetOrCreateAsync(1, 2);

        var result = await svc.GetByIdAsync(created.Conversation!.Id, viewerId: 1);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesUser1LastReadAt_WhenViewerIsUser1()
    {
        var (svc, ctx) = Setup();
        await SeedAcceptedConnectionAsync(ctx, 1, 2);
        var created = await svc.GetOrCreateAsync(1, 2);

        var result = await svc.MarkAsReadAsync(created.Conversation!.Id, userId: 1);

        result.Success.Should().BeTrue();
        var conv = await ctx.Conversations.FirstAsync(c => c.Id == created.Conversation.Id);
        conv.User1LastReadAt.Should().NotBeNull();
        conv.User2LastReadAt.Should().BeNull();
    }

    [Fact]
    public async Task GetUnreadCountAsync_CountsOnlyOtherSenderAfterLastRead()
    {
        var (svc, ctx) = Setup();
        await SeedAcceptedConnectionAsync(ctx, 1, 2);
        var conv = (await svc.GetOrCreateAsync(1, 2)).Conversation!;
        var now = DateTime.UtcNow;

        // 2'den 1'e 3 mesaj
        ctx.Messages.AddRange(
            new Message { ConversationId = conv.Id, SenderId = 2, Body = "<p>m1</p>", CreatedAt = now.AddMinutes(-3), IsDeleted = false },
            new Message { ConversationId = conv.Id, SenderId = 2, Body = "<p>m2</p>", CreatedAt = now.AddMinutes(-2), IsDeleted = false },
            new Message { ConversationId = conv.Id, SenderId = 2, Body = "<p>m3</p>", CreatedAt = now.AddMinutes(-1), IsDeleted = false });
        // 1'den 2'ye mesaj — User1 unread sayımına dahil olmamalı
        ctx.Messages.Add(new Message
        {
            ConversationId = conv.Id, SenderId = 1, Body = "<p>self</p>",
            CreatedAt = now, IsDeleted = false
        });
        await ctx.SaveChangesAsync();

        var unread = await svc.GetUnreadCountAsync(userId: 1);
        unread.Should().Be(3);

        // MarkAsRead sonrası 0
        await svc.MarkAsReadAsync(conv.Id, 1);
        var afterRead = await svc.GetUnreadCountAsync(userId: 1);
        afterRead.Should().Be(0);
    }

    [Fact]
    public async Task GetForUserAsync_ExcludesBlockedConversations()
    {
        var (svc, ctx) = Setup();
        // 1<->2 + 1<->3 conversation
        await SeedAcceptedConnectionAsync(ctx, 1, 2);
        await SeedAcceptedConnectionAsync(ctx, 1, 3);
        await svc.GetOrCreateAsync(1, 2);
        await svc.GetOrCreateAsync(1, 3);

        // 1, 3'ü engelliyor
        ctx.UserBlocks.Add(new UserBlock
        {
            BlockerId = 1, BlockedId = 3, CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var list = await svc.GetForUserAsync(1);

        list.Should().HaveCount(1);
        list[0].OtherUserId.Should().Be(2);
    }

    [Fact]
    public async Task GetForUserAsync_OrdersByLastMessageAtDesc()
    {
        var (svc, ctx) = Setup();
        await SeedAcceptedConnectionAsync(ctx, 1, 2);
        await SeedAcceptedConnectionAsync(ctx, 1, 3);

        var c12 = (await svc.GetOrCreateAsync(1, 2)).Conversation!;
        await Task.Delay(20);
        var c13 = (await svc.GetOrCreateAsync(1, 3)).Conversation!;

        // c12'ye sonra mesaj — LastMessageAt güncellenmiş gibi simüle
        var trackedC12 = await ctx.Conversations.FirstAsync(c => c.Id == c12.Id);
        trackedC12.LastMessageAt = DateTime.UtcNow.AddSeconds(10);
        await ctx.SaveChangesAsync();

        var list = await svc.GetForUserAsync(1);

        list.Should().HaveCount(2);
        list[0].ConversationId.Should().Be(c12.Id);
        list[1].ConversationId.Should().Be(c13.Id);
    }

    private sealed class FakeMediaStorage : IMediaStorage
    {
        public Task<string> StoreAsync(Stream content, string subdirectory,
            string fileName, CancellationToken ct = default) => Task.FromResult($"{subdirectory}/{fileName}");
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) => Task.CompletedTask;
        public string GetPublicUrl(string relativePath) => "/" + relativePath;
    }
}
