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

// Adım 5.8 — SQL Server LocalDB servis testi (SqlServerTestBase, per-test DB).
public class ConversationServiceTests : SqlServerTestBase
{
    // Setup 4 user seed eder; generated Id'leri u1..u4 ile döner.
    private (ConversationService svc, ApplicationDbContext ctx,
        ApplicationUser u1, ApplicationUser u2, ApplicationUser u3, ApplicationUser u4) Setup()
    {
        var ctx = _db.Create();
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        var storage = new FakeMediaStorage();
        var svc = new ConversationService(ctx, blockSvc, storage, new NullActivityLogService());

        // 4 user seed — Id'ler EF tarafından üretilir
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        var u3 = MakeUser("3");
        var u4 = MakeUser("4");
        ctx.Users.AddRange(u1, u2, u3, u4);
        ctx.SaveChanges();
        return (svc, ctx, u1, u2, u3, u4);
    }

    private static ApplicationUser MakeUser(string suffix, int? tenantId = null) => new()
    {
        UserName = $"u{suffix}@x.com", NormalizedUserName = $"U{suffix}@X.COM",
        Email = $"u{suffix}@x.com", NormalizedEmail = $"U{suffix}@X.COM",
        FullName = $"User {suffix}", CreatedAt = DateTime.UtcNow,
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
        var (svc, _, u1, _, _, _) = Setup();
        var result = await svc.GetOrCreateAsync(u1.Id, u1.Id);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kendinize");
    }

    [Fact]
    public async Task GetOrCreateAsync_NoConnectionNoTenant_ReturnsError()
    {
        var (svc, _, u1, u2, _, _) = Setup();
        var result = await svc.GetOrCreateAsync(u1.Id, u2.Id);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("gönderilemiyor");
    }

    [Fact]
    public async Task GetOrCreateAsync_AcceptedConnection_CreatesConversation()
    {
        var (svc, ctx, u1, u2, _, _) = Setup();
        await SeedAcceptedConnectionAsync(ctx, u1.Id, u2.Id);

        var result = await svc.GetOrCreateAsync(u1.Id, u2.Id);

        result.Success.Should().BeTrue();
        result.Conversation.Should().NotBeNull();
        result.Conversation!.User1Id.Should().Be(Math.Min(u1.Id, u2.Id));  // Math.Min
        result.Conversation.User2Id.Should().Be(Math.Max(u1.Id, u2.Id));
    }

