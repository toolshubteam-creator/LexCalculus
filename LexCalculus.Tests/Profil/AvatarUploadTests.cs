using System.Text;
using FluentAssertions;
using LexCalculus.Core.Entities.Content;
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

namespace LexCalculus.Tests.Profil;

public class AvatarUploadTests : IDisposable
{
    private readonly string _tempRoot;

    public AvatarUploadTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "lexcalc-avatar-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); }
        catch { /* test cleanup, swallow */ }
    }

    private (MediaUploadService svc, ApplicationDbContext ctx, IMediaStorage storage)
        CreateService()
    {
        var ctx = TestDbContextFactory.Create();
        var env = new TestWebHostEnvironment(_tempRoot);
        var storage = new LocalDiskMediaStorage(env, NullLogger<LocalDiskMediaStorage>.Instance);
        var svc = new MediaUploadService(
            ctx, storage, new NullActivityLogService(),
            NullLogger<MediaUploadService>.Instance);
        return (svc, ctx, storage);
    }

    private async Task<ApplicationUser> SeedUserAsync(ApplicationDbContext ctx, int id = 42)
    {
        var user = new ApplicationUser
        {
            Id = id,
            UserName = $"u{id}@x.com",
            NormalizedUserName = $"U{id}@X.COM",
            Email = $"u{id}@x.com",
            NormalizedEmail = $"U{id}@X.COM",
            FullName = "Avatar Test",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(user);
        ctx.UserProfiles.Add(new UserProfile { UserId = id, DisplayName = "Avatar Test" });
        await ctx.SaveChangesAsync();
        return user;
    }

    /// <summary>ImageSharp ile küçük geçerli bir JPEG üretir.</summary>
    private static byte[] BuildJpegBytes(int width = 64, int height = 64)
    {
        using var img = new Image<Rgba32>(width, height);
        // Sade bir gradient — encoder çalışsın yeter
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                img[x, y] = new Rgba32((byte)(x * 4), (byte)(y * 4), 100);

        using var ms = new MemoryStream();
        img.SaveAsJpeg(ms, new JpegEncoder { Quality = 90 });
        return ms.ToArray();
    }

    [Fact]
    public async Task UploadAvatar_RejectsFilesLargerThan5MB()
    {
        var (svc, ctx, _) = CreateService();
        var user = await SeedUserAsync(ctx);

        // 6 MB sahte içerik (gerçek görsel değil — boyut zaten 5MB üstüne takılır)
        var bigBytes = new byte[6 * 1024 * 1024];
        using var stream = new MemoryStream(bigBytes);

        var result = await svc.UploadAvatarAsync(
            user.Id, stream, "big.jpg", "image/jpeg", bigBytes.LongLength);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("büyük");
    }

    [Fact]
    public async Task UploadAvatar_RejectsMimeSpoof_TextAsJpg()
    {
        var (svc, ctx, _) = CreateService();
        var user = await SeedUserAsync(ctx);

        // .txt içeriği uzantı/MIME sahte → magic bytes detection fail eder
        var fakeBytes = Encoding.UTF8.GetBytes("This is not an image, it is plain text content.");
        using var stream = new MemoryStream(fakeBytes);

        var result = await svc.UploadAvatarAsync(
            user.Id, stream, "fake.jpg", "image/jpeg", fakeBytes.LongLength);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("uyuşmuyor");
    }

    [Fact]
    public async Task UploadAvatar_AcceptsValidJpeg_StoresAsWebp()
    {
        var (svc, ctx, storage) = CreateService();
        var user = await SeedUserAsync(ctx);

        var jpegBytes = BuildJpegBytes();
        using var stream = new MemoryStream(jpegBytes);

        var result = await svc.UploadAvatarAsync(
            user.Id, stream, "test.jpg", "image/jpeg", jpegBytes.LongLength);

        result.Success.Should().BeTrue();
        result.RelativePath.Should().NotBeNullOrEmpty();
        result.RelativePath.Should().EndWith(".webp", "ImageSharp WebpEncoder ile yeniden kodlanır");
        result.RelativePath.Should().Contain($"uploads/avatars/{user.Id}/");

        // Dosya gerçekten yazıldı mı?
        var diskPath = Path.Combine(_tempRoot, result.RelativePath!.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(diskPath).Should().BeTrue();
        new FileInfo(diskPath).Length.Should().BeGreaterThan(0);

        // Magic bytes WebP mi?
        var written = await File.ReadAllBytesAsync(diskPath);
        written.Length.Should().BeGreaterThan(12);
        Encoding.ASCII.GetString(written, 0, 4).Should().Be("RIFF");
        Encoding.ASCII.GetString(written, 8, 4).Should().Be("WEBP");
    }

    [Fact]
    public async Task UploadAvatar_DeletesPreviousAvatar()
    {
        var (svc, ctx, storage) = CreateService();
        var user = await SeedUserAsync(ctx);

        // İlk yükleme
        var jpeg1 = BuildJpegBytes();
        using (var s1 = new MemoryStream(jpeg1))
        {
            var r1 = await svc.UploadAvatarAsync(user.Id, s1, "first.jpg", "image/jpeg", jpeg1.LongLength);
            r1.Success.Should().BeTrue();
        }

        var profileAfterFirst = await ctx.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);
        var firstPath = profileAfterFirst.AvatarUrl!;
        var firstDiskPath = Path.Combine(_tempRoot, firstPath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(firstDiskPath).Should().BeTrue();

        // İkinci yükleme
        var jpeg2 = BuildJpegBytes(80, 80);
        using (var s2 = new MemoryStream(jpeg2))
        {
            var r2 = await svc.UploadAvatarAsync(user.Id, s2, "second.jpg", "image/jpeg", jpeg2.LongLength);
            r2.Success.Should().BeTrue();
        }

        // Eski dosya silinmiş olmalı, yeni dosya yerinde
        File.Exists(firstDiskPath).Should().BeFalse(
            "yeni avatar yüklenince eski dosya disk'ten silinmeli (Karar D-a)");

        var profileAfterSecond = await ctx.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);
        profileAfterSecond.AvatarUrl.Should().NotBe(firstPath);
        var secondDiskPath = Path.Combine(_tempRoot, profileAfterSecond.AvatarUrl!.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(secondDiskPath).Should().BeTrue();
    }

    [Fact]
    public async Task UploadAvatar_UpdatesUserProfileAvatarUrlAndCreatesMediaFileRow()
    {
        var (svc, ctx, _) = CreateService();
        var user = await SeedUserAsync(ctx);

        var jpegBytes = BuildJpegBytes();
        using var stream = new MemoryStream(jpegBytes);

        var result = await svc.UploadAvatarAsync(
            user.Id, stream, "test.jpg", "image/jpeg", jpegBytes.LongLength);
        result.Success.Should().BeTrue();

        // UserProfile.AvatarUrl güncellenmiş mi?
        var profile = await ctx.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);
        profile.AvatarUrl.Should().Be(result.RelativePath);

        // MediaFile satırı eklenmiş mi?
        var media = await ctx.MediaFiles.AsNoTracking().FirstOrDefaultAsync(m => m.UserId == user.Id);
        media.Should().NotBeNull();
        media!.RelativePath.Should().Be(result.RelativePath);
        media.MimeType.Should().Be("image/webp");
        media.OriginalName.Should().Be("test.jpg");
        media.SizeBytes.Should().BeGreaterThan(0);
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
