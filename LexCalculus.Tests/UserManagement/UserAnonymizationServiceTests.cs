using FluentAssertions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.UserManagement;

public class UserAnonymizationServiceTests : SqlServerTestBase
{
    private (UserAnonymizationService svc, ApplicationDbContext ctx, FakeMediaStorage storage)
        Setup()
    {
        var ctx = _db.Create();
        var storage = new FakeMediaStorage();
        var svc = new UserAnonymizationService(ctx, storage, new NullActivityLogService(),
            new PostTagService(ctx));
        return (svc, ctx, storage);
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
        SecurityStamp = Guid.NewGuid().ToString(),
        PasswordHash = "PWDHASH"
    };

    // UserId FK olarak gerçek (üretilmiş) user id ile çağrılmalı.
    private static UserProfile MakeProfile(int userId, string displayName = "Test User") => new()
    {
        UserId = userId,
        DisplayName = displayName,
        Bio = "Bio metni",
        City = "Istanbul",
        AvatarUrl = $"uploads/avatars/{userId}/avatar.webp",
        PublicSlug = $"test-user-{userId}",
        IsPublicProfile = true,
        ShowConnections = true,
        ShowTenant = false,
        // BaroNo: filtered UNIQUE index across UserProfiles. Per-test seed
        // with multiple profiles would collide on a literal value, ve hiçbir
        // test üzerinde assert edilmiyor; null bırak (Adım 5.8 P2).
        BaroNo = null
    };

