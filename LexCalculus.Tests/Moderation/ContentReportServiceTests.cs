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

public class ContentReportServiceTests : SqlServerTestBase
{
    // Setup sonrası seed edilen kayıtların DB-generated Id'leri.
    private int _ownerId, _reporterId, _strangerId, _adminId, _categoryId;

    private (ContentReportService svc, ApplicationDbContext ctx, RecordingNotificationService notif)
        Setup()
    {
        var ctx = _db.Create();
        var notif = new RecordingNotificationService();
        var svc = new ContentReportService(ctx, notif, new NullActivityLogService(),
            new NoOpMessagingNotifier(), new PostTagService(ctx));

        var owner = MakeUser("owner@x.com");
        var reporter = MakeUser("reporter@x.com");
        var stranger = MakeUser("stranger@x.com");
        var admin = MakeUser("admin@x.com");
        ctx.Users.AddRange(owner, reporter, stranger, admin);
        var category = new PostCategory
        {
            Name = "Genel", Slug = "genel",
            DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(category);
        ctx.SaveChanges();

        _ownerId = owner.Id;
        _reporterId = reporter.Id;
        _strangerId = stranger.Id;
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
    public async Task CreateAsync_Valid_Persists()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: _reporterId,
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
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: _ownerId,
            ContentReportReason.Spam, note: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kendi");
    }

    [Fact]
    public async Task CreateAsync_DraftPost_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId, isPublished: false);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: _reporterId,
            ContentReportReason.Spam, note: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Yayında");
    }

    [Fact]
    public async Task CreateAsync_NonExistentTarget_ReturnsError()
    {
        var (svc, _, _) = Setup();

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, targetId: 9999, reporterId: _reporterId,
            ContentReportReason.Spam, note: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bulunamadı");
    }

    [Fact]
    public async Task CreateAsync_DuplicateReport_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        var first = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: _reporterId,
            ContentReportReason.Spam, note: null);
        first.Success.Should().BeTrue();

        var second = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: _reporterId,
            ContentReportReason.Harassment, note: null);

        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("zaten");
        (await ctx.ContentReports.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_OtherReason_RequiresLongEnoughNote()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: _reporterId,
            ContentReportReason.Other, note: "kısa");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Diğer");
    }

    [Fact]
    public async Task CreateAsync_OtherReason_EmptyNote_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: _reporterId,
            ContentReportReason.Other, note: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Diğer");
    }

    [Fact]
    public async Task CreateAsync_OtherReason_LongEnoughNote_Succeeds()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Post, postId, reporterId: _reporterId,
            ContentReportReason.Other, note: "Bu yeterince uzun bir açıklama metni.");

        result.Success.Should().BeTrue();
        result.Report!.Note.Should().Contain("yeterince");
    }

    [Fact]
    public async Task HasReportedAsync_AfterCreate_ReturnsTrue()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        (await svc.HasReportedAsync(ContentReportTargetType.Post, postId, userId: _reporterId))
            .Should().BeFalse();

        await svc.CreateAsync(ContentReportTargetType.Post, postId,
            reporterId: _reporterId, ContentReportReason.Spam, null);

        (await svc.HasReportedAsync(ContentReportTargetType.Post, postId, userId: _reporterId))
            .Should().BeTrue();
        (await svc.HasReportedAsync(ContentReportTargetType.Post, postId, userId: _strangerId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ForComment_Persists()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        var commentId = await SeedCommentAsync(ctx, postId, userId: _strangerId);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Comment, commentId, reporterId: _reporterId,
            ContentReportReason.Harassment, note: null);

        result.Success.Should().BeTrue();
        result.Report!.TargetType.Should().Be(ContentReportTargetType.Comment);
        result.Report.TargetId.Should().Be(commentId);
    }

    [Fact]
    public async Task CreateAsync_OwnComment_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        var commentId = await SeedCommentAsync(ctx, postId, userId: _reporterId);

        var result = await svc.CreateAsync(
            ContentReportTargetType.Comment, commentId, reporterId: _reporterId,
            ContentReportReason.Spam, note: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kendi");
    }

    [Fact]
    public async Task GetPendingGroupedAsync_GroupsByTarget_OrdersByLatest()
    {
        var (svc, ctx, _) = Setup();
        var postId1 = await SeedPostAsync(ctx, userId: _ownerId, slug: "p1");
        var postId2 = await SeedPostAsync(ctx, userId: _ownerId, slug: "p2");

        await svc.CreateAsync(ContentReportTargetType.Post, postId1, reporterId: _reporterId,
            ContentReportReason.Spam, null);
        await Task.Delay(15);
        await svc.CreateAsync(ContentReportTargetType.Post, postId1, reporterId: _strangerId,
            ContentReportReason.Spam, null);
        await Task.Delay(15);
        await svc.CreateAsync(ContentReportTargetType.Post, postId2, reporterId: _reporterId,
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
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        await svc.CreateAsync(ContentReportTargetType.Post, postId, reporterId: _reporterId,
            ContentReportReason.Spam, null);
        await svc.CreateAsync(ContentReportTargetType.Post, postId, reporterId: _strangerId,
            ContentReportReason.Spam, null);

        var result = await svc.DismissAsync(
            ContentReportTargetType.Post, postId,
            adminUserId: _adminId, reviewNote: "İhlal yok");

        result.Success.Should().BeTrue();

        var reports = await ctx.ContentReports.ToListAsync();
        reports.Should().AllSatisfy(r =>
        {
            r.Status.Should().Be(ContentReportStatus.Dismissed);
            r.ReviewedByUserId.Should().Be(_adminId);
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
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        await svc.CreateAsync(ContentReportTargetType.Post, postId, reporterId: _reporterId,
            ContentReportReason.Harassment, null);

        var result = await svc.ActionAsync(
            ContentReportTargetType.Post, postId,
            adminUserId: _adminId, reviewNote: "Kuralları ihlal");

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
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        var commentId = await SeedCommentAsync(ctx, postId, userId: _strangerId);
        await svc.CreateAsync(ContentReportTargetType.Comment, commentId, reporterId: _reporterId,
            ContentReportReason.Obscene, null);

        var result = await svc.ActionAsync(
            ContentReportTargetType.Comment, commentId,
            adminUserId: _adminId, reviewNote: null);

        result.Success.Should().BeTrue();
        (await ctx.PostComments.AnyAsync(c => c.Id == commentId)).Should().BeFalse();

        // Comment sahibine ContentRemoved bildirim
        notif.Created.Should().Contain(n =>
            n.Type == NotificationType.ContentRemoved && n.UserId == _strangerId);
    }

    [Fact]
    public async Task DismissAsync_NoPendingReports_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        var result = await svc.DismissAsync(
            ContentReportTargetType.Post, postId,
            adminUserId: _adminId, reviewNote: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bekleyen");
    }

    [Fact]
    public async Task ActionAsync_NonExistentTarget_ReturnsError()
    {
        var (svc, _, _) = Setup();

        var result = await svc.ActionAsync(
            ContentReportTargetType.Post, targetId: 99999,
            adminUserId: _adminId, reviewNote: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bekleyen");
    }

    [Fact]
    public async Task GetPendingCountAsync_ReturnsAccurateCount()
    {
        var (svc, ctx, _) = Setup();
        var postId1 = await SeedPostAsync(ctx, userId: _ownerId, slug: "p1");
        var postId2 = await SeedPostAsync(ctx, userId: _ownerId, slug: "p2");

        (await svc.GetPendingCountAsync()).Should().Be(0);

        await svc.CreateAsync(ContentReportTargetType.Post, postId1, reporterId: _reporterId,
            ContentReportReason.Spam, null);
        await svc.CreateAsync(ContentReportTargetType.Post, postId1, reporterId: _strangerId,
            ContentReportReason.Spam, null);
        await svc.CreateAsync(ContentReportTargetType.Post, postId2, reporterId: _reporterId,
            ContentReportReason.Misleading, null);

        (await svc.GetPendingCountAsync()).Should().Be(3);

        // Dismiss postId1 → pending count azalır (sadece postId2 kalır)
        await svc.DismissAsync(ContentReportTargetType.Post, postId1,
            adminUserId: _adminId, reviewNote: null);

        (await svc.GetPendingCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ActionAsync_DeletesPublishedPostWithTags_DecrementsUsageCount()
    {
        var (svc, ctx, _) = Setup();
        // Tag'ler ve post oluştur (UsageCount=1)
        var tag1 = new PostTag { Name = "Vergi", Slug = "vergi", UsageCount = 1, CreatedAt = DateTime.UtcNow };
        var tag2 = new PostTag { Name = "İcra", Slug = "icra", UsageCount = 1, CreatedAt = DateTime.UtcNow };
        ctx.PostTags.AddRange(tag1, tag2);
        await ctx.SaveChangesAsync();

        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        ctx.PostTagLinks.AddRange(
            new PostTagLink { PostId = postId, TagId = tag1.Id, CreatedAt = DateTime.UtcNow },
            new PostTagLink { PostId = postId, TagId = tag2.Id, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        await svc.CreateAsync(ContentReportTargetType.Post, postId, reporterId: _reporterId,
            ContentReportReason.Spam, null);

        var result = await svc.ActionAsync(
            ContentReportTargetType.Post, postId,
            adminUserId: _adminId, reviewNote: null);

        result.Success.Should().BeTrue();

        var t1 = await ctx.PostTags.FirstAsync(t => t.Id == tag1.Id);
        var t2 = await ctx.PostTags.FirstAsync(t => t.Id == tag2.Id);
        t1.UsageCount.Should().Be(0);
        t2.UsageCount.Should().Be(0);
    }
}
