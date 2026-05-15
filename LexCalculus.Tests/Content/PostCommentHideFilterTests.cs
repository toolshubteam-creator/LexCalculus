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
public class PostCommentHideFilterTests : SqlServerTestBase
{
    // Setup sonrası seed edilen kayıtların DB-generated Id'leri.
    private int _ownerId, _commenterId, _categoryId, _postId;

    private (PostCommentService svc, ApplicationDbContext ctx) Setup()
    {
        var ctx = _db.Create();
        var svc = new PostCommentService(
            ctx, new CommentSanitizer(),
            new NullNotificationService(),
            new NullActivityLogService());

        var owner = MakeUser("owner@x.com");
        var commenter = MakeUser("commenter@x.com");
        ctx.Users.AddRange(owner, commenter);
        var category = new PostCategory
        {
            Name = "Genel", Slug = "genel",
            DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(category);
        ctx.SaveChanges();

        var now = DateTime.UtcNow;
        var post = new UserPost
        {
            UserId = owner.Id, CategoryId = category.Id,
            Title = "T", Slug = "t", Body = "<p>x</p>",
            IsPublished = true, PublishedAt = now,
            CreatedAt = now, UpdatedAt = now
        };
        ctx.UserPosts.Add(post);
        ctx.SaveChanges();

        _ownerId = owner.Id;
        _commenterId = commenter.Id;
        _categoryId = category.Id;
        _postId = post.Id;
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
        var visibleId = await SeedCommentAsync(ctx, _postId, _commenterId, isHidden: false, "görünür");
        var hiddenId = await SeedCommentAsync(ctx, _postId, _commenterId, isHidden: true, "gizli");

        var list = await svc.GetByPostIdAsync(_postId);

        list.Should().HaveCount(1);
        list[0].Id.Should().Be(visibleId);
        list.Should().NotContain(c => c.Id == hiddenId);
    }

    [Fact]
    public async Task GetByPostIdAsync_IncludeHiddenTrue_ReturnsAll()
    {
        var (svc, ctx) = Setup();
        await SeedCommentAsync(ctx, _postId, _commenterId, isHidden: false);
        await SeedCommentAsync(ctx, _postId, _commenterId, isHidden: true);

        var list = await svc.GetByPostIdAsync(_postId, includeHidden: true);

        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetCountForPostAsync_ExcludesHidden()
    {
        var (svc, ctx) = Setup();
        await SeedCommentAsync(ctx, _postId, _commenterId, isHidden: false);
        await SeedCommentAsync(ctx, _postId, _commenterId, isHidden: false);
        await SeedCommentAsync(ctx, _postId, _commenterId, isHidden: true);

        var count = await svc.GetCountForPostAsync(_postId);

        count.Should().Be(2);
    }
}
