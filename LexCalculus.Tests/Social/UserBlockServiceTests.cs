using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Social;

public class UserBlockServiceTests : SqlServerTestBase
{
    private (UserBlockService svc, ApplicationDbContext ctx) Setup()
    {
        var ctx = _db.Create();
        var svc = new UserBlockService(ctx, new NullActivityLogService());
        return (svc, ctx);
    }

    private static ApplicationUser MakeUser(string suffix, bool isActive = true) => new()
    {
        UserName = $"u{suffix}@x.com",
        NormalizedUserName = $"U{suffix}@X.COM",
        Email = $"u{suffix}@x.com",
        NormalizedEmail = $"U{suffix}@X.COM",
        FullName = $"User {suffix}",
        CreatedAt = DateTime.UtcNow,
        IsActive = isActive,
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    [Fact]
    public async Task BlockAsync_Self_ReturnsError()
    {
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var result = await svc.BlockAsync(u1.Id, u1.Id);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kendinizi");
    }

    [Fact]
    public async Task BlockAsync_InactiveTarget_ReturnsError()
    {
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2", isActive: false);
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var result = await svc.BlockAsync(u1.Id, u2.Id);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bulunamadı");
    }

    [Fact]
    public async Task BlockAsync_Valid_CreatesBlock()
    {
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var result = await svc.BlockAsync(u1.Id, u2.Id);

        result.Success.Should().BeTrue();
        var block = await ctx.UserBlocks.FirstOrDefaultAsync(
            b => b.BlockerId == u1.Id && b.BlockedId == u2.Id);
        block.Should().NotBeNull();
        block!.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task BlockAsync_AlreadyBlocked_ReturnsError()
    {
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();
        await svc.BlockAsync(u1.Id, u2.Id);

        var second = await svc.BlockAsync(u1.Id, u2.Id);

        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("zaten engellediniz");
    }

    [Fact]
    public async Task BlockAsync_WithExistingAcceptedConnection_RemovesConnection()
    {
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = u1.Id, TargetId = u2.Id,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            RespondedAt = DateTime.UtcNow.AddHours(-1)
        });
        await ctx.SaveChangesAsync();

        var result = await svc.BlockAsync(u1.Id, u2.Id);

        result.Success.Should().BeTrue();
        var anyConn = await ctx.UserConnections.AnyAsync(
            c => (c.RequesterId == u1.Id && c.TargetId == u2.Id)
              || (c.RequesterId == u2.Id && c.TargetId == u1.Id));
        anyConn.Should().BeFalse("cascade ile mevcut Accepted bağlantı silindi");
    }

    [Fact]
    public async Task BlockAsync_WithReverseDirectionAcceptedConnection_AlsoRemoves()
    {
        // 2→1 Accepted (yön ters), 1 engelliyor → silinmeli
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = u2.Id, TargetId = u1.Id,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            RespondedAt = DateTime.UtcNow.AddHours(-1)
        });
        await ctx.SaveChangesAsync();

        await svc.BlockAsync(u1.Id, u2.Id);

        (await ctx.UserConnections.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task BlockAsync_WithoutExistingConnection_DoesNotFail()
    {
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var result = await svc.BlockAsync(u1.Id, u2.Id);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task BlockAsync_OnlyAcceptedConnectionRemoved_PendingPreserved()
    {
        // Pending bağlantı engellemeyle silinmemeli (servis yalnızca Accepted'a dokunur).
        // Pending zaten "henüz bağlı değil"; engelleme onu kaldırmaz, sonraki Send
        // engelleme defansif kontrolüne takılır.
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = u1.Id, TargetId = u2.Id,
            Status = UserConnectionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        await svc.BlockAsync(u1.Id, u2.Id);

        var pendingExists = await ctx.UserConnections.AnyAsync(
            c => c.Status == UserConnectionStatus.Pending);
        pendingExists.Should().BeTrue("Pending kayıtlar engellemeyle silinmez");
    }

    [Fact]
    public async Task UnblockAsync_NotBlocked_ReturnsError()
    {
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var result = await svc.UnblockAsync(u1.Id, u2.Id);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bulunamadı");
    }

    [Fact]
    public async Task UnblockAsync_Valid_RemovesBlock()
    {
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();
        await svc.BlockAsync(u1.Id, u2.Id);

        var result = await svc.UnblockAsync(u1.Id, u2.Id);

        result.Success.Should().BeTrue();
        (await ctx.UserBlocks.AnyAsync(
            b => b.BlockerId == u1.Id && b.BlockedId == u2.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task IsBlockedAsync_ReturnsTrueOnlyForExactDirection()
    {
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();
        await svc.BlockAsync(u1.Id, u2.Id);

        (await svc.IsBlockedAsync(u1.Id, u2.Id)).Should().BeTrue();
        (await svc.IsBlockedAsync(u2.Id, u1.Id)).Should().BeFalse(
            "ters yön sorgulaması — yalnızca 1→2 var");
    }

    [Fact]
    public async Task IsEitherDirectionBlockedAsync_TrueForBothDirections()
    {
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();
        await svc.BlockAsync(u1.Id, u2.Id);

        (await svc.IsEitherDirectionBlockedAsync(u1.Id, u2.Id)).Should().BeTrue();
        (await svc.IsEitherDirectionBlockedAsync(u2.Id, u1.Id)).Should().BeTrue(
            "yön bağımsız mutual check");
    }

    [Fact]
    public async Task GetBlockedByUserAsync_ReturnsOnlyMyBlocks()
    {
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        var u3 = MakeUser("3");
        var u4 = MakeUser("4");
        ctx.Users.AddRange(u1, u2, u3, u4);
        await ctx.SaveChangesAsync();
        await svc.BlockAsync(u1.Id, u2.Id);
        await svc.BlockAsync(u1.Id, u3.Id);
        await svc.BlockAsync(u4.Id, u2.Id); // 4'ün block'u — 1 görmemeli

        var list = await svc.GetBlockedByUserAsync(u1.Id);

        list.Should().HaveCount(2);
        list.Select(b => b.BlockedId).Should().BeEquivalentTo(new[] { u2.Id, u3.Id });
    }

    [Fact]
    public async Task GetBlockedCountAsync_ReturnsCount()
    {
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        var u3 = MakeUser("3");
        ctx.Users.AddRange(u1, u2, u3);
        await ctx.SaveChangesAsync();
        await svc.BlockAsync(u1.Id, u2.Id);
        await svc.BlockAsync(u1.Id, u3.Id);

        (await svc.GetBlockedCountAsync(u1.Id)).Should().Be(2);
        (await svc.GetBlockedCountAsync(u2.Id)).Should().Be(0);
    }
}
