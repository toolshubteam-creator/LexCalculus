using FluentAssertions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Moderation;
using LexCalculus.Core.Messaging;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Moderation;

/// <summary>
/// Faz 5.3 — Hide moderation (UserPost.IsModeratorHidden,
/// PostComment.IsModeratorHidden) servis akışı testleri.
/// Charter Karar 11.
/// </summary>
public class HideModerationTests
{
    private static (ContentReportService svc, ApplicationDbContext ctx, RecordingNotificationService notif)
        Setup()
    {
        var ctx = TestDbContextFactory.Create();
        var notif = new RecordingNotificationService();
        var svc = new ContentReportService(ctx, notif, new NullActivityLogService(),
            new NoOpMessagingNotifier());

        ctx.Users.AddRange(
            MakeUser(1, "owner@x.com"),
            MakeUser(2, "reporter@x.com"),
            MakeUser(3, "admin@x.com"));
        ctx.PostCategories.Add(new PostCategory
        {
            Id = 1, Name = "Genel", Slug = "genel",
            DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        });
        ctx.SaveChanges();
        return (svc, ctx, notif);
    }

    private static ApplicationUser MakeUser(int id, string email) => new()
    {
        Id = id, UserName = email, NormalizedUserName = email.ToUpperInvariant(),
        Email = email, NormalizedEmail = email.ToUpperInvariant(),
        FullName = $"User {id}", CreatedAt = DateTime.UtcNow,
        IsActive = true, EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static async Task<int> SeedPostAsync(ApplicationDbContext ctx,
        int userId, bool isPublished = true, string slug = "test-post")
    {
        var now = DateTime.UtcNow;
        var p = new UserPost
        {
            UserId = userId, CategoryId = 1, Title = "Test", Slug = slug,
            Body = "<p>x</p>", IsPublished = isPublished,
            PublishedAt = isPublished ? now : null,
            CreatedAt = now, UpdatedAt = now
        };
        ctx.UserPosts.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<int> SeedCommentAsync(ApplicationDbContext ctx,
        int postId, int userId, string body = "yorum")
    {
        var now = DateTime.UtcNow;
        var c = new PostComment
        {
            PostId = postId, UserId = userId, Body = body,
            CreatedAt = now, UpdatedAt = now, IsEdited = false
        };
        ctx.PostComments.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
    }

    [Fact]
    public async Task HideAsync_PostValid_SetsIsModeratorHiddenTrue()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        var result = await svc.HideAsync(
            ContentReportTargetType.Post, postId,
            adminUserId: 3, reviewNote: "Spam");

        result.Success.Should().BeTrue();
        var post = await ctx.UserPosts.FirstAsync(p => p.Id == postId);
        post.IsModeratorHidden.Should().BeTrue();
    }

    [Fact]
    public async Task HideAsync_CommentValid_SetsIsModeratorHiddenTrue()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        var commentId = await SeedCommentAsync(ctx, postId, userId: 2);

        var result = await svc.HideAsync(
            ContentReportTargetType.Comment, commentId,
            adminUserId: 3, reviewNote: null);

        result.Success.Should().BeTrue();
        var comment = await ctx.PostComments.FirstAsync(c => c.Id == commentId);
        comment.IsModeratorHidden.Should().BeTrue();
    }

    [Fact]
    public async Task HideAsync_NonExistent_ReturnsError()
    {
        var (svc, _, _) = Setup();

        var result = await svc.HideAsync(
            ContentReportTargetType.Post, targetId: 99999,
            adminUserId: 3, reviewNote: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bulunamadı");
    }

    [Fact]
    public async Task HideAsync_NotifiesReportersAndOwner()
    {
        var (svc, ctx, notif) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        await svc.CreateAsync(ContentReportTargetType.Post, postId, reporterId: 2,
            ContentReportReason.Spam, null);

        var result = await svc.HideAsync(
            ContentReportTargetType.Post, postId,
            adminUserId: 3, reviewNote: "İhlal");

        result.Success.Should().BeTrue();
        notif.Created.Should().Contain(n =>
            n.Type == NotificationType.ContentHidden && n.UserId == 2);
        notif.Created.Should().Contain(n =>
            n.Type == NotificationType.ContentHidden && n.UserId == 1);
    }

    [Fact]
    public async Task HideAsync_PendingReports_BecomeActioned()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        await svc.CreateAsync(ContentReportTargetType.Post, postId, reporterId: 2,
            ContentReportReason.Harassment, null);

        await svc.HideAsync(ContentReportTargetType.Post, postId,
            adminUserId: 3, reviewNote: "Yönetim aksiyonu");

        var report = await ctx.ContentReports.FirstAsync(r => r.TargetId == postId);
        report.Status.Should().Be(ContentReportStatus.Actioned);
        report.ReviewedByUserId.Should().Be(3);
        report.ReviewNote.Should().Be("Yönetim aksiyonu");
    }

    [Fact]
    public async Task UnhideAsync_HiddenPost_SetsIsModeratorHiddenFalse()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        await svc.HideAsync(ContentReportTargetType.Post, postId,
            adminUserId: 3, reviewNote: null);

        var result = await svc.UnhideAsync(
            ContentReportTargetType.Post, postId, adminUserId: 3);

        result.Success.Should().BeTrue();
        var post = await ctx.UserPosts.FirstAsync(p => p.Id == postId);
        post.IsModeratorHidden.Should().BeFalse();
    }

    [Fact]
    public async Task UnhideAsync_NotHidden_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        var result = await svc.UnhideAsync(
            ContentReportTargetType.Post, postId, adminUserId: 3);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("zaten");
    }

    [Fact]
    public async Task UnhideAsync_NotifiesContentOwner()
    {
        var (svc, ctx, notif) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        await svc.HideAsync(ContentReportTargetType.Post, postId,
            adminUserId: 3, reviewNote: null);
        notif.Created.Clear(); // sadece unhide notification'ı say

        await svc.UnhideAsync(ContentReportTargetType.Post, postId, adminUserId: 3);

        notif.Created.Should().Contain(n =>
            n.Type == NotificationType.ContentRestored && n.UserId == 1);
    }

    [Fact]
    public async Task GetHiddenContentAsync_ReturnsHiddenPostsAndComments()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1, slug: "hidden-post");
        var commentId = await SeedCommentAsync(ctx, postId, userId: 2);
        await svc.HideAsync(ContentReportTargetType.Post, postId,
            adminUserId: 3, reviewNote: null);
        await svc.HideAsync(ContentReportTargetType.Comment, commentId,
            adminUserId: 3, reviewNote: null);

        var visibleId = await SeedPostAsync(ctx, userId: 1, slug: "visible");

        var hidden = await svc.GetHiddenContentAsync();

        hidden.Should().HaveCount(2);
        hidden.Should().Contain(h => h.TargetType == ContentReportTargetType.Post && h.TargetId == postId);
        hidden.Should().Contain(h => h.TargetType == ContentReportTargetType.Comment && h.TargetId == commentId);
        hidden.Should().NotContain(h => h.TargetId == visibleId);
    }
}
