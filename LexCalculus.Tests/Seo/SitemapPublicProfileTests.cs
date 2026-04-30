using FluentAssertions;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Infrastructure.Seo;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LexCalculus.Tests.Seo;

public class SitemapPublicProfileTests
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

    [Fact]
    public async Task Sitemap_IncludesPublicProfileUrls_AndExcludesPrivate()
    {
        await using var ctx = TestDbContextFactory.Create();

        ctx.Users.AddRange(
            MakeUser(1, "u1@x.com", isActive: true),
            MakeUser(2, "u2@x.com", isActive: true));
        ctx.UserProfiles.AddRange(
            new UserProfile
            {
                UserId = 1, DisplayName = "Public User",
                PublicSlug = "public-user", IsPublicProfile = true
            },
            new UserProfile
            {
                UserId = 2, DisplayName = "Private User",
                PublicSlug = "private-user", IsPublicProfile = false
            });
        await ctx.SaveChangesAsync();

        var builder = CreateBuilder(ctx);
        var nodes = await builder.BuildAsync();

        nodes.Should().Contain(n => n.Url == "https://test.lexcalculus.com/uye/public-user",
            "IsPublicProfile=true olan kullanıcı sitemap'te bulunmalı");
        nodes.Should().NotContain(n => n.Url == "https://test.lexcalculus.com/uye/private-user",
            "IsPublicProfile=false olan kullanıcı sitemap'te bulunmamalı");
    }

    [Fact]
    public async Task Sitemap_ExcludesInactiveUsers_EvenIfPublic()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Users.Add(MakeUser(99, "inactive@x.com", isActive: false));
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = 99, DisplayName = "Inactive Public",
            PublicSlug = "inactive-public", IsPublicProfile = true
        });
        await ctx.SaveChangesAsync();

        var builder = CreateBuilder(ctx);
        var nodes = await builder.BuildAsync();

        nodes.Should().NotContain(n => n.Url.EndsWith("/uye/inactive-public"),
            "pasifleştirilmiş kullanıcı public olsa bile sitemap'te yer almamalı");
    }

    [Fact]
    public async Task Sitemap_ExcludesProfilesWithNullSlug()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Users.Add(MakeUser(50, "noslug@x.com", isActive: true));
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = 50, DisplayName = "No Slug",
            PublicSlug = null, IsPublicProfile = true
        });
        await ctx.SaveChangesAsync();

        var builder = CreateBuilder(ctx);
        var nodes = await builder.BuildAsync();

        nodes.Should().NotContain(n => n.Url.Contains("/uye/"),
            "PublicSlug=null kullanıcı sitemap'te yer almamalı");
    }

    private static ApplicationUser MakeUser(int id, string email, bool isActive) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        FullName = email,
        CreatedAt = DateTime.UtcNow,
        IsActive = isActive,
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };
}
