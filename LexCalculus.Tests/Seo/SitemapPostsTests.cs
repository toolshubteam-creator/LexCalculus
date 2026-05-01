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

public class SitemapPostsTests
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

    private static ApplicationUser MakeUser(int id, string email, bool isActive) => new()
    {
        Id = id,
        UserName = email, NormalizedUserName = email.ToUpperInvariant(),
        Email = email, NormalizedEmail = email.ToUpperInvariant(),
        FullName = email, CreatedAt = DateTime.UtcNow,
        IsActive = isActive, EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static UserPost MakePost(int id, int userId, int catId, string slug, bool isPublished) => new()
    {
        Id = id, UserId = userId, CategoryId = catId,
        Title = $"Post {id}", Slug = slug, Body = "<p>x</p>",
        IsPublished = isPublished,
        PublishedAt = isPublished ? DateTime.UtcNow : null,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Sitemap_IncludesPublishedPostsWithPublicAuthor()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Users.Add(MakeUser(1, "pub@x.com", isActive: true));
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = 1, DisplayName = "Pub", PublicSlug = "pub-yazar",
            IsPublicProfile = true
        });
        ctx.PostCategories.Add(new PostCategory
        {
            Id = 1, Name = "İş", Slug = "is", DisplayOrder = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        ctx.UserPosts.Add(MakePost(10, 1, 1, "is-makalesi", isPublished: true));
        await ctx.SaveChangesAsync();

        var builder = CreateBuilder(ctx);
        var nodes = await builder.BuildAsync();

        nodes.Should().Contain(n =>
            n.Url == "https://test.lexcalculus.com/uye/pub-yazar/makale/is-makalesi");
    }

    [Fact]
    public async Task Sitemap_ExcludesDraftPosts()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Users.Add(MakeUser(1, "drf@x.com", isActive: true));
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = 1, DisplayName = "Drf", PublicSlug = "drf-yazar",
            IsPublicProfile = true
        });
        ctx.PostCategories.Add(new PostCategory
        {
            Id = 1, Name = "İş", Slug = "is", DisplayOrder = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        ctx.UserPosts.Add(MakePost(20, 1, 1, "taslak-makale", isPublished: false));
        await ctx.SaveChangesAsync();

        var builder = CreateBuilder(ctx);
        var nodes = await builder.BuildAsync();

        nodes.Should().NotContain(n => n.Url.Contains("/makale/taslak-makale"),
            "yayında olmayan post sitemap'te olmamalı");
    }

    [Fact]
    public async Task Sitemap_ExcludesPostsWithPrivateAuthor()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Users.Add(MakeUser(1, "priv@x.com", isActive: true));
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = 1, DisplayName = "Priv", PublicSlug = "priv-yazar",
            IsPublicProfile = false   // gizli profil
        });
        ctx.PostCategories.Add(new PostCategory
        {
            Id = 1, Name = "İş", Slug = "is", DisplayOrder = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        ctx.UserPosts.Add(MakePost(30, 1, 1, "gizli-yazar-yayinda", isPublished: true));
        await ctx.SaveChangesAsync();

        var builder = CreateBuilder(ctx);
        var nodes = await builder.BuildAsync();

        nodes.Should().NotContain(n => n.Url.Contains("/makale/gizli-yazar-yayinda"),
            "gizli profil yazarın yayındaki makalesi sitemap dışı (charter §G-a)");
    }

    [Fact]
    public async Task Sitemap_ExcludesPostsWithInactiveAuthor()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Users.Add(MakeUser(1, "inact@x.com", isActive: false));
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = 1, DisplayName = "Inact", PublicSlug = "inact-yazar",
            IsPublicProfile = true
        });
        ctx.PostCategories.Add(new PostCategory
        {
            Id = 1, Name = "İş", Slug = "is", DisplayOrder = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        ctx.UserPosts.Add(MakePost(40, 1, 1, "pasif-yazar-makale", isPublished: true));
        await ctx.SaveChangesAsync();

        var builder = CreateBuilder(ctx);
        var nodes = await builder.BuildAsync();

        nodes.Should().NotContain(n => n.Url.Contains("/makale/pasif-yazar-makale"));
    }
}