    // User'ı seed eder, EF'in ürettiği Id ile profili ekler, gerçek user id'yi döner.
    private static async Task<int> SeedUserWithProfileAsync(ApplicationDbContext ctx, string suffix)
    {
        var user = MakeUser(suffix);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var profile = MakeProfile(user.Id);
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task CanAnonymizeAsync_NonExistentUser_ReturnsCannotProceed()
    {
        var (svc, _, _) = Setup();
        var check = await svc.CanAnonymizeAsync(99999);
        check.CanProceed.Should().BeFalse();
        check.BlockerMessage.Should().Contain("bulunamadı");
    }

    [Fact]
    public async Task CanAnonymizeAsync_AlreadyInactive_ReturnsCannotProceed()
    {
        var (svc, ctx, _) = Setup();
        var user = MakeUser("1", isActive: false);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var check = await svc.CanAnonymizeAsync(user.Id);
        check.CanProceed.Should().BeFalse();
        check.BlockerMessage.Should().Contain("zaten");
    }

    [Fact]
    public async Task CanAnonymizeAsync_ValidUser_ReturnsCorrectCounts()
    {
        var (svc, ctx, _) = Setup();
        var user1Id = await SeedUserWithProfileAsync(ctx, "1");
        var user2Id = await SeedUserWithProfileAsync(ctx, "2");

        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = user1Id, TargetId = user2Id,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var check = await svc.CanAnonymizeAsync(user1Id);
        check.CanProceed.Should().BeTrue();
        check.ConnectionCount.Should().Be(1);
        check.PostCount.Should().Be(0);
    }

    [Fact]
    public async Task CanAnonymizeAsync_TenantOwnerWithOtherActiveMembers_Blocks()
    {
        var (svc, ctx, _) = Setup();
        var user1Id = await SeedUserWithProfileAsync(ctx, "1");
        var user2Id = await SeedUserWithProfileAsync(ctx, "2");

        // Circular FK staging: user'lar zaten seed edildi (TenantId null);
        // önce Tenant'ı OwnerUserId ile ekle, sonra user.TenantId güncelle.
        var tenant = new Tenant
        {
            Name = "Test Tenant", Slug = "test-tenant",
            CreatedAt = DateTime.UtcNow, OwnerUserId = user1Id
        };
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var owner = await ctx.Users.FirstAsync(u => u.Id == user1Id);
        var member = await ctx.Users.FirstAsync(u => u.Id == user2Id);
        owner.TenantId = tenant.Id;
        member.TenantId = tenant.Id;
        await ctx.SaveChangesAsync();

        var check = await svc.CanAnonymizeAsync(user1Id);
        check.CanProceed.Should().BeFalse();
        check.BlockerMessage.Should().Contain("Test Tenant");
    }

    [Fact]
    public async Task CanAnonymizeAsync_TenantOwnerWithNoOtherMembers_AllowsAnonymize()
    {
        var (svc, ctx, _) = Setup();
        var user1Id = await SeedUserWithProfileAsync(ctx, "1");

        // Circular FK staging: user seed edildi; Tenant'ı ekle, sonra user.TenantId güncelle.
        var tenant = new Tenant
        {
            Name = "Solo Tenant", Slug = "solo",
            CreatedAt = DateTime.UtcNow, OwnerUserId = user1Id
        };
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var owner = await ctx.Users.FirstAsync(u => u.Id == user1Id);
        owner.TenantId = tenant.Id;
        await ctx.SaveChangesAsync();

        var check = await svc.CanAnonymizeAsync(user1Id);
        check.CanProceed.Should().BeTrue();
    }

    [Fact]
    public async Task AnonymizeAsync_ValidUser_SetsIsActiveFalse_ChangesEmail_ClearsProfile()
    {
        var (svc, ctx, _) = Setup();
        var user1Id = await SeedUserWithProfileAsync(ctx, "1");

        var result = await svc.AnonymizeAsync(user1Id, actingAdminUserId: 99);

        result.Success.Should().BeTrue();

        var user = await ctx.Users.Include(u => u.Profile).FirstAsync(u => u.Id == user1Id);
        user.IsActive.Should().BeFalse();
        user.Email.Should().StartWith($"deleted-{user1Id}-").And.EndWith("@anonymized.local");
        user.UserName.Should().Be(user.Email);
        user.NormalizedEmail.Should().Be(user.Email!.ToUpperInvariant());
        user.PasswordHash.Should().BeNull();
        user.LockoutEnd.Should().Be(DateTimeOffset.MaxValue);
        user.EmailConfirmed.Should().BeFalse();

        user.Profile!.DisplayName.Should().Be("Silinmiş Kullanıcı");
        user.Profile.Bio.Should().BeNull();
        user.Profile.City.Should().BeNull();
        user.Profile.AvatarUrl.Should().BeNull();
        user.Profile.PublicSlug.Should().BeNull();
        user.Profile.IsPublicProfile.Should().BeFalse();
    }

    [Fact]
    public async Task AnonymizeAsync_DeletesAvatarFile()
    {
        var (svc, ctx, storage) = Setup();
        var user1Id = await SeedUserWithProfileAsync(ctx, "1");

        await svc.AnonymizeAsync(user1Id, actingAdminUserId: 99);

        storage.DeletedPaths.Should().Contain($"uploads/avatars/{user1Id}/avatar.webp");
    }

    [Fact]
    public async Task AnonymizeAsync_DeletesUserConnections_BothDirections()
    {
        var (svc, ctx, _) = Setup();
        var user1Id = await SeedUserWithProfileAsync(ctx, "1");
        var user2Id = await SeedUserWithProfileAsync(ctx, "2");
        var user3Id = await SeedUserWithProfileAsync(ctx, "3");

        ctx.UserConnections.AddRange(
            new UserConnection { RequesterId = user1Id, TargetId = user2Id,
                Status = UserConnectionStatus.Accepted, CreatedAt = DateTime.UtcNow },
            new UserConnection { RequesterId = user3Id, TargetId = user1Id,
                Status = UserConnectionStatus.Pending, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        await svc.AnonymizeAsync(user1Id, actingAdminUserId: 99);

        (await ctx.UserConnections.AnyAsync(
            c => c.RequesterId == user1Id || c.TargetId == user1Id)).Should().BeFalse();
    }

    [Fact]
    public async Task AnonymizeAsync_DeletesUserBlocks_BothDirections()
    {
        var (svc, ctx, _) = Setup();
        var user1Id = await SeedUserWithProfileAsync(ctx, "1");
        var user2Id = await SeedUserWithProfileAsync(ctx, "2");

        ctx.UserBlocks.AddRange(
            new UserBlock { BlockerId = user1Id, BlockedId = user2Id, CreatedAt = DateTime.UtcNow },
            new UserBlock { BlockerId = user2Id, BlockedId = user1Id, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        await svc.AnonymizeAsync(user1Id, actingAdminUserId: 99);

        (await ctx.UserBlocks.AnyAsync(
            b => b.BlockerId == user1Id || b.BlockedId == user1Id)).Should().BeFalse();
    }

    [Fact]
    public async Task AnonymizeAsync_UnpublishesUserPosts_DecrementsTagUsage()
    {
        var (svc, ctx, _) = Setup();
        var user1Id = await SeedUserWithProfileAsync(ctx, "1");

        var category = new PostCategory
        {
            Name = "Genel", Slug = "genel",
            DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(category);
        var tag = new PostTag { Name = "Vergi", Slug = "vergi", UsageCount = 1, CreatedAt = DateTime.UtcNow };
        ctx.PostTags.Add(tag);
        await ctx.SaveChangesAsync();

        var post = new UserPost
        {
            UserId = user1Id, CategoryId = category.Id, Title = "T", Slug = "t",
            Body = "<p>x</p>", IsPublished = true, PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        ctx.UserPosts.Add(post);
        await ctx.SaveChangesAsync();

        ctx.PostTagLinks.Add(new PostTagLink
        {
            PostId = post.Id, TagId = tag.Id, CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        await svc.AnonymizeAsync(user1Id, actingAdminUserId: 99);

        var updatedPost = await ctx.UserPosts.FirstAsync(p => p.Id == post.Id);
        updatedPost.IsPublished.Should().BeFalse();

        var updatedTag = await ctx.PostTags.FirstAsync(t => t.Id == tag.Id);
        updatedTag.UsageCount.Should().Be(0);
    }

    [Fact]
    public async Task AnonymizeAsync_PreservesPostCommentsAndReports()
    {
        var (svc, ctx, _) = Setup();
        var user1Id = await SeedUserWithProfileAsync(ctx, "1");
        var user2Id = await SeedUserWithProfileAsync(ctx, "2");

        var category = new PostCategory
        {
            Name = "Genel", Slug = "genel",
            DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(category);
        await ctx.SaveChangesAsync();

        var post = new UserPost
        {
            UserId = user2Id, CategoryId = category.Id, Title = "T", Slug = "t",
            Body = "<p>x</p>", IsPublished = true, PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        ctx.UserPosts.Add(post);
        await ctx.SaveChangesAsync();

        var comment = new PostComment
        {
            PostId = post.Id, UserId = user1Id, Body = "yorum",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, IsEdited = false
        };
        ctx.PostComments.Add(comment);
        await ctx.SaveChangesAsync();

        await svc.AnonymizeAsync(user1Id, actingAdminUserId: 99);

        // Yorum SİLİNMEZ — sadece yazar anonim render edilir (extension ile)
        (await ctx.PostComments.AnyAsync(c => c.Id == comment.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task AnonymizeAsync_AlreadyAnonymized_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var user = MakeUser("1", isActive: false);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var result = await svc.AnonymizeAsync(user.Id, actingAdminUserId: 99);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("zaten");
    }

    [Fact]
    public async Task AnonymizeAsync_TenantSoleOwnerWithOtherMembers_Blocks()
    {
        var (svc, ctx, _) = Setup();
        var user1Id = await SeedUserWithProfileAsync(ctx, "1");
        var user2Id = await SeedUserWithProfileAsync(ctx, "2");

        // Circular FK staging: user'lar seed edildi; Tenant'ı ekle, sonra user.TenantId güncelle.
        var tenant = new Tenant
        {
            Name = "Tenant", Slug = "tenant",
            CreatedAt = DateTime.UtcNow, OwnerUserId = user1Id
        };
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var owner = await ctx.Users.FirstAsync(u => u.Id == user1Id);
        var member = await ctx.Users.FirstAsync(u => u.Id == user2Id);
        owner.TenantId = tenant.Id;
        member.TenantId = tenant.Id;
        await ctx.SaveChangesAsync();

        var result = await svc.AnonymizeAsync(user1Id, actingAdminUserId: 99);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("sahibisiniz");
    }

    private sealed class FakeMediaStorage : IMediaStorage
    {
        public List<string> DeletedPaths { get; } = new();

        public Task<string> StoreAsync(Stream content, string subdirectory,
            string fileName, CancellationToken ct = default)
            => Task.FromResult($"{subdirectory.TrimEnd('/')}/{fileName}");

        public Task DeleteAsync(string relativePath, CancellationToken ct = default)
        {
            DeletedPaths.Add(relativePath);
            return Task.CompletedTask;
        }

        public string GetPublicUrl(string relativePath) => "/" + relativePath;
    }
}
