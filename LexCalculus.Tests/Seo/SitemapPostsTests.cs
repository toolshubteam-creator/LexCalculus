using FluentAssertions;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Infrastructure.Seo;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LexCalculus.Tests.Seo;

public class SitemapPostsTests : SqlServerTestBase
{
    private static DefaultSitemapBuilder CreateBuilder(
        LexCalculus.Infrastructure.Data.ApplicationDbContext ctx,
        string siteUrl = "https://test.lexcalculus.com")
    {
        var seo = Options.Create(new SeoSettings { SiteUrl = siteUrl });
        var registry = new Mock<ICalculatorRegistry>();
        registry.Setup(x => x.GetAll())
            .Returns(new List<CalculatorMetadata>().AsReadOnly());
        registry.Setup(x => x.GetActiveCategories())
            .Returns(new List<CalculatorCategory>());
        return new DefaultSitemapBuilder(seo, registry.Object, ctx);
    }

    private static ApplicationUser MakeUser(string email, bool isActive) => new()
    {
        UserName = email, NormalizedUserName = email.ToUpperInvariant(),
        Email = email, NormalizedEmail = email.ToUpperInvariant(),
        FullName = email, CreatedAt = DateTime.UtcNow,
        IsActive = isActive, EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static UserPost MakePost(int userId, int catId, string slug, bool isPublished) => new()
    {
        UserId = userId, CategoryId = catId,
        Title = $"Post {slug}", Slug = slug, Body = "<p>x</p>",
        IsPublished = isPublished,
        PublishedAt = isPublished ? DateTime.UtcNow : null,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Sitemap_IncludesPublishedPostsWithPublicAuthor()
    {
        await using var ctx = _db.Create();
        var user = MakeUser("pub@x.com", isActive: true);
        ctx.Users.Add(user);
        var category = new PostCategory
        {
            Name = "İş", Slug = "is", DisplayOrder = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(category);
        await ctx.SaveChangesAsync();

        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id, DisplayName = "Pub", PublicSlug = "pub-yazar",
            IsPublicProfile = true
        });
        ctx.UserPosts.Add(MakePost(user.Id, category.Id, "is-makalesi", isPublished: true));
        await ctx.SaveChangesAsync();

        var builder = CreateBuilder(ctx);
        var nodes = await builder.BuildAsync();

        nodes.Should().Contain(n =>
            n.Url == "https://test.lexcalculus.com/uye/pub-yazar/makale/is-makalesi");
    }

    [Fact]
    public async Task Sitemap_ExcludesDraftPosts()
    {
        await using var ctx = _db.Create();
        var user = MakeUser("drf@x.com", isActive: true);
        ctx.Users.Add(user);
        var category = new PostCategory
        {
            Name = "İş", Slug = "is", DisplayOrder = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(category);
        await ctx.SaveChangesAsync();

        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id, DisplayName = "Drf", PublicSlug = "drf-yazar",
            IsPublicProfile = true
        });
        ctx.UserPosts.Add(MakePost(user.Id, category.Id, "taslak-makale", isPublished: false));
        await ctx.SaveChangesAsync();

        var builder = CreateBuilder(ctx);
        var nodes = await builder.BuildAsync();

        nodes.Should().NotContain(n => n.Url.Contains("/makale/taslak-makale"),
            "yayında olmayan post sitemap'te olmamalı");
    }

    [Fact]
    public async Task Sitemap_ExcludesPostsWithPrivateAuthor()
    {
        await using var ctx = _db.Create();
        var user = MakeUser("priv@x.com", isActive: true);
        ctx.Users.Add(user);
        var category = new PostCategory
        {
            Name = "İş", Slug = "is", DisplayOrder = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(category);
        await ctx.SaveChangesAsync();

        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id, DisplayName = "Priv", PublicSlug = "priv-yazar",
            IsPublicProfile = false   // gizli profil
        });
        ctx.UserPosts.Add(MakePost(user.Id, category.Id, "gizli-yazar-yayinda", isPublished: true));
        await ctx.SaveChangesAsync();

        var builder = CreateBuilder(ctx);
        var nodes = await builder.BuildAsync();

        nodes.Should().NotContain(n => n.Url.Contains("/makale/gizli-yazar-yayinda"),
            "gizli profil yazarın yayındaki makalesi sitemap dışı (charter §G-a)");
    }

    [Fact]
    public async Task Sitemap_ExcludesPostsWithInactiveAuthor()
    {
        await using var ctx = _db.Create();
        var user = MakeUser("inact@x.com", isActive: false);
        ctx.Users.Add(user);
        var category = new PostCategory
        {
            Name = "İş", Slug = "is", DisplayOrder = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(category);
        await ctx.SaveChangesAsync();

        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id, DisplayName = "Inact", PublicSlug = "inact-yazar",
            IsPublicProfile = true
        });
        ctx.UserPosts.Add(MakePost(user.Id, category.Id, "pasif-yazar-makale", isPublished: true));
        await ctx.SaveChangesAsync();

        var builder = CreateBuilder(ctx);
        var nodes = await builder.BuildAsync();

        nodes.Should().NotContain(n => n.Url.Contains("/makale/pasif-yazar-makale"));
    }

    [Fact]
    public async Task Sitemap_ExcludesHiddenPosts()
    {
        await using var ctx = _db.Create();
        var user = MakeUser("hid@x.com", isActive: true);
        ctx.Users.Add(user);
        var category = new PostCategory
        {
            Name = "İş", Slug = "is", DisplayOrder = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(category);
        await ctx.SaveChangesAsync();

        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id, DisplayName = "Hid", PublicSlug = "hid-yazar",
            IsPublicProfile = true
        });
        var hidden = MakePost(user.Id, category.Id, "gizlenmis-makale", isPublished: true);
        hidden.IsModeratorHidden = true;
        ctx.UserPosts.Add(hidden);
        await ctx.SaveChangesAsync();

        var builder = CreateBuilder(ctx);
        var nodes = await builder.BuildAsync();

        nodes.Should().NotContain(n => n.Url.Contains("/makale/gizlenmis-makale"),
            "yönetim tarafından gizlenmiş post sitemap dışı (Faz 5.3 Karar 11)");
    }
}