    [Fact]
    public async Task GetOrCreateAsync_DeterministicOrder_BothDirectionsSameConversation()
    {
        var (svc, ctx, u1, u2, _, _) = Setup();
        await SeedAcceptedConnectionAsync(ctx, u1.Id, u2.Id);

        var first = await svc.GetOrCreateAsync(u1.Id, u2.Id);
        var second = await svc.GetOrCreateAsync(u2.Id, u1.Id);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        first.Conversation!.Id.Should().Be(second.Conversation!.Id);
        (await ctx.Conversations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_SameTenant_AllowsCreate()
    {
        var ctx = _db.Create();
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        var storage = new FakeMediaStorage();
        var svc = new ConversationService(ctx, blockSvc, storage, new NullActivityLogService());

        // Circular FK staging: (a) user'lar TenantId = null ile, (b) Tenant OwnerUserId ile,
        // (c) user.TenantId güncelle.
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var tenant = new Tenant
        {
            Name = "T", Slug = "t",
            CreatedAt = DateTime.UtcNow, OwnerUserId = u1.Id
        };
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        u1.TenantId = tenant.Id;
        u2.TenantId = tenant.Id;
        await ctx.SaveChangesAsync();

        var result = await svc.GetOrCreateAsync(u1.Id, u2.Id);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrCreateAsync_BlockedEitherWay_ReturnsError()
    {
        var (svc, ctx, u1, u2, _, _) = Setup();
        await SeedAcceptedConnectionAsync(ctx, u1.Id, u2.Id);
        ctx.UserBlocks.Add(new UserBlock
        {
            BlockerId = u2.Id, BlockedId = u1.Id, CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var result = await svc.GetOrCreateAsync(u1.Id, u2.Id);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("gönderilemiyor");
    }

    [Fact]
    public async Task GetByIdAsync_NonParticipant_ReturnsNull()
    {
        var (svc, ctx, u1, u2, u3, _) = Setup();
        await SeedAcceptedConnectionAsync(ctx, u1.Id, u2.Id);
        var created = await svc.GetOrCreateAsync(u1.Id, u2.Id);

        var result = await svc.GetByIdAsync(created.Conversation!.Id, viewerId: u3.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_Participant_ReturnsConversation()
    {
        var (svc, ctx, u1, u2, _, _) = Setup();
        await SeedAcceptedConnectionAsync(ctx, u1.Id, u2.Id);
        var created = await svc.GetOrCreateAsync(u1.Id, u2.Id);

        var result = await svc.GetByIdAsync(created.Conversation!.Id, viewerId: u1.Id);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesUser1LastReadAt_WhenViewerIsUser1()
    {
        var (svc, ctx, u1, u2, _, _) = Setup();
        await SeedAcceptedConnectionAsync(ctx, u1.Id, u2.Id);
        var created = await svc.GetOrCreateAsync(u1.Id, u2.Id);

        // Math.Min/Max ile User1 belirlenir — viewer User1 olmalı
        var user1Id = Math.Min(u1.Id, u2.Id);
        var result = await svc.MarkAsReadAsync(created.Conversation!.Id, userId: user1Id);

        result.Success.Should().BeTrue();
        var conv = await ctx.Conversations.FirstAsync(c => c.Id == created.Conversation.Id);
        conv.User1LastReadAt.Should().NotBeNull();
        conv.User2LastReadAt.Should().BeNull();
    }

    [Fact]
    public async Task GetUnreadCountAsync_CountsOnlyOtherSenderAfterLastRead()
    {
        var (svc, ctx, u1, u2, _, _) = Setup();
        await SeedAcceptedConnectionAsync(ctx, u1.Id, u2.Id);
        var conv = (await svc.GetOrCreateAsync(u1.Id, u2.Id)).Conversation!;
        var now = DateTime.UtcNow;

        // 2'den 1'e 3 mesaj
        ctx.Messages.AddRange(
            new Message { ConversationId = conv.Id, SenderId = u2.Id, Body = "<p>m1</p>", CreatedAt = now.AddMinutes(-3), IsDeleted = false },
            new Message { ConversationId = conv.Id, SenderId = u2.Id, Body = "<p>m2</p>", CreatedAt = now.AddMinutes(-2), IsDeleted = false },
            new Message { ConversationId = conv.Id, SenderId = u2.Id, Body = "<p>m3</p>", CreatedAt = now.AddMinutes(-1), IsDeleted = false });
        // 1'den 2'ye mesaj — User1 unread sayımına dahil olmamalı
        ctx.Messages.Add(new Message
        {
            ConversationId = conv.Id, SenderId = u1.Id, Body = "<p>self</p>",
            CreatedAt = now, IsDeleted = false
        });
        await ctx.SaveChangesAsync();

        var unread = await svc.GetUnreadCountAsync(userId: u1.Id);
        unread.Should().Be(3);

        // MarkAsRead sonrası 0
        await svc.MarkAsReadAsync(conv.Id, u1.Id);
        var afterRead = await svc.GetUnreadCountAsync(userId: u1.Id);
        afterRead.Should().Be(0);
    }

    [Fact]
    public async Task GetForUserAsync_ExcludesBlockedConversations()
    {
        var (svc, ctx, u1, u2, u3, _) = Setup();
        // 1<->2 + 1<->3 conversation
        await SeedAcceptedConnectionAsync(ctx, u1.Id, u2.Id);
        await SeedAcceptedConnectionAsync(ctx, u1.Id, u3.Id);
        await svc.GetOrCreateAsync(u1.Id, u2.Id);
        await svc.GetOrCreateAsync(u1.Id, u3.Id);

        // 1, 3'ü engelliyor
        ctx.UserBlocks.Add(new UserBlock
        {
            BlockerId = u1.Id, BlockedId = u3.Id, CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var list = await svc.GetForUserAsync(u1.Id);

        list.Should().HaveCount(1);
        list[0].OtherUserId.Should().Be(u2.Id);
    }

    [Fact]
    public async Task GetForUserAsync_OrdersByLastMessageAtDesc()
    {
        var (svc, ctx, u1, u2, u3, _) = Setup();
        await SeedAcceptedConnectionAsync(ctx, u1.Id, u2.Id);
        await SeedAcceptedConnectionAsync(ctx, u1.Id, u3.Id);

        var c12 = (await svc.GetOrCreateAsync(u1.Id, u2.Id)).Conversation!;
        await Task.Delay(20);
        var c13 = (await svc.GetOrCreateAsync(u1.Id, u3.Id)).Conversation!;

        // c12'ye sonra mesaj — LastMessageAt güncellenmiş gibi simüle
        var trackedC12 = await ctx.Conversations.FirstAsync(c => c.Id == c12.Id);
        trackedC12.LastMessageAt = DateTime.UtcNow.AddSeconds(10);
        await ctx.SaveChangesAsync();

        var list = await svc.GetForUserAsync(u1.Id);

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
