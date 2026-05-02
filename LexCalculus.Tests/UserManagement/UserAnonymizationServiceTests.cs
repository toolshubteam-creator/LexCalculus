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

public class UserAnonymizationServiceTests
{
    private static (UserAnonymizationService svc, ApplicationDbContext ctx, FakeMediaStorage storage)
        Setup()
    {
        var ctx = TestDbContextFactory.Create();
        var storage = new FakeMediaStorage();
        var svc = new UserAnonymizationService(ctx, storage, new NullActivityLogService());
        return (svc, ctx, storage);
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
        SecurityStamp = Guid.NewGuid().ToString(),
        PasswordHash = "PWDHASH"
    };

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
        BaroNo = "12345"
    };

    private static async Task<int> SeedUserWithProfileAsync(ApplicationDbContext ctx, int id)
    {
        var user = MakeUser(id);
        var profile = MakeProfile(id);
        ctx.Users.Add(user);
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();
        return id;
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
        ctx.Users.Add(MakeUser(1, isActive: false));
        await ctx.SaveChangesAsync();

        var check = await svc.CanAnonymizeAsync(1);
        check.CanProceed.Should().BeFalse();
        check.BlockerMessage.Should().Contain("zaten");
    }

    [Fact]
    public async Task CanAnonymizeAsync_ValidUser_ReturnsCorrectCounts()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserWithProfileAsync(ctx, 1);
        await SeedUserWithProfileAsync(ctx, 2);

        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = 1, TargetId = 2,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var check = await svc.CanAnonymizeAsync(1);
        check.CanProceed.Should().BeTrue();
        check.ConnectionCount.Should().Be(1);
        check.PostCount.Should().Be(0);
    }

    [Fact]
    public async Task CanAnonymizeAsync_TenantOwnerWithOtherActiveMembers_Blocks()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserWithProfileAsync(ctx, 1);
        await SeedUserWithProfileAsync(ctx, 2);

        var tenant = new Tenant
        {
            Id = 100, Name = "Test Tenant", Slug = "test-tenant",
            CreatedAt = DateTime.UtcNow, OwnerUserId = 1
        };
        ctx.Tenants.Add(tenant);

        var owner = await ctx.Users.FirstAsync(u => u.Id == 1);
        var member = await ctx.Users.FirstAsync(u => u.Id == 2);
        owner.TenantId = 100;
        member.TenantId = 100;
        await ctx.SaveChangesAsync();

        var check = await svc.CanAnonymizeAsync(1);
        check.CanProceed.Should().BeFalse();
        check.BlockerMessage.Should().Contain("Test Tenant");
    }

    [Fact]
    public async Task CanAnonymizeAsync_TenantOwnerWithNoOtherMembers_AllowsAnonymize()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserWithProfileAsync(ctx, 1);

        var tenant = new Tenant
        {
            Id = 100, Name = "Solo Tenant", Slug = "solo",
            CreatedAt = DateTime.UtcNow, OwnerUserId = 1
        };
        ctx.Tenants.Add(tenant);
        var owner = await ctx.Users.FirstAsync(u => u.Id == 1);
        owner.TenantId = 100;
        await ctx.SaveChangesAsync();

        var check = await svc.CanAnonymizeAsync(1);
        check.CanProceed.Should().BeTrue();
    }

    [Fact]
    public async Task AnonymizeAsync_ValidUser_SetsIsActiveFalse_ChangesEmail_ClearsProfile()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserWithProfileAsync(ctx, 1);

        var result = await svc.AnonymizeAsync(1, actingAdminUserId: 99);

        result.Success.Should().BeTrue();

        var user = await ctx.Users.Include(u => u.Profile).FirstAsync(u => u.Id == 1);
        user.IsActive.Should().BeFalse();
        user.Email.Should().StartWith("deleted-1-").And.EndWith("@anonymized.local");
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
        await SeedUserWithProfileAsync(ctx, 1);

        await svc.AnonymizeAsync(1, actingAdminUserId: 99);

        storage.DeletedPaths.Should().Contain("uploads/avatars/1/avatar.webp");
    }

    [Fact]
    public async Task AnonymizeAsync_DeletesUserConnections_BothDirections()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserWithProfileAsync(ctx, 1);
        await SeedUserWithProfileAsync(ctx, 2);
        await SeedUserWithProfileAsync(ctx, 3);

        ctx.UserConnections.AddRange(
            new UserConnection { RequesterId = 1, TargetId = 2,
                Status = UserConnectionStatus.Accepted, CreatedAt = DateTime.UtcNow },
            new UserConnection { RequesterId = 3, TargetId = 1,
                Status = UserConnectionStatus.Pending, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        await svc.AnonymizeAsync(1, actingAdminUserId: 99);

        (await ctx.UserConnections.AnyAsync(
            c => c.RequesterId == 1 || c.TargetId == 1)).Should().BeFalse();
    }

    [Fact]
    public async Task AnonymizeAsync_DeletesUserBlocks_BothDirections()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserWithProfileAsync(ctx, 1);
        await SeedUserWithProfileAsync(ctx, 2);

        ctx.UserBlocks.AddRange(
            new UserBlock { BlockerId = 1, BlockedId = 2, CreatedAt = DateTime.UtcNow },
            new UserBlock { BlockerId = 2, BlockedId = 1, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        await svc.AnonymizeAsync(1, actingAdminUserId: 99);

        (await ctx.UserBlocks.AnyAsync(
            b => b.BlockerId == 1 || b.BlockedId == 1)).Should().BeFalse();
    }

    [Fact]
    public async Task AnonymizeAsync_UnpublishesUserPosts_DecrementsTagUsage()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserWithProfileAsync(ctx, 1);

        ctx.PostCategories.Add(new PostCategory
        {
            Id = 1, Name = "Genel", Slug = "genel",
            DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        });
        var tag = new PostTag { Id = 1, Name = "Vergi", Slug = "vergi", UsageCount = 1, CreatedAt = DateTime.UtcNow };
        ctx.PostTags.Add(tag);
        var post = new UserPost
        {
            Id = 1, UserId = 1, CategoryId = 1, Title = "T", Slug = "t",
            Body = "<p>x</p>", IsPublished = true, PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        ctx.UserPosts.Add(post);
        await ctx.SaveChangesAsync();

        ctx.PostTagLinks.Add(new PostTagLink
        {
            PostId = 1, TagId = 1, CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        await svc.AnonymizeAsync(1, actingAdminUserId: 99);

        var updatedPost = await ctx.UserPosts.FirstAsync(p => p.Id == 1);
        updatedPost.IsPublished.Should().BeFalse();

        var updatedTag = await ctx.PostTags.FirstAsync(t => t.Id == 1);
        updatedTag.UsageCount.Should().Be(0);
    }

    [Fact]
    public async Task AnonymizeAsync_PreservesPostCommentsAndReports()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserWithProfileAsync(ctx, 1);
        await SeedUserWithProfileAsync(ctx, 2);

        ctx.PostCategories.Add(new PostCategory
        {
            Id = 1, Name = "Genel", Slug = "genel",
            DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        });
        var post = new UserPost
        {
            Id = 1, UserId = 2, CategoryId = 1, Title = "T", Slug = "t",
            Body = "<p>x</p>", IsPublished = true, PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        ctx.UserPosts.Add(post);
        await ctx.SaveChangesAsync();

        ctx.PostComments.Add(new PostComment
        {
            Id = 1, PostId = 1, UserId = 1, Body = "yorum",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, IsEdited = false
        });
        await ctx.SaveChangesAsync();

        await svc.AnonymizeAsync(1, actingAdminUserId: 99);

        // Yorum SİLİNMEZ — sadece yazar anonim render edilir (extension ile)
        (await ctx.PostComments.AnyAsync(c => c.Id == 1)).Should().BeTrue();
    }

    [Fact]
    public async Task AnonymizeAsync_AlreadyAnonymized_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        ctx.Users.Add(MakeUser(1, isActive: false));
        await ctx.SaveChangesAsync();

        var result = await svc.AnonymizeAsync(1, actingAdminUserId: 99);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("zaten");
    }

    [Fact]
    public async Task AnonymizeAsync_TenantSoleOwnerWithOtherMembers_Blocks()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserWithProfileAsync(ctx, 1);
        await SeedUserWithProfileAsync(ctx, 2);

        ctx.Tenants.Add(new Tenant
        {
            Id = 100, Name = "Tenant", Slug = "tenant",
            CreatedAt = DateTime.UtcNow, OwnerUserId = 1
        });
        var owner = await ctx.Users.FirstAsync(u => u.Id == 1);
        var member = await ctx.Users.FirstAsync(u => u.Id == 2);
        owner.TenantId = 100;
        member.TenantId = 100;
        await ctx.SaveChangesAsync();

        var result = await svc.AnonymizeAsync(1, actingAdminUserId: 99);
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
