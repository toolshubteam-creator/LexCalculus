using FluentAssertions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Content;

public class PostCommentServiceTests
{
    private static (PostCommentService svc, ApplicationDbContext ctx, RecordingNotificationService notif)
        Setup()
    {
        var ctx = TestDbContextFactory.Create();
        var notif = new RecordingNotificationService();
        var svc = new PostCommentService(
            ctx, new CommentSanitizer(), notif, new NullActivityLogService());

        ctx.Users.AddRange(
            MakeUser(1, "owner@x.com"),
            MakeUser(2, "commenter@x.com"),
            MakeUser(3, "stranger@x.com"));
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

    [Fact]
    public async Task CreateAsync_Valid_PersistsAndReturnsComment()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        var result = await svc.CreateAsync(postId, userId: 2, "İlk yorum");

        result.Success.Should().BeTrue();
        result.Comment.Should().NotBeNull();
        result.Comment!.Body.Should().Contain("İlk yorum");
        result.Comment.IsEdited.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_DraftPost_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1, isPublished: false);

        var result = await svc.CreateAsync(postId, userId: 2, "yorum");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Yayında olmayan");
    }

    [Fact]
    public async Task CreateAsync_EmptyBody_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        var result = await svc.CreateAsync(postId, userId: 2, "   ");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("boş");
    }

    [Fact]
    public async Task CreateAsync_TooLongBody_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        var longBody = new string('a', 1001);

        var result = await svc.CreateAsync(postId, userId: 2, longBody);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("1000");
    }

    [Fact]
    public async Task CreateAsync_BodyWithScript_StripsAndEscapes()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        var result = await svc.CreateAsync(postId, userId: 2,
            "<script>alert(1)</script>kötü içerik");

        result.Success.Should().BeTrue();
        // <script> tag escape edilir; "alert" text olarak kalır ama browser render etmez
        result.Comment!.Body.Should().NotContain("<script");
        result.Comment.Body.Should().Contain("&lt;script");
        result.Comment.Body.Should().Contain("kötü içerik");
    }

    [Fact]
    public async Task CreateAsync_NotifiesPostOwner()
    {
        var (svc, ctx, notif) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        await svc.CreateAsync(postId, userId: 2, "İlk yorum");

        notif.Created.Should().HaveCount(1);
        notif.Created[0].Type.Should().Be(NotificationType.PostComment);
        notif.Created[0].UserId.Should().Be(1, "post sahibi alır");
        notif.Created[0].RelatedEntityType.Should().Be(nameof(PostComment));
    }

    [Fact]
    public async Task CreateAsync_OwnPost_DoesNotNotifySelf()
    {
        var (svc, ctx, notif) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);

        await svc.CreateAsync(postId, userId: 1, "Kendi yorumum");

        notif.Created.Should().BeEmpty("kullanıcı kendi makalesine yorum yapsa kendine notify olmaz");
    }

    [Fact]
    public async Task UpdateAsync_NonOwner_ReturnsUnauthorized()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        var c = await svc.CreateAsync(postId, userId: 2, "orijinal");

        var result = await svc.UpdateAsync(c.Comment!.Id, actingUserId: 3, "kötü düzenleme");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("düzenleyemezsiniz");
    }

    [Fact]
    public async Task UpdateAsync_Valid_SetsIsEditedTrue()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        var c = await svc.CreateAsync(postId, userId: 2, "ilk");
        c.Comment!.IsEdited.Should().BeFalse();

        var updated = await svc.UpdateAsync(c.Comment.Id, actingUserId: 2, "düzeltildi");

        updated.Success.Should().BeTrue();
        updated.Comment!.IsEdited.Should().BeTrue();
        updated.Comment.Body.Should().Contain("düzeltildi");
    }

    [Fact]
    public async Task DeleteAsync_PostOwner_AllowsRemoval()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        var c = await svc.CreateAsync(postId, userId: 2, "yorum");

        // Post sahibi (1) başkasının yorumunu (2) silebilir
        var result = await svc.DeleteAsync(c.Comment!.Id, actingUserId: 1, isAdmin: false);

        result.Success.Should().BeTrue();
        (await ctx.PostComments.AnyAsync(x => x.Id == c.Comment.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_OwnerCanDeleteOwn()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        var c = await svc.CreateAsync(postId, userId: 2, "yorum");

        var result = await svc.DeleteAsync(c.Comment!.Id, actingUserId: 2, isAdmin: false);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_AdminCanDeleteAny()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        var c = await svc.CreateAsync(postId, userId: 2, "yorum");

        var result = await svc.DeleteAsync(c.Comment!.Id, actingUserId: 3, isAdmin: true);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_OtherUser_NotAdmin_ReturnsUnauthorized()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        var c = await svc.CreateAsync(postId, userId: 2, "yorum");

        var result = await svc.DeleteAsync(c.Comment!.Id, actingUserId: 3, isAdmin: false);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("yetk");
    }

    [Fact]
    public async Task GetByPostId_OrderedByCreatedAtAsc()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: 1);
        var c1 = await svc.CreateAsync(postId, userId: 2, "ilk");
        await Task.Delay(15);
        var c2 = await svc.CreateAsync(postId, userId: 3, "ikinci");
        await Task.Delay(15);
        var c3 = await svc.CreateAsync(postId, userId: 2, "üçüncü");

        var list = await svc.GetByPostIdAsync(postId);

        list.Should().HaveCount(3);
        list.Select(c => c.Id).Should().Equal(c1.Comment!.Id, c2.Comment!.Id, c3.Comment!.Id);
    }
}
