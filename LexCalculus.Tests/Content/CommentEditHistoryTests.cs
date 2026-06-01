using FluentAssertions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Content;

/// <summary>
/// Faz 6.8 (#21) — yorum düzenleme geçmişi (PostCommentRevision). İlk
/// düzenlemede orijinal saklanır, sonraki düzenlemeler revision'a dokunmaz,
/// içerik değişmezse revision yaratılmaz, yorum silinince cascade ile silinir.
/// </summary>
public class CommentEditHistoryTests : SqlServerTestBase
{
    private int _ownerId, _commenterId, _categoryId;

    private (PostCommentService svc, ApplicationDbContext ctx) Setup()
    {
        var ctx = _db.Create();
        var svc = new PostCommentService(
            ctx, new CommentSanitizer(), new RecordingNotificationService(),
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

        _ownerId = owner.Id;
        _commenterId = commenter.Id;
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

    private async Task<int> SeedPostAsync(ApplicationDbContext ctx, int userId)
    {
        var now = DateTime.UtcNow;
        var p = new UserPost
        {
            UserId = userId, CategoryId = _categoryId, Title = "Test", Slug = "test-post",
            Body = "<p>x</p>", IsPublished = true, PublishedAt = now,
            CreatedAt = now, UpdatedAt = now
        };
        ctx.UserPosts.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task UpdateAsync_FirstEdit_StoresOriginalBodyAndCreatedAt()
    {
        var (svc, ctx) = Setup();
        var postId = await SeedPostAsync(ctx, _ownerId);
        var created = await svc.CreateAsync(postId, _commenterId, "ilk içerik");
        var originalBody = created.Comment!.Body;
        var originalCreatedAt = created.Comment.CreatedAt;

        await svc.UpdateAsync(created.Comment.Id, _commenterId, "düzenlenmiş içerik");

        var revision = await svc.GetRevisionAsync(created.Comment.Id);
        revision.Should().NotBeNull();
        revision!.OriginalBody.Should().Be(originalBody);
        revision.OriginalCreatedAt.Should().BeCloseTo(originalCreatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpdateAsync_SecondEdit_DoesNotOverwriteOriginal()
    {
        var (svc, ctx) = Setup();
        var postId = await SeedPostAsync(ctx, _ownerId);
        var created = await svc.CreateAsync(postId, _commenterId, "orijinal");
        var originalBody = created.Comment!.Body;

        await svc.UpdateAsync(created.Comment.Id, _commenterId, "ilk düzenleme");
        await svc.UpdateAsync(created.Comment.Id, _commenterId, "ikinci düzenleme");

        // Tek revision olmalı ve hep İLK orijinali yansıtmalı.
        var revisionCount = await ctx.PostCommentRevisions
            .CountAsync(r => r.CommentId == created.Comment.Id);
        revisionCount.Should().Be(1);

        var revision = await svc.GetRevisionAsync(created.Comment.Id);
        revision!.OriginalBody.Should().Be(originalBody);
    }

    [Fact]
    public async Task UpdateAsync_NoContentChange_DoesNotCreateRevision()
    {
        var (svc, ctx) = Setup();
        var postId = await SeedPostAsync(ctx, _ownerId);
        var created = await svc.CreateAsync(postId, _commenterId, "değişmeyen");

        var result = await svc.UpdateAsync(created.Comment!.Id, _commenterId, "değişmeyen");

        result.Success.Should().BeTrue();
        result.Comment!.IsEdited.Should().BeFalse("içerik değişmedi → düzenlendi sayılmaz");
        (await svc.GetRevisionAsync(created.Comment.Id)).Should().BeNull();
    }

    [Fact]
    public async Task GetRevisionAsync_NeverEdited_ReturnsNull()
    {
        var (svc, ctx) = Setup();
        var postId = await SeedPostAsync(ctx, _ownerId);
        var created = await svc.CreateAsync(postId, _commenterId, "hiç düzenlenmedi");

        (await svc.GetRevisionAsync(created.Comment!.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteComment_CascadeDeletesRevision()
    {
        var (svc, ctx) = Setup();
        var postId = await SeedPostAsync(ctx, _ownerId);
        var created = await svc.CreateAsync(postId, _commenterId, "silinecek");
        await svc.UpdateAsync(created.Comment!.Id, _commenterId, "düzenlendi");
        (await svc.GetRevisionAsync(created.Comment.Id)).Should().NotBeNull();

        var del = await svc.DeleteAsync(created.Comment.Id, _commenterId, isAdmin: false);
        del.Success.Should().BeTrue();

        (await ctx.PostCommentRevisions.AnyAsync(r => r.CommentId == created.Comment.Id))
            .Should().BeFalse("yorum silinince revision cascade ile silinmeli");
    }
}
