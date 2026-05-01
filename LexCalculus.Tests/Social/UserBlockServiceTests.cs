using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Social;

public class UserBlockServiceTests
{
    private static (UserBlockService svc, ApplicationDbContext ctx) Setup()
    {
        var ctx = TestDbContextFactory.Create();
        var svc = new UserBlockService(ctx, new NullActivityLogService());
        return (svc, ctx);
    }

    private static ApplicationUser MakeUser(int id, bool isActive = true) => new()
    {
        Id = id,
        UserName = $"u{id}@x.com",
        NormalizedUserName = $"U{id}@X.COM",
        Email = $"u{id}@x.com",
        NormalizedEmail = $"U{id}@X.COM",
        FullName = $"User {id}",
        CreatedAt = DateTime.UtcNow,
        IsActive = isActive,
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static async Task SeedUsersAsync(ApplicationDbContext ctx, params int[] ids)
    {
        foreach (var id in ids) ctx.Users.Add(MakeUser(id));
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task BlockAsync_Self_ReturnsError()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1);

        var result = await svc.BlockAsync(1, 1);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kendinizi");
    }

    [Fact]
    public async Task BlockAsync_InactiveTarget_ReturnsError()
    {
        var (svc, ctx) = Setup();
        ctx.Users.AddRange(MakeUser(1), MakeUser(2, isActive: false));
        await ctx.SaveChangesAsync();

        var result = await svc.BlockAsync(1, 2);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bulunamadı");
    }

    [Fact]
    public async Task BlockAsync_Valid_CreatesBlock()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);

        var result = await svc.BlockAsync(1, 2);

        result.Success.Should().BeTrue();
        var block = await ctx.UserBlocks.FirstOrDefaultAsync(
            b => b.BlockerId == 1 && b.BlockedId == 2);
        block.Should().NotBeNull();
        block!.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task BlockAsync_AlreadyBlocked_ReturnsError()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        await svc.BlockAsync(1, 2);

        var second = await svc.BlockAsync(1, 2);

        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("zaten engellediniz");
    }

    [Fact]
    public async Task BlockAsync_WithExistingAcceptedConnection_RemovesConnection()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = 1, TargetId = 2,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            RespondedAt = DateTime.UtcNow.AddHours(-1)
        });
        await ctx.SaveChangesAsync();

        var result = await svc.BlockAsync(1, 2);

        result.Success.Should().BeTrue();
        var anyConn = await ctx.UserConnections.AnyAsync(
            c => (c.RequesterId == 1 && c.TargetId == 2)
              || (c.RequesterId == 2 && c.TargetId == 1));
        anyConn.Should().BeFalse("cascade ile mevcut Accepted bağlantı silindi");
    }

    [Fact]
    public async Task BlockAsync_WithReverseDirectionAcceptedConnection_AlsoRemoves()
    {
        // 2→1 Accepted (yön ters), 1 engelliyor → silinmeli
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = 2, TargetId = 1,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            RespondedAt = DateTime.UtcNow.AddHours(-1)
        });
        await ctx.SaveChangesAsync();

        await svc.BlockAsync(1, 2);

        (await ctx.UserConnections.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task BlockAsync_WithoutExistingConnection_DoesNotFail()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);

        var result = await svc.BlockAsync(1, 2);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task BlockAsync_OnlyAcceptedConnectionRemoved_PendingPreserved()
    {
        // Pending bağlantı engellemeyle silinmemeli (servis yalnızca Accepted'a dokunur).
        // Pending zaten "henüz bağlı değil"; engelleme onu kaldırmaz, sonraki Send
        // engelleme defansif kontrolüne takılır.
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = 1, TargetId = 2,
            Status = UserConnectionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        await svc.BlockAsync(1, 2);

        var pendingExists = await ctx.UserConnections.AnyAsync(
            c => c.Status == UserConnectionStatus.Pending);
        pendingExists.Should().BeTrue("Pending kayıtlar engellemeyle silinmez");
    }

    [Fact]
    public async Task UnblockAsync_NotBlocked_ReturnsError()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);

        var result = await svc.UnblockAsync(1, 2);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bulunamadı");
    }

    [Fact]
    public async Task UnblockAsync_Valid_RemovesBlock()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        await svc.BlockAsync(1, 2);

        var result = await svc.UnblockAsync(1, 2);

        result.Success.Should().BeTrue();
        (await ctx.UserBlocks.AnyAsync(
            b => b.BlockerId == 1 && b.BlockedId == 2)).Should().BeFalse();
    }

    [Fact]
    public async Task IsBlockedAsync_ReturnsTrueOnlyForExactDirection()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        await svc.BlockAsync(1, 2);

        (await svc.IsBlockedAsync(1, 2)).Should().BeTrue();
        (await svc.IsBlockedAsync(2, 1)).Should().BeFalse(
            "ters yön sorgulaması — yalnızca 1→2 var");
    }

    [Fact]
    public async Task IsEitherDirectionBlockedAsync_TrueForBothDirections()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        await svc.BlockAsync(1, 2);

        (await svc.IsEitherDirectionBlockedAsync(1, 2)).Should().BeTrue();
        (await svc.IsEitherDirectionBlockedAsync(2, 1)).Should().BeTrue(
            "yön bağımsız mutual check");
    }

    [Fact]
    public async Task GetBlockedByUserAsync_ReturnsOnlyMyBlocks()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2, 3, 4);
        await svc.BlockAsync(1, 2);
        await svc.BlockAsync(1, 3);
        await svc.BlockAsync(4, 2); // 4'ün block'u — 1 görmemeli

        var list = await svc.GetBlockedByUserAsync(1);

        list.Should().HaveCount(2);
        list.Select(b => b.BlockedId).Should().BeEquivalentTo(new[] { 2, 3 });
    }

    [Fact]
    public async Task GetBlockedCountAsync_ReturnsCount()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2, 3);
        await svc.BlockAsync(1, 2);
        await svc.BlockAsync(1, 3);

        (await svc.GetBlockedCountAsync(1)).Should().Be(2);
        (await svc.GetBlockedCountAsync(2)).Should().Be(0);
    }
}
