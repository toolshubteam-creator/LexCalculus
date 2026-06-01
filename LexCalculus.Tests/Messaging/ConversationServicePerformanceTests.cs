using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Messaging;

/// <summary>
/// Adım 6.10 (#31/#32) — ConversationService n+1 refactor kanıtı + davranış
/// regresyonu. <see cref="QueryCounterInterceptor"/> ile gerçek SQL Server
/// LocalDB üzerinde yürütülen komut sayısı ölçülür (tek query iddiası).
/// </summary>
public sealed class ConversationServicePerformanceTests : SqlServerTestBase
{
    private static ApplicationUser MakeUser(string suffix) => new()
    {
        UserName = $"u{suffix}@x.com", NormalizedUserName = $"U{suffix}@X.COM",
        Email = $"u{suffix}@x.com", NormalizedEmail = $"U{suffix}@X.COM",
        FullName = $"User {suffix}", CreatedAt = DateTime.UtcNow,
        IsActive = true, EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    // Counter takılı context + bu context'i kullanan servis. GetForUserAsync
    // _blockService çağırmaz (engelleme filtresi query içinde) — yine de geçilir.
    private (QueryCounterInterceptor counter, ConversationService svc, ApplicationDbContext ctx) BuildCounted()
    {
        var counter = new QueryCounterInterceptor();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_db.ConnectionString)
            .AddInterceptors(counter)
            .Options;
        var ctx = _db.Create(options);
        var svc = new ConversationService(
            ctx, new UserBlockService(ctx, new NullActivityLogService()),
            new FakeMediaStorage(), new NullActivityLogService());
        return (counter, svc, ctx);
    }

    private Conversation SeedConversation(
        ApplicationDbContext ctx, int viewerId, int otherId, DateTime lastMessageAt)
    {
        var conv = new Conversation
        {
            User1Id = Math.Min(viewerId, otherId),
            User2Id = Math.Max(viewerId, otherId),
            CreatedAt = lastMessageAt,
            LastMessageAt = lastMessageAt
        };
        ctx.Conversations.Add(conv);
        return conv;
    }

    [Fact]
    public async Task GetForUserAsync_TenConversations_ExecutesSingleQuery()
    {
        int viewerId;
        // Seed — audit'siz context (explicit CreatedAt korunur).
        using (var seed = _db.CreateNoAuditContext())
        {
            var viewer = MakeUser("viewer");
            seed.Users.Add(viewer);
            await seed.SaveChangesAsync();
            viewerId = viewer.Id;

            var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 10; i++)
            {
                var other = MakeUser($"o{i}");
                seed.Users.Add(other);
                await seed.SaveChangesAsync();

                var conv = SeedConversation(seed, viewerId, other.Id, baseTime.AddMinutes(i));
                await seed.SaveChangesAsync();

                // Her conv'a karşı taraftan 2 mesaj (unread alt sorgusunu da çalıştırır).
                seed.Messages.AddRange(
                    new Message { ConversationId = conv.Id, SenderId = other.Id, Body = "<p>a</p>", CreatedAt = baseTime.AddMinutes(i) },
                    new Message { ConversationId = conv.Id, SenderId = other.Id, Body = "<p>b</p>", CreatedAt = baseTime.AddMinutes(i).AddSeconds(30) });
                await seed.SaveChangesAsync();
            }
        }

