using FluentAssertions;
using LexCalculus.Core.Email;
using LexCalculus.Core.Email.Models;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Notifications;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Email;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LexCalculus.Tests.Email;

/// <summary>
/// Faz 6.2 P2 — NotificationEmailDispatcher: master + granüler + anonimize
/// gating + tip→template seçimi. Template render'ı Moq'lanır (gerçek render
/// EmailTemplateRendererTests'te); burada dispatch mantığı doğrulanır.
/// </summary>
public class NotificationEmailDispatcherTests : SqlServerTestBase
{
    private static ApplicationUser MakeUser(
        string suffix, bool active = true, bool master = true) => new()
    {
        UserName = $"{suffix}@x.com",
        NormalizedUserName = $"{suffix.ToUpperInvariant()}@X.COM",
        Email = $"{suffix}@x.com",
        NormalizedEmail = $"{suffix.ToUpperInvariant()}@X.COM",
        FullName = $"User {suffix}", CreatedAt = DateTime.UtcNow,
        IsActive = active, EmailConfirmed = true,
        NotificationsEmailEnabled = master,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static int SeedRecipient(
        ApplicationDbContext ctx, string suffix,
        bool master = true, bool active = true,
        bool conn = true, bool comment = true, bool report = true)
    {
        var user = MakeUser(suffix, active, master);
        ctx.Users.Add(user);
        ctx.SaveChanges();
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id, DisplayName = $"User {suffix}",
            EmailOnConnection = conn, EmailOnComment = comment,
            EmailOnContentReport = report, EmailOnMessageDigest = true
        });
        ctx.SaveChanges();
        return user.Id;
    }

    private static (Mock<IEmailService> email, Mock<IEmailTemplateRenderer> renderer, NotificationEmailDispatcher sut)
        Build(ApplicationDbContext ctx)
    {
        var email = new Mock<IEmailService>();
        email.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        var renderer = new Mock<IEmailTemplateRenderer>();
        renderer.Setup(r => r.RenderAsync(It.IsAny<string>(), It.IsAny<ConnectionEmailModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("<html>conn</html>");
        renderer.Setup(r => r.RenderAsync(It.IsAny<string>(), It.IsAny<CommentEmailModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("<html>comment</html>");
        renderer.Setup(r => r.RenderAsync(It.IsAny<string>(), It.IsAny<ContentReportEmailModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("<html>report</html>");

        var sut = new NotificationEmailDispatcher(
            ctx, email.Object, renderer.Object,
            Options.Create(new SeoSettings { SiteUrl = "https://test.local" }),
            NullLogger<NotificationEmailDispatcher>.Instance);
        return (email, renderer, sut);
    }

    [Fact]
    public async Task DispatchAsync_ConnectionRequest_SendsEmailWhenPreferenceOn()
    {
        await using var ctx = _db.Create();
        var recipId = SeedRecipient(ctx, "recip", conn: true);

        var other = MakeUser("other");
        ctx.Users.Add(other); ctx.SaveChanges();
        ctx.UserProfiles.Add(new UserProfile { UserId = other.Id, DisplayName = "Other Kisi", PublicSlug = "other-kisi" });
        var conn = new UserConnection
        {
            RequesterId = other.Id, TargetId = recipId,
            Status = UserConnectionStatus.Pending, CreatedAt = DateTime.UtcNow
        };
        ctx.UserConnections.Add(conn); ctx.SaveChanges();

        var (email, renderer, sut) = Build(ctx);
        await sut.DispatchAsync(new Notification
        {
            UserId = recipId, Type = NotificationType.ConnectionRequest,
            Title = "t", Body = "b",
            RelatedEntityType = "UserConnection", RelatedEntityId = conn.Id
        });

        renderer.Verify(r => r.RenderAsync("Connection", It.IsAny<ConnectionEmailModel>(), It.IsAny<CancellationToken>()), Times.Once);
        email.Verify(e => e.SendAsync(
            It.Is<EmailMessage>(m => m.ToAddress == "recip@x.com" && m.Subject == "Yeni bağlantı isteği"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ConnectionRequest_SkipsWhenGranularOff()
    {
        await using var ctx = _db.Create();
        var recipId = SeedRecipient(ctx, "recip", conn: false);

        var (email, _, sut) = Build(ctx);
        await sut.DispatchAsync(new Notification
        {
            UserId = recipId, Type = NotificationType.ConnectionRequest,
            Title = "t", Body = "b", RelatedEntityType = "UserConnection", RelatedEntityId = 1
        });

        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_MasterSwitchOff_SkipsEmail()
    {
        await using var ctx = _db.Create();
        // Granüler açık ama master kapalı → hiçbir e-posta gitmez
        var recipId = SeedRecipient(ctx, "recip", master: false, conn: true);

        var (email, _, sut) = Build(ctx);
        await sut.DispatchAsync(new Notification
        {
            UserId = recipId, Type = NotificationType.ConnectionRequest,
            Title = "t", Body = "b", RelatedEntityType = "UserConnection", RelatedEntityId = 1
        });

        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_InactiveUser_SkipsEmail()
    {
        await using var ctx = _db.Create();
        var recipId = SeedRecipient(ctx, "recip", active: false);

        var (email, _, sut) = Build(ctx);
        await sut.DispatchAsync(new Notification
        {
            UserId = recipId, Type = NotificationType.ConnectionRequest,
            Title = "t", Body = "b", RelatedEntityType = "UserConnection", RelatedEntityId = 1
        });

        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_PostComment_RendersCommentTemplate()
    {
        await using var ctx = _db.Create();
        var ownerId = SeedRecipient(ctx, "owner", comment: true);

        var commenter = MakeUser("commenter");
        ctx.Users.Add(commenter); ctx.SaveChanges();
        ctx.UserProfiles.Add(new UserProfile { UserId = commenter.Id, DisplayName = "Yorumcu" });
        var category = new PostCategory { Name = "Genel", Slug = "genel", DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.PostCategories.Add(category); ctx.SaveChanges();
        var post = new UserPost
        {
            UserId = ownerId, CategoryId = category.Id, Title = "Kidem Rehberi", Slug = "kidem",
            Body = "<p>x</p>", IsPublished = true, PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        ctx.UserPosts.Add(post); ctx.SaveChanges();
        var comment = new PostComment
        {
            PostId = post.Id, UserId = commenter.Id, Body = "Guzel yazi",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, IsEdited = false
        };
        ctx.PostComments.Add(comment); ctx.SaveChanges();

        var (email, renderer, sut) = Build(ctx);
        await sut.DispatchAsync(new Notification
        {
            UserId = ownerId, Type = NotificationType.PostComment,
            Title = "t", Body = "b", Link = "/uye/owner/makale/kidem",
            RelatedEntityType = "PostComment", RelatedEntityId = comment.Id
        });

        renderer.Verify(r => r.RenderAsync("Comment", It.IsAny<CommentEmailModel>(), It.IsAny<CancellationToken>()), Times.Once);
        email.Verify(e => e.SendAsync(
            It.Is<EmailMessage>(m => m.Subject == "Makalenize yeni yorum"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ContentHidden_OwnerNotification_RendersContentReport()
    {
        await using var ctx = _db.Create();
        var ownerId = SeedRecipient(ctx, "owner", report: true);

        var (email, renderer, sut) = Build(ctx);
        await sut.DispatchAsync(new Notification
        {
            UserId = ownerId, Type = NotificationType.ContentHidden,
            Title = "t", Body = "b",
            RelatedEntityType = "Post", RelatedEntityId = 99   // sahip bildirimi (Post/Comment/Message)
        });

        renderer.Verify(r => r.RenderAsync("ContentReport", It.IsAny<ContentReportEmailModel>(), It.IsAny<CancellationToken>()), Times.Once);
        email.Verify(e => e.SendAsync(
            It.Is<EmailMessage>(m => m.Subject == "İçeriğiniz gizlendi"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ContentHidden_ReporterNotification_SkipsEmail()
    {
        await using var ctx = _db.Create();
        var reporterId = SeedRecipient(ctx, "reporter", report: true);

        var (email, _, sut) = Build(ctx);
        // Reporter bildirimi: relatedEntityType = "ContentReport" → owner-only kapısından geçemez
        await sut.DispatchAsync(new Notification
        {
            UserId = reporterId, Type = NotificationType.ContentHidden,
            Title = "t", Body = "b",
            RelatedEntityType = "ContentReport", RelatedEntityId = 5
        });

        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
