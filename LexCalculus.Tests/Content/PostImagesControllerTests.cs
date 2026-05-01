using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace LexCalculus.Tests.Content;

[Collection("AdminWebHost")]
public class PostImagesControllerTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public PostImagesControllerTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAnonClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient CreateAuthClient(int userId, string email)
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-User", email);
        c.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return c;
    }

    private async Task<ApplicationUser> SeedUserAsync(string email)
    {
        await CleanupUserAsync(email);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = new ApplicationUser
        {
            UserName = email, Email = email, FullName = "Inline Tester",
            CreatedAt = DateTime.UtcNow, IsActive = true, EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        var r = await um.CreateAsync(u, "ValidPass123!");
        r.Succeeded.Should().BeTrue();
        return u;
    }

    private async Task CleanupUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var u = await ctx.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (u is null) return;
        var media = await ctx.MediaFiles.Where(m => m.UserId == u.Id).ToListAsync();
        ctx.MediaFiles.RemoveRange(media);
        var profile = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == u.Id);
        if (profile is not null) ctx.UserProfiles.Remove(profile);
        var roles = ctx.UserRoles.Where(ur => ur.UserId == u.Id);
        ctx.UserRoles.RemoveRange(roles);
        ctx.Users.Remove(u);
        await ctx.SaveChangesAsync();
    }

    private static byte[] BuildJpegBytes(int width = 64, int height = 64)
    {
        using var img = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                img[x, y] = new Rgba32((byte)(x * 4 % 255), (byte)(y * 4 % 255), 100);
        using var ms = new MemoryStream();
        img.SaveAsJpeg(ms, new JpegEncoder { Quality = 90 });
        return ms.ToArray();
    }

    /// <summary>
    /// CSRF token alır. Razor Pages cookies token'ı set eder; aynı client
    /// cookie + header ile gönderdiğinde validate olur. /makalelerim her
    /// authenticated user için form içerir.
    /// </summary>
    private static async Task<string> GetCsrfTokenAsync(HttpClient client, string url = "/makalelerim/yeni")
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            throw new InvalidOperationException($"Antiforgery token not found at {url}");
        return match.Groups[1].Value;
    }

    private static MultipartFormDataContent BuildJpegContent(byte[] bytes, string fileName, string contentType)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Upload_Anonymous_ReturnsUnauthorizedOrRedirect()
    {
        using var client = CreateAnonClient();
        var jpeg = BuildJpegBytes();
        var content = BuildJpegContent(jpeg, "x.jpg", "image/jpeg");

        var response = await client.PostAsync("/api/post-images/upload", content);

        // [Authorize] → 401 (TestAuthHandler) veya 302 (production cookie auth)
        ((int)response.StatusCode).Should().BeOneOf(
            (int)HttpStatusCode.Unauthorized,
            (int)HttpStatusCode.Redirect,
            (int)HttpStatusCode.BadRequest); // CSRF fail de gelebilir
    }

    [Fact]
    public async Task Upload_NoFile_ReturnsBadRequest()
    {
        var u = await SeedUserAsync("postimg-empty@example.com");
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!);
            var token = await GetCsrfTokenAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var emptyContent = new MultipartFormDataContent();
            var response = await client.PostAsync("/api/post-images/upload", emptyContent);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task Upload_ValidJpeg_ReturnsOkWithUrl()
    {
        var u = await SeedUserAsync("postimg-valid@example.com");
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!);
            var token = await GetCsrfTokenAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var jpeg = BuildJpegBytes();
            var content = BuildJpegContent(jpeg, "valid.jpg", "image/jpeg");
            var response = await client.PostAsync("/api/post-images/upload", content);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var json = await response.Content.ReadFromJsonAsync<UploadResponse>();
            json.Should().NotBeNull();
            json!.url.Should().NotBeNullOrEmpty();
            json.url.Should().Contain($"uploads/posts/{u.Id}/inline/");
            json.url.Should().EndWith(".webp");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task Upload_InvalidContentType_ReturnsBadRequest()
    {
        var u = await SeedUserAsync("postimg-mime@example.com");
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!);
            var token = await GetCsrfTokenAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 fake content");
            var content = BuildJpegContent(pdfBytes, "doc.pdf", "application/pdf");
            var response = await client.PostAsync("/api/post-images/upload", content);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task Upload_FakeMagicBytes_ReturnsBadRequest()
    {
        var u = await SeedUserAsync("postimg-magic@example.com");
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!);
            var token = await GetCsrfTokenAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            // Geçerli MIME header ama içerik düz metin
            var fake = Encoding.UTF8.GetBytes("Plain text masquerading as image.");
            var content = BuildJpegContent(fake, "fake.jpg", "image/jpeg");
            var response = await client.PostAsync("/api/post-images/upload", content);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task Upload_MissingCsrfToken_ReturnsBadRequest()
    {
        var u = await SeedUserAsync("postimg-csrf@example.com");
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!);
            // CSRF cookie almak için bir GET çek (form sayfası)
            await client.GetAsync("/makalelerim/yeni");
            // Header EKLEMİYORUZ — CSRF validate fail beklenir

            var jpeg = BuildJpegBytes();
            var content = BuildJpegContent(jpeg, "valid.jpg", "image/jpeg");
            var response = await client.PostAsync("/api/post-images/upload", content);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "ValidateAntiForgeryToken token yokken 400 döner");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    private sealed class UploadResponse
    {
        public string url { get; set; } = "";
    }
}
