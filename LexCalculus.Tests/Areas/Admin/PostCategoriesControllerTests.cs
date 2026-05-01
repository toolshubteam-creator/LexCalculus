using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Areas.Admin;

[Collection("AdminWebHost")]
public class PostCategoriesControllerTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public PostCategoriesControllerTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAdminClient(bool allowAutoRedirect = false)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect
        });
        client.DefaultRequestHeaders.Add("X-Test-User", "kategori-admin@local");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");
        return client;
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            throw new InvalidOperationException($"Antiforgery token not found at {url}");
        return match.Groups[1].Value;
    }

    private async Task RemoveCategoryAsync(string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var c = await ctx.PostCategories.FirstOrDefaultAsync(x => x.Slug == slug);
        if (c is not null)
        {
            ctx.PostCategories.Remove(c);
            await ctx.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Index_AsAdmin_RendersList()
    {
        // Seed çalıştığı için en az 10 kategori olmalı
        using var client = CreateAdminClient(allowAutoRedirect: true);
        var response = await client.GetAsync("/admin/kategoriler");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Kategoriler");
        body.Should().Contain("param-table",
            "kategori tablosu render edilmeli");
    }

    [Fact]
    public async Task Yeni_Get_RendersEmptyForm()
    {
        using var client = CreateAdminClient(allowAutoRedirect: true);
        var response = await client.GetAsync("/admin/kategoriler/yeni");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Yeni Kategori");
        body.Should().Contain("__RequestVerificationToken");
    }

    [Fact]
    public async Task Yeni_Post_Valid_RedirectsToIndex_AndPersists()
    {
        var slug = "ctrl-test-yeni-kat";
        await RemoveCategoryAsync(slug);
        try
        {
            using var client = CreateAdminClient();
            var token = await GetAntiforgeryTokenAsync(client, "/admin/kategoriler/yeni");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("Name", "Ctrl Test Yeni Kat"),
                new KeyValuePair<string, string>("Description", "test"),
                new KeyValuePair<string, string>("DisplayOrder", "99"),
                new KeyValuePair<string, string>("IsActive", "true")
            });
            var response = await client.PostAsync("/admin/kategoriler/yeni", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("/admin/kategoriler");

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var saved = await ctx.PostCategories.FirstOrDefaultAsync(c => c.Slug == slug);
            saved.Should().NotBeNull();
            saved!.Name.Should().Be("Ctrl Test Yeni Kat");
            saved.DisplayOrder.Should().Be(99);
        }
        finally
        {
            await RemoveCategoryAsync(slug);
        }
    }

    [Fact]
    public async Task Yeni_Post_DuplicateName_ShowsError()
    {
        // Seed'deki "İş Hukuku" → slug "is-hukuku" zaten var
        using var client = CreateAdminClient();
        var token = await GetAntiforgeryTokenAsync(client, "/admin/kategoriler/yeni");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Name", "İş Hukuku"),
            new KeyValuePair<string, string>("DisplayOrder", "99"),
            new KeyValuePair<string, string>("IsActive", "true")
        });
        var response = await client.PostAsync("/admin/kategoriler/yeni", form);

        // Validation hatası → 200 OK + form yeniden render
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("zaten mevcut");
    }

    [Fact]
    public async Task Duzenle_Post_Valid_UpdatesCategory()
    {
        var slug = "ctrl-test-duzenle-kat";
        await RemoveCategoryAsync(slug);
        // önce yarat
        int categoryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var c = new PostCategory
            {
                Name = "Ctrl Test Düzenle Kat", Slug = slug,
                DisplayOrder = 50, IsActive = true, CreatedAt = DateTime.UtcNow
            };
            ctx.PostCategories.Add(c);
            await ctx.SaveChangesAsync();
            categoryId = c.Id;
        }

        try
        {
            using var client = CreateAdminClient();
            var token = await GetAntiforgeryTokenAsync(client, $"/admin/kategoriler/{categoryId}/duzenle");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("Id", categoryId.ToString()),
                new KeyValuePair<string, string>("Name", "Ctrl Test Düzenle Kat"),
                new KeyValuePair<string, string>("Description", "yeni açıklama"),
                new KeyValuePair<string, string>("DisplayOrder", "55"),
                new KeyValuePair<string, string>("IsActive", "true")
            });
            var response = await client.PostAsync($"/admin/kategoriler/{categoryId}/duzenle", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var updated = await ctx.PostCategories.AsNoTracking()
                .FirstAsync(c => c.Id == categoryId);
            updated.Description.Should().Be("yeni açıklama");
            updated.DisplayOrder.Should().Be(55);
        }
        finally
        {
            await RemoveCategoryAsync(slug);
        }
    }

    [Fact]
    public async Task Index_AsAnonymous_Redirects()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/admin/kategoriler");

        // Production cookie auth → 302; TestAuthHandler → 401
        ((int)response.StatusCode).Should().BeOneOf(
            (int)HttpStatusCode.Unauthorized,
            (int)HttpStatusCode.Redirect);
    }
}
