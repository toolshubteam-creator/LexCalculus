using FluentAssertions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Moderation;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Moderation;

public class ContentReportServiceTests
{
    private static (ContentReportService svc, ApplicationDbContext ctx, RecordingNotificationService notif)
        Setup()
    {
        var ctx = TestDbContextFactory.Create();
        var notif = new RecordingNotificationService();
        var svc = new ContentReportService(ctx, notif, new NullActivityLogService());

        ctx.Users.AddRange(
            MakeUser(1, "owner@x.com"),
            MakeUser(2, "reporter@x.com"),
            MakeUser(3, "stranger@x.com"),
            MakeUser(4, "admin@x.com"));
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
    public async Task CreateAsync_Valid_Persists()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: 2,
            ContentReportReason.Spam, note: null);

        result.Success.Should().BeTrue();
        result.Report.Should().NotBeNull();
        result.Report!.Status.Should().Be(ContentReportStatus.Pending);

        (await ctx.ContentReports.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_SelfReport_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: 1,
            ContentReportReason.Spam, note: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kendi");
    }

    [Fact]
    public async Task CreateAsync_DraftPost_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1, isPublished: false);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: 2,
            ContentReportReason.Spam, note: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Yayında");
    }

    [Fact]
    public async Task CreateAsync_NonExistentTarget_ReturnsError()
    {
        var (svc, _, _) = Setup();

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, targetId: 9999, reporterId: 2,
            ContentReportReason.Spam, note: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bulunamadı");
    }

    [Fact]
    public async Task CreateAsync_DuplicateReport_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        var first = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: 2,
            ContentReportReason.Spam, note: null);
        first.Success.Should().BeTrue();

        var second = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: 2,
            ContentReportReason.Harassment, note: null);

        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("zaten");
        (await ctx.ContentReports.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_OtherReason_RequiresLongEnoughNote()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: 2,
            ContentReportReason.Other, note: "kısa");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Diğer");
    }

    [Fact]
    public async Task CreateAsync_OtherReason_EmptyNote_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: 2,
            ContentReportReason.Other, note: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Diğer");
    }

    [Fact]
    public async Task CreateAsync_OtherReason_LongEnoughNote_Succeeds()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: 2,
            ContentReportReason.Other, note: "Bu yeterince uzun bir açıklama metni.");

        result.Success.Should().BeTrue();
        result.Report!.Note.Should().Contain("yeterince");
    }

    [Fact]
    public async Task HasReportedAsync_AfterCreate_ReturnsTrue()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        (await svc.HasReportedAsync(ContentReportTargetType.Post, postId, userId: 2))
            .Should().BeFalse();

        await svc.CreateAsync(ContentReportTargetType.Post, postId,
            reporterId: 2, ContentReportReason.Spam, null);

        (await svc.HasReportedAsync(ContentReportTargetType.Post, postId, userId: 2))
            .Should().BeTrue();
        (await svc.HasReportedAsync(ContentReportTargetType.Post, postId, userId: 3))
            .Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ForComment_Persists()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        var commentId = await SeedCommentAsync(ctx, postId, userId: 3);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Comment, commentId, reporterId: 2,
            ContentReportReason.Harassment, note: null);

        result.Success.Should().BeTrue();
        result.Report!.TargetType.Should().Be(ContentReportTargetType.Comment);
        result.Report.TargetId.Should().Be(commentId);
    }

    [Fact]
    public async Task CreateAsync_OwnComment_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        var commentId = await SeedCommentAsync(ctx, postId, userId: 2);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Comment, commentId, reporterId: 2,
            ContentReportReason.Spam, note: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kendi");
    }

    [Fact]
    public async Task GetPendingGroupedAsync_GroupsByTarget_OrdersByLatest()
    {
        var (svc, ctx, _) = Setup();
        var postId1 = await SeedPostAsync(ctx, userId: 1, slug: "p1");
        var postId2 = await SeedPostAsync(ctx, userId: 1, slug: "p2");

        await svc.CreateAsync(ContentReportTargetType.Post, postId1, reporterId: 2,
            ContentReportReason.Spam, null);
        await Task.Delay(15);
        await svc.CreateAsync(ContentReportTargetType.Post, postId1, reporterId: 3,
            ContentReportReason.Spam, null);
        await Task.Delay(15);
        await svc.CreateAsync(ContentReportTargetType.Post, postId2, reporterId: 2,
            ContentReportReason.Misleading, null);

        var groups = await svc.GetPendingGroupedAsync();

        groups.Should().HaveCount(2);
        // postId2 daha yeni → ilk gelir
        groups[0].TargetId.Should().Be(postId2);
        groups[0].ReportCount.Should().Be(1);
        groups[1].TargetId.Should().Be(postId1);
        groups[1].ReportCount.Should().Be(2);
    }

    [Fact]
    public async Task DismissAsync_PendingReports_SetsDismissed()
    {
        var (svc, ctx, notif) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        await svc.CreateAsync(ContentReportTargetType.Post, postId, reporterId: 2,
            ContentReportReason.Spam, null);
        await svc.CreateAsync(ContentReportTargetType.Post, postId, reporterId: 3,
            ContentReportReason.Spam, null);

        var result = await svc.DismissAsync(
            ContentReportTargetType.Post, postId,
            adminUserId: 4, reviewNote: "İhlal yok");

        result.Success.Should().BeTrue();

        var reports = await ctx.ContentReports.ToListAsync();
        reports.Should().AllSatisfy(r =>
        {
            r.Status.Should().Be(ContentReportStatus.Dismissed);
            r.ReviewedByUserId.Should().Be(4);
            r.ReviewedAt.Should().NotBeNull();
            r.ReviewNote.Should().Be("İhlal yok");
        });

        // 2 reporter'a notification
        notif.Created.Should().HaveCount(2);
        notif.Created.Should().AllSatisfy(n =>
            n.Type.Should().Be(NotificationType.ContentReportResolved));
    }

    [Fact]
    public async Task ActionAsync_DeletesPostAndUpdatesReports()
    {
        var (svc, ctx, notif) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        await svc.CreateAsync(ContentReportTargetType.Post, postId, reporterId: 2,
            ContentReportReason.Harassment, null);

        var result = await svc.ActionAsync(
            ContentReportTargetType.Post, postId,
            adminUserId: 4, reviewNote: "Kuralları ihlal");

        result.Success.Should().BeTrue();
        (await ctx.UserPosts.AnyAsync(p => p.Id == postId)).Should().BeFalse();

        var reports = await ctx.ContentReports.ToListAsync();
        reports.Should().AllSatisfy(r =>
            r.Status.Should().Be(ContentReportStatus.Actioned));

        // Reporter (1 kişi) + content sahibi (1 kişi) = 2 notification
        notif.Created.Should().HaveCount(2);
        notif.Created.Should().Contain(n => n.Type == NotificationType.ContentReportResolved);
        notif.Created.Should().Contain(n => n.Type == NotificationType.ContentRemoved);
    }

    [Fact]
    public async Task ActionAsync_DeletesCommentAndUpdatesReports()
    {
        var (svc, ctx, notif) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        var commentId = await SeedCommentAsync(ctx, postId, userId: 3);
        await svc.CreateAsync(ContentReportTargetType.Comment, commentId, reporterId: 2,
            ContentReportReason.Obscene, null);

        var result = await svc.ActionAsync(
            ContentReportTargetType.Comment, commentId,
            adminUserId: 4, reviewNote: null);

        result.Success.Should().BeTrue();
        (await ctx.PostComments.AnyAsync(c => c.Id == commentId)).Should().BeFalse();

        // Comment sahibine ContentRemoved bildirim
        notif.Created.Should().Contain(n =>
            n.Type == NotificationType.ContentRemoved && n.UserId == 3);
    }
}
