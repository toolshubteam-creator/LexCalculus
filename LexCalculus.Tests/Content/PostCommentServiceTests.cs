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

public class PostCommentServiceTests : SqlServerTestBase
{
    // Setup sonrası seed edilen kayıtların DB-generated Id'leri.
    private int _ownerId, _commenterId, _strangerId, _categoryId;

    private (PostCommentService svc, ApplicationDbContext ctx, RecordingNotificationService notif)
        Setup()
    {
        var ctx = _db.Create();
        var notif = new RecordingNotificationService();
        var svc = new PostCommentService(
            ctx, new CommentSanitizer(), notif, new NullActivityLogService());

        var owner = MakeUser("owner@x.com");
        var commenter = MakeUser("commenter@x.com");
        var stranger = MakeUser("stranger@x.com");
        ctx.Users.AddRange(owner, commenter, stranger);
        var category = new PostCategory
        {
            Name = "Genel", Slug = "genel",
            DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(category);
        ctx.SaveChanges();

        _ownerId = owner.Id;
        _commenterId = commenter.Id;
        _strangerId = stranger.Id;
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

    [Fact]
    public async Task CreateAsync_Valid_PersistsAndReturnsComment()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        var result = await svc.CreateAsync(postId, userId: _commenterId, "İlk yorum");

        result.Success.Should().BeTrue();
        result.Comment.Should().NotBeNull();
        result.Comment!.Body.Should().Contain("İlk yorum");
        result.Comment.IsEdited.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_DraftPost_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId, isPublished: false);

        var result = await svc.CreateAsync(postId, userId: _commenterId, "yorum");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Yayında olmayan");
    }

    [Fact]
    public async Task CreateAsync_EmptyBody_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        var result = await svc.CreateAsync(postId, userId: _commenterId, "   ");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("boş");
    }

    [Fact]
    public async Task CreateAsync_TooLongBody_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        var longBody = new string('a', 1001);

        var result = await svc.CreateAsync(postId, userId: _commenterId, longBody);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("1000");
    }

    [Fact]
    public async Task CreateAsync_BodyWithScript_StripsAndEscapes()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        var result = await svc.CreateAsync(postId, userId: _commenterId,
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
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        await svc.CreateAsync(postId, userId: _commenterId, "İlk yorum");

        notif.Created.Should().HaveCount(1);
        notif.Created[0].Type.Should().Be(NotificationType.PostComment);
        notif.Created[0].UserId.Should().Be(_ownerId, "post sahibi alır");
        notif.Created[0].RelatedEntityType.Should().Be(nameof(PostComment));
    }

    [Fact]
    public async Task CreateAsync_OwnPost_DoesNotNotifySelf()
    {
        var (svc, ctx, notif) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);

        await svc.CreateAsync(postId, userId: _ownerId, "Kendi yorumum");

        notif.Created.Should().BeEmpty("kullanıcı kendi makalesine yorum yapsa kendine notify olmaz");
    }

    [Fact]
    public async Task UpdateAsync_NonOwner_ReturnsUnauthorized()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        var c = await svc.CreateAsync(postId, userId: _commenterId, "orijinal");

        var result = await svc.UpdateAsync(c.Comment!.Id, actingUserId: _strangerId, "kötü düzenleme");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("düzenleyemezsiniz");
    }

    [Fact]
    public async Task UpdateAsync_Valid_SetsIsEditedTrue()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        var c = await svc.CreateAsync(postId, userId: _commenterId, "ilk");
        c.Comment!.IsEdited.Should().BeFalse();

        var updated = await svc.UpdateAsync(c.Comment.Id, actingUserId: _commenterId, "düzeltildi");

        updated.Success.Should().BeTrue();
        updated.Comment!.IsEdited.Should().BeTrue();
        updated.Comment.Body.Should().Contain("düzeltildi");
    }

    [Fact]
    public async Task DeleteAsync_PostOwner_AllowsRemoval()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        var c = await svc.CreateAsync(postId, userId: _commenterId, "yorum");

        // Post sahibi başkasının yorumunu silebilir
        var result = await svc.DeleteAsync(c.Comment!.Id, actingUserId: _ownerId, isAdmin: false);

        result.Success.Should().BeTrue();
        (await ctx.PostComments.AnyAsync(x => x.Id == c.Comment.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_OwnerCanDeleteOwn()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        var c = await svc.CreateAsync(postId, userId: _commenterId, "yorum");

        var result = await svc.DeleteAsync(c.Comment!.Id, actingUserId: _commenterId, isAdmin: false);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_AdminCanDeleteAny()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        var c = await svc.CreateAsync(postId, userId: _commenterId, "yorum");

        var result = await svc.DeleteAsync(c.Comment!.Id, actingUserId: _strangerId, isAdmin: true);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_OtherUser_NotAdmin_ReturnsUnauthorized()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        var c = await svc.CreateAsync(postId, userId: _commenterId, "yorum");

        var result = await svc.DeleteAsync(c.Comment!.Id, actingUserId: _strangerId, isAdmin: false);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("yetk");
    }

    [Fact]
    public async Task GetByPostId_OrderedByCreatedAtAsc()
    {
        var (svc, ctx, _) = Setup();
        var postId = await SeedPostAsync(ctx, userId: _ownerId);
        var c1 = await svc.CreateAsync(postId, userId: _commenterId, "ilk");
        await Task.Delay(15);
        var c2 = await svc.CreateAsync(postId, userId: _strangerId, "ikinci");
        await Task.Delay(15);
        var c3 = await svc.CreateAsync(postId, userId: _commenterId, "üçüncü");

        var list = await svc.GetByPostIdAsync(postId);

        list.Should().HaveCount(3);
        list.Select(c => c.Id).Should().Equal(c1.Comment!.Id, c2.Comment!.Id, c3.Comment!.Id);
    }
}