        var (counter, svc, ctx) = BuildCounted();
        using (ctx)
        {
            counter.Reset();
            var list = await svc.GetForUserAsync(viewerId);

            list.Should().HaveCount(10);
            counter.Count.Should().Be(1, "10 conversation tek SELECT ile çekilmeli (n+1 yok)");
        }
    }

    [Fact]
    public async Task GetForUserAsync_NoConversations_ExecutesSingleQuery()
    {
        int viewerId;
        using (var seed = _db.CreateNoAuditContext())
        {
            var viewer = MakeUser("solo");
            seed.Users.Add(viewer);
            await seed.SaveChangesAsync();
            viewerId = viewer.Id;
        }

        var (counter, svc, ctx) = BuildCounted();
        using (ctx)
        {
            counter.Reset();
            var list = await svc.GetForUserAsync(viewerId);

            list.Should().BeEmpty();
            counter.Count.Should().Be(1, "boş sonuç da tek query (erken çıkış sorgudan SONRA)");
        }
    }

    [Fact]
    public async Task GetForUserAsync_ReturnsCorrectLastMessageAndUnread()
    {
        int viewerId, o1Id, o2Id;
        var baseTime = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc);
        using (var seed = _db.CreateNoAuditContext())
        {
            var viewer = MakeUser("v"); var o1 = MakeUser("p1"); var o2 = MakeUser("p2");
            seed.Users.AddRange(viewer, o1, o2);
            await seed.SaveChangesAsync();
            viewerId = viewer.Id; o1Id = o1.Id; o2Id = o2.Id;

            // conv1: en yeni mesaj, 2 unread; viewer hiç okumadı.
            var c1 = SeedConversation(seed, viewerId, o1Id, baseTime.AddMinutes(10));
            // conv2: daha eski, 1 unread + viewer'ın kendi mesajı (sayılmaz).
            var c2 = SeedConversation(seed, viewerId, o2Id, baseTime.AddMinutes(5));
            await seed.SaveChangesAsync();

            seed.Messages.AddRange(
                new Message { ConversationId = c1.Id, SenderId = o1Id, Body = "<p>Merhaba dunya</p>", CreatedAt = baseTime.AddMinutes(9) },
                new Message { ConversationId = c1.Id, SenderId = o1Id, Body = "<p>son mesaj</p>", CreatedAt = baseTime.AddMinutes(10) },
                new Message { ConversationId = c2.Id, SenderId = viewerId, Body = "<p>benim</p>", CreatedAt = baseTime.AddMinutes(4) },
                new Message { ConversationId = c2.Id, SenderId = o2Id, Body = "<p>tek</p>", CreatedAt = baseTime.AddMinutes(5) });
            await seed.SaveChangesAsync();
        }

        var (_, svc, ctx) = BuildCounted();
        using (ctx)
        {
            var list = await svc.GetForUserAsync(viewerId);

            list.Should().HaveCount(2);
            // LastMessageAt DESC → conv1 üstte.
            list[0].OtherUserId.Should().Be(o1Id);
            list[0].LastMessagePreview.Should().Be("son mesaj");
            list[0].UnreadCount.Should().Be(2);

            list[1].OtherUserId.Should().Be(o2Id);
            list[1].LastMessagePreview.Should().Be("tek");
            list[1].UnreadCount.Should().Be(1, "viewer'ın kendi mesajı unread sayılmaz");
        }
    }

    [Fact]
    public async Task GetForUserAsync_BlockedConversations_Excluded()
    {
        int viewerId, friendId, blockedId;
        var baseTime = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        using (var seed = _db.CreateNoAuditContext())
        {
            var viewer = MakeUser("v"); var friend = MakeUser("f"); var blocked = MakeUser("b");
            seed.Users.AddRange(viewer, friend, blocked);
            await seed.SaveChangesAsync();
            viewerId = viewer.Id; friendId = friend.Id; blockedId = blocked.Id;

            SeedConversation(seed, viewerId, friendId, baseTime.AddMinutes(2));
            SeedConversation(seed, viewerId, blockedId, baseTime.AddMinutes(1));
            seed.UserBlocks.Add(new Core.Entities.Social.UserBlock
            {
                BlockerId = viewerId, BlockedId = blockedId, CreatedAt = baseTime
            });
            await seed.SaveChangesAsync();
        }

        var (counter, svc, ctx) = BuildCounted();
        using (ctx)
        {
            counter.Reset();
            var list = await svc.GetForUserAsync(viewerId);

            list.Should().HaveCount(1);
            list[0].OtherUserId.Should().Be(friendId);
            counter.Count.Should().Be(1, "engelleme filtresi de aynı query içinde (ayrı round-trip yok)");
        }
    }

    [Fact]
    public async Task GetUnreadCountAsync_SumsAcrossAllConversations_SingleQuery()
    {
        int viewerId;
        var baseTime = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);
        using (var seed = _db.CreateNoAuditContext())
        {
            var viewer = MakeUser("v");
            seed.Users.Add(viewer);
            await seed.SaveChangesAsync();
            viewerId = viewer.Id;

            // 3 conv: sırasıyla 2 + 1 + 0 unread (toplam 3). Biri okunmuş eşik üstü.
            for (var i = 0; i < 3; i++)
            {
                var other = MakeUser($"x{i}");
                seed.Users.Add(other);
                await seed.SaveChangesAsync();

                var conv = SeedConversation(seed, viewerId, other.Id, baseTime.AddMinutes(i));
                // conv #2 (i=2): viewer okumuş → 0 unread.
                if (i == 2)
                {
                    if (conv.User1Id == viewerId) conv.User1LastReadAt = baseTime.AddHours(1);
                    else conv.User2LastReadAt = baseTime.AddHours(1);
                }
                await seed.SaveChangesAsync();

                var count = i == 0 ? 2 : 1;   // i0→2, i1→1, i2→1 ama okunmuş → 0
                for (var m = 0; m < count; m++)
                {
                    seed.Messages.Add(new Message
                    {
                        ConversationId = conv.Id, SenderId = other.Id,
                        Body = "<p>m</p>", CreatedAt = baseTime.AddMinutes(i).AddSeconds(m)
                    });
                }
                await seed.SaveChangesAsync();
            }
        }

        var (counter, svc, ctx) = BuildCounted();
        using (ctx)
        {
            counter.Reset();
            var total = await svc.GetUnreadCountAsync(viewerId);

            total.Should().Be(3, "conv0=2 + conv1=1 + conv2=0 (okunmuş) = 3");
            counter.Count.Should().Be(1, "tek SELECT COUNT (n+1 yok)");
        }
    }

    private sealed class FakeMediaStorage : IMediaStorage
    {
        public Task<string> StoreAsync(Stream content, string subdirectory,
            string fileName, CancellationToken ct = default) => Task.FromResult($"{subdirectory}/{fileName}");
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) => Task.CompletedTask;
        public string GetPublicUrl(string relativePath) => "/" + relativePath;
    }
}
