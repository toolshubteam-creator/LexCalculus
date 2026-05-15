using FluentAssertions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Content;

public class PostLikeServiceTests : SqlServerTestBase
{
    // Setup sonrası seed edilen kayıtların DB-generated Id'leri.
    private int _authorId, _likerId, _otherId, _categoryId;

    private (PostLikeService svc, ApplicationDbContext ctx) Setup()
    {
        var ctx = _db.Create();
        var svc = new PostLikeService(ctx, new NullActivityLogService());

        var author = MakeUser("author@x.com");
        var liker = MakeUser("liker@x.com");
        var other = MakeUser("other@x.com");
        ctx.Users.AddRange(author, liker, other);
        var category = new PostCategory
        {
            Name = "Genel", Slug = "genel",
            DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(category);
        ctx.SaveChanges();

        _authorId = author.Id;
        _likerId = liker.Id;
        _otherId = other.Id;
        _categoryId = category.Id;
        return (svc, ctx);
    }

    private static ApplicationUser MakeUser(string email) => new()
    {
        UserName = email, NormalizedUserName = email.ToUpperInvariant(),
        Email = email, NormalizedEmail = email.ToUpperInvariant(),
        FullName = $"User {email}", CreatedAt = DateTime.UtcNow,
        IsActive = true, EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private async Task<int> SeedPostAsync(ApplicationDbContext ctx,
        bool isPublished = true)
    {
        var now = DateTime.UtcNow;
        var p = new UserPost
        {
            UserId = _authorId, CategoryId = _categoryId, Title = "T", Slug = "t",
            Body = "<p>x</p>", IsPublished = isPublished,
            PublishedAt = isPublished ? now : null,
            CreatedAt = now, UpdatedAt = now
        };
        ctx.UserPosts.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task ToggleAsync_NotLiked_AddsLikeAndReturnsCount()
    {
        var (svc, ctx) = Setup();
        var postId = await SeedPostAsync(ctx);

        var result = await svc.ToggleAsync(postId, userId: _likerId);

        result.Success.Should().BeTrue();
        result.IsLiked.Should().BeTrue();
        result.LikeCount.Should().Be(1);
        (await ctx.PostLikes.AnyAsync(l => l.PostId == postId && l.UserId == _likerId))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ToggleAsync_AlreadyLiked_RemovesLike()
    {
        var (svc, ctx) = Setup();
        var postId = await SeedPostAsync(ctx);
        await svc.ToggleAsync(postId, userId: _likerId);

        var second = await svc.ToggleAsync(postId, userId: _likerId);

        second.Success.Should().BeTrue();
        second.IsLiked.Should().BeFalse();
        second.LikeCount.Should().Be(0);
    }

    [Fact]
    public async Task ToggleAsync_DraftPost_ReturnsError()
    {
        var (svc, ctx) = Setup();
        var postId = await SeedPostAsync(ctx, isPublished: false);

        var result = await svc.ToggleAsync(postId, userId: _likerId);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Yayında olmayan");
    }

    [Fact]
    public async Task ToggleAsync_NonExistingPost_ReturnsError()
    {
        var (svc, _) = Setup();
        var result = await svc.ToggleAsync(postId: 9999, userId: _likerId);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bulunamadı");
    }

    [Fact]
    public async Task IsLikedByAsync_ReturnsCorrectStatus()
    {
        var (svc, ctx) = Setup();
        var postId = await SeedPostAsync(ctx);

        (await svc.IsLikedByAsync(postId, userId: _likerId)).Should().BeFalse();

        await svc.ToggleAsync(postId, userId: _likerId);
        (await svc.IsLikedByAsync(postId, userId: _likerId)).Should().BeTrue();
        (await svc.IsLikedByAsync(postId, userId: _otherId)).Should().BeFalse();
    }

    [Fact]
    public async Task GetCountForPostAsync_ReturnsAccurateCount()
    {
        var (svc, ctx) = Setup();
        var postId = await SeedPostAsync(ctx);

        await svc.ToggleAsync(postId, userId: _likerId);
        await svc.ToggleAsync(postId, userId: _otherId);

        (await svc.GetCountForPostAsync(postId)).Should().Be(2);
    }

    [Fact]
    public async Task ToggleAsync_DifferentUsers_AccumulateLikes()
    {
        var (svc, ctx) = Setup();
        var postId = await SeedPostAsync(ctx);

        var r1 = await svc.ToggleAsync(postId, userId: _likerId);
        var r2 = await svc.ToggleAsync(postId, userId: _otherId);

        r1.LikeCount.Should().Be(1);
        r2.LikeCount.Should().Be(2);
    }
}
