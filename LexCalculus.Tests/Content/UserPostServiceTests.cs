using FluentAssertions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Content;

public class UserPostServiceTests
{
    private static (UserPostService svc, ApplicationDbContext ctx, PostTagService tagSvc) Setup(
        bool seedCategory = true)
    {
        var ctx = TestDbContextFactory.Create();
        var tagSvc = new PostTagService(ctx);
        var svc = new UserPostService(ctx, tagSvc, new NullActivityLogService());
        if (seedCategory)
        {
            ctx.PostCategories.Add(new PostCategory
            {
                Id = 1, Name = "Genel", Slug = "genel", DisplayOrder = 1,
                IsActive = true, CreatedAt = DateTime.UtcNow
            });
            ctx.PostCategories.Add(new PostCategory
            {
                Id = 2, Name = "Pasif", Slug = "pasif", DisplayOrder = 2,
                IsActive = false, CreatedAt = DateTime.UtcNow
            });
            ctx.SaveChanges();
        }
        return (svc, ctx, tagSvc);
    }

    private static async Task SeedUserAsync(ApplicationDbContext ctx, int userId)
    {
        ctx.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"u{userId}@x.com", NormalizedUserName = $"U{userId}@X.COM",
            Email = $"u{userId}@x.com", NormalizedEmail = $"U{userId}@X.COM",
            FullName = $"User {userId}", CreatedAt = DateTime.UtcNow,
            IsActive = true, EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await ctx.SaveChangesAsync();
    }

    private static UserPostInput Input(string title = "Test Makalesi",
        string body = "<p>içerik</p>", int categoryId = 1,
        IReadOnlyList<string>? tags = null)
        => new(title, body, categoryId, null, tags ?? Array.Empty<string>());

    // ─── CreateDraft ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_Valid_GeneratesSlug()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);

        var result = await svc.CreateDraftAsync(1, Input("İş Hukuku Makalem"));

