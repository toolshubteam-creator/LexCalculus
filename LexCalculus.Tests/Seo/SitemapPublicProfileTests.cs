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

public class SitemapPublicProfileTests : SqlServerTestBase
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
        await using var ctx = _db.Create();

        var u1 = MakeUser("1", "u1@x.com", isActive: true);
        var u2 = MakeUser("2", "u2@x.com", isActive: true);
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        ctx.UserProfiles.AddRange(
            new UserProfile
            {
                UserId = u1.Id, DisplayName = "Public User",
                PublicSlug = "public-user", IsPublicProfile = true
            },
            new UserProfile
            {
                UserId = u2.Id, DisplayName = "Private User",
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
        await using var ctx = _db.Create();
        var user = MakeUser("99", "inactive@x.com", isActive: false);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id, DisplayName = "Inactive Public",
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
        await using var ctx = _db.Create();
        var user = MakeUser("50", "noslug@x.com", isActive: true);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id, DisplayName = "No Slug",
            PublicSlug = null, IsPublicProfile = true
        });
        await ctx.SaveChangesAsync();

        var builder = CreateBuilder(ctx);
        var nodes = await builder.BuildAsync();

        nodes.Should().NotContain(n => n.Url.Contains("/uye/"),
            "PublicSlug=null kullanıcı sitemap'te yer almamalı");
    }

    // IDENTITY_INSERT fix (Adım 5.8 P2): explicit Id atamak yerine EF'in
    // ürettiği Id'yi kullan. UserName/Email her test içinde unique.
    private static ApplicationUser MakeUser(string suffix, string email, bool isActive) => new()
    {
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        FullName = $"User {suffix}",
        CreatedAt = DateTime.UtcNow,
        IsActive = isActive,
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };
}
