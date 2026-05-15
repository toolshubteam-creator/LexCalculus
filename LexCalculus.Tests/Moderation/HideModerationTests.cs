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
// Adım 5.8 — SQL Server LocalDB servis testi (SqlServerTestBase, per-test DB).
public class HideModerationTests : SqlServerTestBase
{
    // Setup sonrası seed edilen kayıtların DB-generated Id'leri.
    private int _ownerId, _reporterId, _adminId, _categoryId;

    private (ContentReportService svc, ApplicationDbContext ctx, RecordingNotificationService notif)
        Setup()
    {
        var ctx = _db.Create();
        var notif = new RecordingNotificationService();
        var svc = new ContentReportService(ctx, notif, new NullActivityLogService(),
            new NoOpMessagingNotifier());

        var owner = MakeUser("owner@x.com");
        var reporter = MakeUser("reporter@x.com");
        var admin = MakeUser("admin@x.com");
        ctx.Users.AddRange(owner, reporter, admin);
        var category = new PostCategory
        {
            Name = "Genel", Slug = "genel",
            DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(category);
        ctx.SaveChanges();

        _ownerId = owner.Id;
        _reporterId = reporter.Id;
        _adminId = admin.Id;
        _categoryId = category.Id;
        return (svc, ctx, notif);
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
        int userId, bool isPublished = true, string slug = "test-post")
    {
        var now = DateTime.UtcNow;
        var p = new UserPost
        {
            UserId = userId, CategoryId = _categoryId, Title = "Test", Slug = slug,
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
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        var result = await svc.HideAsync(
            ContentReportTargetType.Post, postId,
            adminUserId: _adminId, reviewNote: "Spam");

        result.Success.Should().BeTrue();
        var post = await ctx.UserPosts.FirstAsync(p => p.Id == postId);
        post.IsModeratorHidden.Should().BeTrue();
    }

    [Fact]
    public async Task HideAsync_CommentValid_SetsIsModeratorHiddenTrue()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        var commentId = await SeedCommentAsync(ctx, postId, userId: _reporterId);

        var result = await svc.HideAsync(
            ContentReportTargetType.Comment, commentId,
            adminUserId: _adminId, reviewNote: null);

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
            adminUserId: _adminId, reviewNote: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bulunamadı");
    }

    [Fact]
    public async Task HideAsync_NotifiesReportersAndOwner()
    {
        var (svc, ctx, notif) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        await svc.CreateAsync(ContentReportTargetType.Post, postId, reporterId: _reporterId,
            ContentReportReason.Spam, null);

        var result = await svc.HideAsync(
            ContentReportTargetType.Post, postId,
            adminUserId: _adminId, reviewNote: "İhlal");

        result.Success.Should().BeTrue();
        notif.Created.Should().Contain(n =>
            n.Type == NotificationType.ContentHidden && n.UserId == _reporterId);
        notif.Created.Should().Contain(n =>
            n.Type == NotificationType.ContentHidden && n.UserId == _ownerId);
    }

    [Fact]
    public async Task HideAsync_PendingReports_BecomeActioned()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        await svc.CreateAsync(ContentReportTargetType.Post, postId, reporterId: _reporterId,
            ContentReportReason.Harassment, null);

        await svc.HideAsync(ContentReportTargetType.Post, postId,
            adminUserId: _adminId, reviewNote: "Yönetim aksiyonu");

        var report = await ctx.ContentReports.FirstAsync(r => r.TargetId == postId);
        report.Status.Should().Be(ContentReportStatus.Actioned);
        report.ReviewedByUserId.Should().Be(_adminId);
        report.ReviewNote.Should().Be("Yönetim aksiyonu");
    }

    [Fact]
    public async Task UnhideAsync_HiddenPost_SetsIsModeratorHiddenFalse()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        await svc.HideAsync(ContentReportTargetType.Post, postId,
            adminUserId: _adminId, reviewNote: null);

        var result = await svc.UnhideAsync(
            ContentReportTargetType.Post, postId, adminUserId: _adminId);

        result.Success.Should().BeTrue();
        var post = await ctx.UserPosts.FirstAsync(p => p.Id == postId);
        post.IsModeratorHidden.Should().BeFalse();
    }

    [Fact]
    public async Task UnhideAsync_NotHidden_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        var result = await svc.UnhideAsync(
            ContentReportTargetType.Post, postId, adminUserId: _adminId);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("zaten");
    }

    [Fact]
    public async Task UnhideAsync_NotifiesContentOwner()
    {
        var (svc, ctx, notif) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        await svc.HideAsync(ContentReportTargetType.Post, postId,
            adminUserId: _adminId, reviewNote: null);
        notif.Created.Clear(); // sadece unhide notification'ı say

        await svc.UnhideAsync(ContentReportTargetType.Post, postId, adminUserId: _adminId);

        notif.Created.Should().Contain(n =>
            n.Type == NotificationType.ContentRestored && n.UserId == _ownerId);
    }

    [Fact]
    public async Task GetHiddenContentAsync_ReturnsHiddenPostsAndComments()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId, slug: "hidden-post");
        var commentId = await SeedCommentAsync(ctx, postId, userId: _reporterId);
        await svc.HideAsync(ContentReportTargetType.Post, postId,
            adminUserId: _adminId, reviewNote: null);
        await svc.HideAsync(ContentReportTargetType.Comment, commentId,
            adminUserId: _adminId, reviewNote: null);

        var visibleId = await SeedPostAsync(ctx, userId: _ownerId, slug: "visible");

        var hidden = await svc.GetHiddenContentAsync();

        hidden.Should().HaveCount(2);
        hidden.Should().Contain(h => h.TargetType == ContentReportTargetType.Post && h.TargetId == postId);
        hidden.Should().Contain(h => h.TargetType == ContentReportTargetType.Comment && h.TargetId == commentId);
        hidden.Should().NotContain(h => h.TargetId == visibleId);
    }
}
