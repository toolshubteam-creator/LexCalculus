using System.Text;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Infrastructure.Storage;
using LexCalculus.Tests.TestHelpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace LexCalculus.Tests.Content;

/// <summary>
/// MediaUploadService.UploadInlineImageAsync — Quill inline görsel yükleme.
/// AvatarUploadTests pattern reuse. Faz 4.8.
/// </summary>
public sealed class InlineImageUploadTests : IDisposable
{
    private readonly string _tempRoot;

    public InlineImageUploadTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "lexcalc-inline-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); }
        catch { /* test cleanup, swallow */ }
    }

    private (MediaUploadService svc, ApplicationDbContext ctx) CreateService()
    {
        var ctx = TestDbContextFactory.Create();
        var env = new TestWebHostEnvironment(_tempRoot);
        var storage = new LocalDiskMediaStorage(env, NullLogger<LocalDiskMediaStorage>.Instance);
        var svc = new MediaUploadService(
            ctx, storage, new NullActivityLogService(),
            NullLogger<MediaUploadService>.Instance);
        return (svc, ctx);
    }

    private static async Task SeedUserAsync(ApplicationDbContext ctx, int id = 42)
    {
        ctx.Users.Add(new ApplicationUser
        {
            Id = id, UserName = $"u{id}@x.com", NormalizedUserName = $"U{id}@X.COM",
            Email = $"u{id}@x.com", NormalizedEmail = $"U{id}@X.COM",
            FullName = "Inline Test", CreatedAt = DateTime.UtcNow,
            IsActive = true, EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });
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

    [Fact]
    public async Task UploadInline_Valid_StoresInPostsInlineDirectory()
    {
        var (svc, ctx) = CreateService();
        await SeedUserAsync(ctx, 100);

        var jpeg = BuildJpegBytes();
        using var stream = new MemoryStream(jpeg);

        var result = await svc.UploadInlineImageAsync(
            100, stream, "inline.jpg", "image/jpeg", jpeg.LongLength);

        result.Success.Should().BeTrue();
        result.RelativePath.Should().Contain("uploads/posts/100/inline/");
        result.RelativePath.Should().EndWith(".webp");

        var diskPath = Path.Combine(_tempRoot,
            result.RelativePath!.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(diskPath).Should().BeTrue();
    }

    [Fact]
    public async Task UploadInline_OversizedFile_ReturnsError()
    {
        var (svc, ctx) = CreateService();
        await SeedUserAsync(ctx, 101);

        var bigBytes = new byte[6 * 1024 * 1024];
        using var stream = new MemoryStream(bigBytes);

        var result = await svc.UploadInlineImageAsync(
            101, stream, "big.jpg", "image/jpeg", bigBytes.LongLength);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("büyük");
    }

    [Fact]
    public async Task UploadInline_InvalidContentType_ReturnsError()
    {
        var (svc, ctx) = CreateService();
        await SeedUserAsync(ctx, 102);

        var jpeg = BuildJpegBytes();
        using var stream = new MemoryStream(jpeg);

        var result = await svc.UploadInlineImageAsync(
            102, stream, "doc.pdf", "application/pdf", jpeg.LongLength);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("JPG");
    }

    [Fact]
    public async Task UploadInline_FakeMagicBytes_ReturnsError()
    {
        var (svc, ctx) = CreateService();
        await SeedUserAsync(ctx, 103);

        var fakeBytes = Encoding.UTF8.GetBytes("This is plain text, not a real image.");
        using var stream = new MemoryStream(fakeBytes);

        var result = await svc.UploadInlineImageAsync(
            103, stream, "fake.jpg", "image/jpeg", fakeBytes.LongLength);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("uyuşmuyor");
    }

    [Fact]
    public async Task UploadInline_LargeImage_ResizesToMaxDimension()
    {
        var (svc, ctx) = CreateService();
        await SeedUserAsync(ctx, 104);

        // 2400x1800 — her iki boyut da 1200'den büyük; Max mode 1200x900'e indirir
        var jpeg = BuildJpegBytes(2400, 1800);
        using var stream = new MemoryStream(jpeg);

        var result = await svc.UploadInlineImageAsync(
            104, stream, "big.jpg", "image/jpeg", jpeg.LongLength);

        result.Success.Should().BeTrue();

        var diskPath = Path.Combine(_tempRoot,
            result.RelativePath!.Replace('/', Path.DirectorySeparatorChar));
        using var image = await Image.LoadAsync(diskPath);

        image.Width.Should().BeLessThanOrEqualTo(1200, "Max mode 1200x1200 üst sınır");
        image.Height.Should().BeLessThanOrEqualTo(1200);
        // Aspect korundu mu (2400/1800 = 4:3 → 1200/900)
        var aspectIn = 2400.0 / 1800.0;
        var aspectOut = (double)image.Width / image.Height;
        aspectOut.Should().BeApproximately(aspectIn, 0.05);
    }

    [Fact]
    public async Task UploadInline_CreatesMediaFileAuditRow()
    {
        var (svc, ctx) = CreateService();
        await SeedUserAsync(ctx, 105);

        var jpeg = BuildJpegBytes();
        using var stream = new MemoryStream(jpeg);

        var result = await svc.UploadInlineImageAsync(
            105, stream, "test-inline.jpg", "image/jpeg", jpeg.LongLength);
        result.Success.Should().BeTrue();

        var media = await ctx.MediaFiles.AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == 105);
        media.Should().NotBeNull();
        media!.RelativePath.Should().Be(result.RelativePath);
        media.MimeType.Should().Be("image/webp");
        media.OriginalName.Should().Be("test-inline.jpg");
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