        result.Success.Should().BeTrue();
        result.Post!.Slug.Should().Be("is-hukuku-makalem");
        result.Post.IsPublished.Should().BeFalse();
        result.Post.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateDraft_DuplicateTitleSameUser_AppendsSuffix()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);

        await svc.CreateDraftAsync(1, Input("Aynı Başlık"));
        var second = await svc.CreateDraftAsync(1, Input("Aynı Başlık"));
        var third = await svc.CreateDraftAsync(1, Input("Aynı Başlık"));

        second.Post!.Slug.Should().Be("ayni-baslik-2");
        third.Post!.Slug.Should().Be("ayni-baslik-3");
    }

    [Fact]
    public async Task CreateDraft_SameTitleDifferentUsers_AllowedInUserNamespace()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);
        await SeedUserAsync(ctx, 2);

        var u1 = await svc.CreateDraftAsync(1, Input("Aynı Başlık"));
        var u2 = await svc.CreateDraftAsync(2, Input("Aynı Başlık"));

        u1.Post!.Slug.Should().Be("ayni-baslik");
        u2.Post!.Slug.Should().Be("ayni-baslik",
            "user namespace altında çakışma yok");
    }

    [Fact]
    public async Task CreateDraft_EmptyTitle_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);

        var result = await svc.CreateDraftAsync(1, Input("   "));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Başlık");
    }

    [Fact]
    public async Task CreateDraft_EmptyBody_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);

        var result = await svc.CreateDraftAsync(1, Input(body: " "));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("İçerik");
    }

    [Fact]
    public async Task CreateDraft_InactiveCategory_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);

        var result = await svc.CreateDraftAsync(1, Input(categoryId: 2));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("aktif değil");
    }

    [Fact]
    public async Task CreateDraft_TooManyTags_ReturnsError()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);

        var result = await svc.CreateDraftAsync(1, Input(
            tags: new[] { "a", "b", "c", "d", "e", "f" }));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("etiket");
    }

    [Fact]
    public async Task CreateDraft_TagsAdded_DraftDoesNotIncrementUsageCount()
    {
        var (svc, ctx, tagSvc) = Setup();
        await SeedUserAsync(ctx, 1);

        await svc.CreateDraftAsync(1, Input(tags: new[] { "kvkk", "iş hukuku" }));

        var tags = await ctx.PostTags.AsNoTracking().ToListAsync();
        tags.Should().HaveCount(2);
        tags.Should().AllSatisfy(t => t.UsageCount.Should().Be(0,
            "draft state'te tag UsageCount artmaz"));
    }

    // ─── Update ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_NonOwner_ReturnsUnauthorized()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);
        await SeedUserAsync(ctx, 2);
        var draft = await svc.CreateDraftAsync(1, Input());

        var result = await svc.UpdateAsync(draft.Post!.Id, actingUserId: 2, Input("Yeni"));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("yetk");
    }

    [Fact]
    public async Task Update_DoesNotRegenerateSlug()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);
        var draft = await svc.CreateDraftAsync(1, Input("İlk Başlık"));
        var originalSlug = draft.Post!.Slug;

        var updated = await svc.UpdateAsync(draft.Post.Id, 1, Input("Tamamen Farklı Başlık"));

        updated.Success.Should().BeTrue();
        updated.Post!.Title.Should().Be("Tamamen Farklı Başlık");
        updated.Post.Slug.Should().Be(originalSlug,
            "Yaklaşım 4: slug ilk üretimde sabit");
    }

    [Fact]
    public async Task Update_TagsChanged_PublishedPost_AdjustsUsageCount()
    {
        var (svc, ctx, tagSvc) = Setup();
        await SeedUserAsync(ctx, 1);
        var draft = await svc.CreateDraftAsync(1, Input(tags: new[] { "kvkk", "iş hukuku" }));
        await svc.PublishAsync(draft.Post!.Id, 1);

        // Yayında: kvkk=1, iş hukuku=1
        // Update ile kvkk çıkar, ticaret ekle
        await svc.UpdateAsync(draft.Post.Id, 1, Input(tags: new[] { "iş hukuku", "ticaret" }));

        var kvkk = await ctx.PostTags.AsNoTracking().FirstAsync(t => t.Slug == "kvkk");
        var isHukuku = await ctx.PostTags.AsNoTracking().FirstAsync(t => t.Slug == "is-hukuku");
        var ticaret = await ctx.PostTags.AsNoTracking().FirstAsync(t => t.Slug == "ticaret");

        kvkk.UsageCount.Should().Be(0, "kvkk kaldırıldı");
        isHukuku.UsageCount.Should().Be(1, "iş hukuku korundu");
        ticaret.UsageCount.Should().Be(1, "ticaret eklendi");
    }

    // ─── Publish ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Publish_FromDraft_SetsPublishedAtAndIncrementsTagCount()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);
        var draft = await svc.CreateDraftAsync(1, Input(tags: new[] { "kvkk" }));

        var result = await svc.PublishAsync(draft.Post!.Id, 1);

        result.Success.Should().BeTrue();
        result.Post!.IsPublished.Should().BeTrue();
        result.Post.PublishedAt.Should().NotBeNull();
        var tag = await ctx.PostTags.AsNoTracking().FirstAsync(t => t.Slug == "kvkk");
        tag.UsageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_AlreadyPublished_NoOp()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);
        var draft = await svc.CreateDraftAsync(1, Input(tags: new[] { "kvkk" }));
        await svc.PublishAsync(draft.Post!.Id, 1);

        var second = await svc.PublishAsync(draft.Post.Id, 1);

        second.Success.Should().BeTrue();
        var tag = await ctx.PostTags.AsNoTracking().FirstAsync(t => t.Slug == "kvkk");
        tag.UsageCount.Should().Be(1, "ikinci publish çağrısı tag count artırmaz");
    }

    // ─── Unpublish ────────────────────────────────────────────────────────

    [Fact]
    public async Task Unpublish_FromPublished_DecrementsTagCount()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);
        var draft = await svc.CreateDraftAsync(1, Input(tags: new[] { "kvkk" }));
        await svc.PublishAsync(draft.Post!.Id, 1);

        await svc.UnpublishAsync(draft.Post.Id, 1);

        var tag = await ctx.PostTags.AsNoTracking().FirstAsync(t => t.Slug == "kvkk");
        tag.UsageCount.Should().Be(0);
        var refreshed = await ctx.UserPosts.AsNoTracking().FirstAsync(p => p.Id == draft.Post.Id);
        refreshed.IsPublished.Should().BeFalse();
    }

    // ─── Delete ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_PublishedPost_DecrementsTagCountAndCascadesLinks()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);
        var draft = await svc.CreateDraftAsync(1, Input(tags: new[] { "kvkk", "ticaret" }));
        await svc.PublishAsync(draft.Post!.Id, 1);

        var result = await svc.DeleteAsync(draft.Post.Id, 1);

        result.Success.Should().BeTrue();
        (await ctx.UserPosts.AnyAsync(p => p.Id == draft.Post.Id)).Should().BeFalse();
        (await ctx.PostTagLinks.AnyAsync(l => l.PostId == draft.Post.Id))
            .Should().BeFalse("cascade ile link satırları silinir");
        var tags = await ctx.PostTags.AsNoTracking().ToListAsync();
        tags.Should().AllSatisfy(t => t.UsageCount.Should().Be(0));
    }

    [Fact]
    public async Task Delete_DraftPost_DoesNotAffectTagCount()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);
        var draft = await svc.CreateDraftAsync(1, Input(tags: new[] { "kvkk" }));

        await svc.DeleteAsync(draft.Post!.Id, 1);

        var tag = await ctx.PostTags.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == "kvkk");
        tag.Should().NotBeNull();
        tag!.UsageCount.Should().Be(0, "draft silindi, tag zaten 0'dı");
    }

    [Fact]
    public async Task Delete_NonOwner_ReturnsUnauthorized()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);
        await SeedUserAsync(ctx, 2);
        var draft = await svc.CreateDraftAsync(1, Input());

        var result = await svc.DeleteAsync(draft.Post!.Id, actingUserId: 2);

        result.Success.Should().BeFalse();
    }

    // ─── Query ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByUserIdAsync_IncludeUnpublishedTrue_ReturnsBoth()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);
        var draft = await svc.CreateDraftAsync(1, Input("Birinci"));
        var second = await svc.CreateDraftAsync(1, Input("İkinci"));
        await svc.PublishAsync(second.Post!.Id, 1);

        var list = await svc.GetByUserIdAsync(1, includeUnpublished: true);

        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUserIdAsync_IncludeUnpublishedFalse_OnlyPublished()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);
        await svc.CreateDraftAsync(1, Input("Taslak"));
        var pub = await svc.CreateDraftAsync(1, Input("Yayında"));
        await svc.PublishAsync(pub.Post!.Id, 1);

        var list = await svc.GetByUserIdAsync(1, includeUnpublished: false);

        list.Should().HaveCount(1);
        list[0].Title.Should().Be("Yayında");
    }

    [Fact]
    public async Task GetByUserAndSlugAsync_ReturnsPostWithTagsAndCategory()
    {
        var (svc, ctx, _) = Setup();
        await SeedUserAsync(ctx, 1);
        var draft = await svc.CreateDraftAsync(1, Input("Test Post", tags: new[] { "kvkk" }));

        var fetched = await svc.GetByUserAndSlugAsync(1, draft.Post!.Slug);

        fetched.Should().NotBeNull();
        fetched!.Category.Should().NotBeNull();
        fetched.TagLinks.Should().HaveCount(1);
        fetched.TagLinks.First().Tag.Slug.Should().Be("kvkk");
    }
}
