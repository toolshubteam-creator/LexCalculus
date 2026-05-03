using FluentAssertions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Xunit;

namespace LexCalculus.Tests.Content;

/// <summary>
/// Faz 5.3 — PostCommentService.GetByPostIdAsync ve GetCountForPostAsync
/// IsModeratorHidden filtreleme testleri.
/// </summary>
public class PostCommentHideFilterTests
{
    private static (PostCommentService svc, ApplicationDbContext ctx) Setup()
    {
        var ctx = TestDbContextFactory.Create();
        var svc = new PostCommentService(
            ctx, new CommentSanitizer(),
            new NullNotificationService(),
            new NullActivityLogService());

        ctx.Users.AddRange(
            MakeUser(1, "owner@x.com"),
            MakeUser(2, "commenter@x.com"));
        ctx.PostCategories.Add(new PostCategory
        {
            Id = 1, Name = "Genel", Slug = "genel",
            DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        });
        var now = DateTime.UtcNow;
        ctx.UserPosts.Add(new UserPost
        {
            Id = 100, UserId = 1, CategoryId = 1,
            Title = "T", Slug = "t", Body = "<p>x</p>",
            IsPublished = true, PublishedAt = now,
            CreatedAt = now, UpdatedAt = now
        });
        ctx.SaveChanges();
        return (svc, ctx);
    }

    private static ApplicationUser MakeUser(int id, string email) => new()
    {
        Id = id, UserName = email, NormalizedUserName = email.ToUpperInvariant(),
        Email = email, NormalizedEmail = email.ToUpperInvariant(),
        FullName = $"User {id}", CreatedAt = DateTime.UtcNow,
        IsActive = true, EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static async Task<int> SeedCommentAsync(
        ApplicationDbContext ctx, int postId, int userId, bool isHidden, string body = "yorum")
    {
        var now = DateTime.UtcNow;
        var c = new PostComment
        {
            PostId = postId, UserId = userId, Body = body,
            CreatedAt = now, UpdatedAt = now, IsEdited = false,
            IsModeratorHidden = isHidden
        };
        ctx.PostComments.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
    }

    [Fact]
    public async Task GetByPostIdAsync_DefaultExcludesHidden()
    {
        var (svc, ctx) = Setup();
        var visibleId = await SeedCommentAsync(ctx, 100, 2, isHidden: false, "görünür");
        var hiddenId = await SeedCommentAsync(ctx, 100, 2, isHidden: true, "gizli");

        var list = await svc.GetByPostIdAsync(100);

        list.Should().HaveCount(1);
        list[0].Id.Should().Be(visibleId);
        list.Should().NotContain(c => c.Id == hiddenId);
    }

    [Fact]
    public async Task GetByPostIdAsync_IncludeHiddenTrue_ReturnsAll()
    {
        var (svc, ctx) = Setup();
        await SeedCommentAsync(ctx, 100, 2, isHidden: false);
        await SeedCommentAsync(ctx, 100, 2, isHidden: true);

        var list = await svc.GetByPostIdAsync(100, includeHidden: true);

        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetCountForPostAsync_ExcludesHidden()
    {
        var (svc, ctx) = Setup();
        await SeedCommentAsync(ctx, 100, 2, isHidden: false);
        await SeedCommentAsync(ctx, 100, 2, isHidden: false);
        await SeedCommentAsync(ctx, 100, 2, isHidden: true);

        var count = await svc.GetCountForPostAsync(100);

        count.Should().Be(2);
    }
}
