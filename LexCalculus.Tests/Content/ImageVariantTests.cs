using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Infrastructure.Storage;
using LexCalculus.Tests.TestHelpers;
using LexCalculus.Web.Infrastructure.Rendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace LexCalculus.Tests.Content;

/// <summary>
/// Faz 6.8 (#18) — inline görsel responsive variant üretimi (MediaUploadService)
/// ve render-time srcset enrichment (ImageVariantEnricher).
/// </summary>
public sealed class ImageVariantTests : SqlServerTestBase, IDisposable
{
    private readonly string _tempRoot;

    public ImageVariantTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "lexcalc-variant-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); }
        catch { /* test cleanup */ }
    }

    private static byte[] BuildJpegBytes(int width, int height)
    {
        using var img = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                img[x, y] = new Rgba32((byte)(x * 4 % 255), (byte)(y * 4 % 255), 100);
        using var ms = new MemoryStream();
        img.SaveAsJpeg(ms, new JpegEncoder { Quality = 90 });
        return ms.ToArray();
    }

    private MediaUploadService CreateUploadService()
    {
        var ctx = _db.Create();
        var env = new TestWebHostEnvironment(_tempRoot);
        var storage = new LocalDiskMediaStorage(env, NullLogger<LocalDiskMediaStorage>.Instance);
        return new MediaUploadService(
            ctx, storage, new NullActivityLogService(),
            NullLogger<MediaUploadService>.Instance);
    }

    private async Task<int> SeedUserAsync()
    {
        var ctx = _db.Create();
        var user = new ApplicationUser
        {
            UserName = "vuser@x.com", NormalizedUserName = "VUSER@X.COM",
            Email = "vuser@x.com", NormalizedEmail = "VUSER@X.COM",
            FullName = "Variant Test", CreatedAt = DateTime.UtcNow,
            IsActive = true, EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    private string DiskPath(string relative)
        => Path.Combine(_tempRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string VariantRelative(string mainRelative, int width)
    {
        var dir = mainRelative[..mainRelative.LastIndexOf('/')];
        var baseName = Path.GetFileNameWithoutExtension(mainRelative);
        return $"{dir}/{baseName}_{width}.webp";
    }

    [Fact]
    public async Task UploadInline_LargeImage_GeneratesSmallerVariants()
    {
        var svc = CreateUploadService();
        var userId = await SeedUserAsync();

        // 1000x750 — Max 1200 dokunmaz (≤1200), genişlik 1000.
        var jpeg = BuildJpegBytes(1000, 750);
        using var stream = new MemoryStream(jpeg);

        var result = await svc.UploadInlineImageAsync(
            userId, stream, "big.jpg", "image/jpeg", jpeg.LongLength);
        result.Success.Should().BeTrue();

        // 480 ve 800 üretilmeli; 1200 (>1000) üretilmemeli (upscale yok).
        File.Exists(DiskPath(VariantRelative(result.RelativePath!, 480))).Should().BeTrue();
        File.Exists(DiskPath(VariantRelative(result.RelativePath!, 800))).Should().BeTrue();
        File.Exists(DiskPath(VariantRelative(result.RelativePath!, 1200))).Should().BeFalse();
    }

    [Fact]
    public async Task UploadInline_SmallImage_GeneratesNoVariants()
    {
        var svc = CreateUploadService();
        var userId = await SeedUserAsync();

        // 320x240 — tüm variant genişlikleri (480/800/1200) orijinalden büyük → hiçbiri üretilmez.
        var jpeg = BuildJpegBytes(320, 240);
        using var stream = new MemoryStream(jpeg);

        var result = await svc.UploadInlineImageAsync(
            userId, stream, "small.jpg", "image/jpeg", jpeg.LongLength);
        result.Success.Should().BeTrue();

        File.Exists(DiskPath(VariantRelative(result.RelativePath!, 480))).Should().BeFalse();
        File.Exists(DiskPath(VariantRelative(result.RelativePath!, 800))).Should().BeFalse();
        File.Exists(DiskPath(VariantRelative(result.RelativePath!, 1200))).Should().BeFalse();
        // Ana görsel yine de var.
        File.Exists(DiskPath(result.RelativePath!)).Should().BeTrue();
    }

    // ─── ImageVariantEnricher ─────────────────────────────────────────────

    private void TouchFile(string relative)
    {
        var path = DiskPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
    }

    [Fact]
    public void Enrich_InlineImageWithVariants_AddsSrcsetSizesLoading()
    {
        const string baseRel = "uploads/posts/5/inline/abc123";
        TouchFile($"{baseRel}.webp");
        TouchFile($"{baseRel}_480.webp");
        TouchFile($"{baseRel}_800.webp");
        // _1200 yok

        var enricher = new ImageVariantEnricher(new TestWebHostEnvironment(_tempRoot));
        var html = $"<p>Metin</p><img src=\"/{baseRel}.webp\" alt=\"x\">";

        var result = enricher.Enrich(html);

        result.Should().Contain($"/{baseRel}_480.webp 480w");
        result.Should().Contain($"/{baseRel}_800.webp 800w");
        result.Should().NotContain("_1200.webp");
        result.Should().Contain("sizes=");
        result.Should().Contain("loading=\"lazy\"");
    }

    [Fact]
    public void Enrich_NoVariantsOnDisk_LeavesImgUnchanged()
    {
        const string baseRel = "uploads/posts/5/inline/novariant";
        TouchFile($"{baseRel}.webp");   // sadece ana görsel, variant yok

        var enricher = new ImageVariantEnricher(new TestWebHostEnvironment(_tempRoot));
        var html = $"<img src=\"/{baseRel}.webp\">";

        enricher.Enrich(html).Should().NotContain("srcset");
    }

    [Fact]
    public void Enrich_ExternalAndFeaturedImages_Untouched()
    {
        // Harici görsel + featured (inline değil) — ikisi de dokunulmamalı.
        var enricher = new ImageVariantEnricher(new TestWebHostEnvironment(_tempRoot));
        var html = "<img src=\"https://cdn.example.com/x.webp\">"
                 + "<img src=\"/uploads/posts/5/featured.webp\">";

        var result = enricher.Enrich(html);
        result.Should().NotContain("srcset");
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string webRootPath)
        {
            WebRootPath = webRootPath;
            ContentRootPath = webRootPath;
            EnvironmentName = "Testing";
            ApplicationName = "LexCalculus.Tests";
            WebRootFileProvider = new NullFileProvider();
            ContentRootFileProvider = new NullFileProvider();
        }
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
        public string ApplicationName { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; }
    }
}
