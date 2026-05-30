#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Enums;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Core.Services;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LexCalculus.Web.Pages;

[Authorize]
public class ProfilModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _ctx;
    private readonly IPublicProfileService _publicProfile;
    private readonly IMediaUploadService _mediaUpload;
    private readonly IMediaStorage _mediaStorage;
    private readonly SeoSettings _seo;
    private readonly ILogger<ProfilModel> _logger;

    public ProfilModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext ctx,
        IPublicProfileService publicProfile,
        IMediaUploadService mediaUpload,
        IMediaStorage mediaStorage,
        IOptions<SeoSettings> seoOptions,
        ILogger<ProfilModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _ctx = ctx;
        _publicProfile = publicProfile;
        _mediaUpload = mediaUpload;
        _mediaStorage = mediaStorage;
        _seo = seoOptions.Value ?? new SeoSettings();
        _logger = logger;
    }

    public string Email { get; set; } = "";

    /// <summary>
    /// View'da ShowTenant toggle'ını koşullu render etmek için.
    /// </summary>
    public bool HasTenant { get; set; }

    /// <summary>
    /// Mevcut avatarın public URL'i (varsa). View'da preview render etmek için.
    /// </summary>
    public string? CurrentAvatarUrl { get; set; }

    /// <summary>
    /// Kullanıcının public profil URL'i (read-only). PublicSlug null ise null —
    /// view bilgi satırını koşullu render eder. SiteUrl varsa absolute, yoksa
    /// relative. Faz 4.1 P3-fix tasarım kararı (slug değiştirilemez ama görünür,
    /// LinkedIn pattern).
    /// </summary>
    public string? CurrentProfileUrl { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Ad-Soyad zorunludur.")]
        [StringLength(100)]
        [Display(Name = "Ad-Soyad")]
        public string FullName { get; set; } = "";

        [Display(Name = "Mesleğiniz")]
        public MeslekTuru? MeslekTuru { get; set; }

        [StringLength(50)]
        [Display(Name = "Mesleğinizi yazınız")]
        public string? MeslekTuruDiger { get; set; }

        [StringLength(50)]
        [Display(Name = "Baro/Sicil Numarası")]
        public string? BaroNo { get; set; }

        [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
        [StringLength(20)]
        [Display(Name = "Telefon")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "E-posta bildirimleri")]
        public bool NotificationsEmailEnabled { get; set; }

        // Faz 6.2 P2 — granüler e-posta tercihleri (master açıkken geçerli)
        [Display(Name = "Bağlantı istekleri")]
        public bool EmailOnConnection { get; set; }

        [Display(Name = "Makale yorumları")]
        public bool EmailOnComment { get; set; }

        [Display(Name = "İçerik moderasyon kararları")]
        public bool EmailOnContentReport { get; set; }

        [Display(Name = "Mesaj özetleri")]
        public bool EmailOnMessageDigest { get; set; }

        // Faz 4.1 P1/3 — public profile alanları
        [Display(Name = "Profilim kamuya açık")]
        public bool IsPublicProfile { get; set; }

        [Display(Name = "Hukuk büromu profilimde göster")]
        public bool ShowTenant { get; set; }

        [Display(Name = "Bağlantı listemi profilimde göster")]
        public bool ShowConnections { get; set; }

        [StringLength(2000)]
        [Display(Name = "Hakkınızda")]
        public string? Bio { get; set; }

        [StringLength(80)]
        [Display(Name = "Şehir")]
        public string? City { get; set; }

        // Faz 4.1 P2/3 — avatar yükleme. ASP.NET binder boş file için null bırakır.
        [Display(Name = "Profil Fotoğrafı")]
        public IFormFile? AvatarFile { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        Email = user.Email ?? "";
        HasTenant = user.TenantId.HasValue;

        var profile = await _ctx.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile == null)
        {
            profile = new UserProfile
            {
                UserId = user.Id,
                DisplayName = user.FullName ?? user.Email ?? "Kullanıcı"
            };
            _ctx.UserProfiles.Add(profile);
            await _ctx.SaveChangesAsync();
        }

        CurrentAvatarUrl = string.IsNullOrEmpty(profile.AvatarUrl)
            ? null
            : _mediaStorage.GetPublicUrl(profile.AvatarUrl);

        CurrentProfileUrl = BuildProfileUrl(profile.PublicSlug);

        Input = new InputModel
        {
            FullName = user.FullName ?? "",
            MeslekTuru = profile.MeslekTuru,
            MeslekTuruDiger = profile.MeslekTuruDiger,
            BaroNo = profile.BaroNo,
            PhoneNumber = user.PhoneNumber,
            NotificationsEmailEnabled = user.NotificationsEmailEnabled,
            EmailOnConnection = profile.EmailOnConnection,
            EmailOnComment = profile.EmailOnComment,
            EmailOnContentReport = profile.EmailOnContentReport,
            EmailOnMessageDigest = profile.EmailOnMessageDigest,
            IsPublicProfile = profile.IsPublicProfile,
            ShowTenant = profile.ShowTenant && HasTenant,
            ShowConnections = profile.ShowConnections,
            Bio = profile.Bio,
            City = profile.City
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        Email = user.Email ?? "";
        HasTenant = user.TenantId.HasValue;

        // CurrentAvatarUrl Page() return path'lerinde view'da görünmesi için reload edilir
        // (en sondaki SaveChanges sonrası reset edilebilir).
        var currentProfile = await _ctx.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        CurrentAvatarUrl = string.IsNullOrEmpty(currentProfile?.AvatarUrl)
            ? null
            : _mediaStorage.GetPublicUrl(currentProfile.AvatarUrl);

        CurrentProfileUrl = BuildProfileUrl(currentProfile?.PublicSlug);

        // Avatar yükleme — kullanıcı dosya seçmediyse sessiz atla, mevcut avatar korunur.
        if (Input.AvatarFile is { Length: > 0 })
        {
            await using var avatarStream = Input.AvatarFile.OpenReadStream();
            var uploadResult = await _mediaUpload.UploadAvatarAsync(
                user.Id,
                avatarStream,
                Input.AvatarFile.FileName,
                Input.AvatarFile.ContentType,
                Input.AvatarFile.Length,
                ct);

            if (!uploadResult.Success)
            {
                ModelState.AddModelError("Input.AvatarFile",
                    uploadResult.ErrorMessage ?? "Avatar yüklenemedi.");
                return Page();
            }

            // Başarılı yükleme — preview'i güncelle (Page() return olmasa da
            // RedirectToPage() ileride OnGet'i çağıracağı için zaten yenilenir; ama
            // defansif olarak eldeki referansı güncel tut).
            CurrentAvatarUrl = _mediaStorage.GetPublicUrl(uploadResult.RelativePath!);
        }

        if (!ModelState.IsValid)
        {
            // Sessiz fail tanısı için (Faz 4.1 P1/3 fix): hangi field hangi hata.
            var errors = ModelState
                .Where(kv => kv.Value?.Errors.Count > 0)
                .Select(kv => $"{kv.Key}: {string.Join("; ", kv.Value!.Errors.Select(e => e.ErrorMessage))}")
                .ToList();
            _logger.LogWarning("Profil POST ModelState invalid for user {UserId}: {Errors}",
                user.Id, string.Join(" | ", errors));
            return Page();
        }

        if (Input.MeslekTuru == LexCalculus.Core.Enums.MeslekTuru.Diger
            && string.IsNullOrWhiteSpace(Input.MeslekTuruDiger))
        {
            ModelState.AddModelError("Input.MeslekTuruDiger",
                "Diğer seçtiğinizde mesleğinizi yazmanız gerekir.");
            return Page();
        }

        user.FullName = Input.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(Input.PhoneNumber) ? null : Input.PhoneNumber.Trim();
        user.NotificationsEmailEnabled = Input.NotificationsEmailEnabled;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        var profile = await _ctx.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile == null)
        {
            profile = new UserProfile
            {
                UserId = user.Id,
                DisplayName = Input.FullName.Trim()
            };
            _ctx.UserProfiles.Add(profile);
        }
        else
        {
            profile.DisplayName = Input.FullName.Trim();
        }

        profile.MeslekTuru = Input.MeslekTuru;
        profile.MeslekTuruDiger = Input.MeslekTuru == LexCalculus.Core.Enums.MeslekTuru.Diger
            ? Input.MeslekTuruDiger?.Trim()
            : null;
        profile.BaroNo = string.IsNullOrWhiteSpace(Input.BaroNo) ? null : Input.BaroNo.Trim();

        // Faz 4.1 P1/3 — public profil alanları
        profile.Bio = string.IsNullOrWhiteSpace(Input.Bio) ? null : Input.Bio.Trim();
        profile.City = string.IsNullOrWhiteSpace(Input.City) ? null : Input.City.Trim();
        profile.IsPublicProfile = Input.IsPublicProfile;
        // ShowTenant defansif: kullanıcı tenant üyesi değilse her zaman false
        profile.ShowTenant = HasTenant && Input.ShowTenant;
        profile.ShowConnections = Input.ShowConnections;

        // Faz 6.2 P2 — granüler e-posta tercihleri (master = user.NotificationsEmailEnabled)
        profile.EmailOnConnection = Input.EmailOnConnection;
        profile.EmailOnComment = Input.EmailOnComment;
        profile.EmailOnContentReport = Input.EmailOnContentReport;
        profile.EmailOnMessageDigest = Input.EmailOnMessageDigest;

        // Slug yönetimi (Faz 4.1 P2-fix — Yaklaşım 4 görünmez kimlik):
        // Slug kayıt anında EnsureProfileExistsAsync ile üretilir, kullanıcı UI'da
        // görmez/değiştirmez. Bu blok sadece eski kayıtlar için defansif fallback —
        // mevcut PublicSlug null ise (P1/3 öncesinde oluşturulmuş profile) DisplayName
        // tabanlı otomatik üret.
        if (string.IsNullOrEmpty(profile.PublicSlug))
        {
            profile.PublicSlug = await _publicProfile.GenerateUniquePublicSlugAsync(
                profile.DisplayName, user.Id, ct);
        }

        await _ctx.SaveChangesAsync(ct);

        await _signInManager.RefreshSignInAsync(user);

        StatusMessage = "Profiliniz güncellendi.";
        return RedirectToPage();
    }

    private string? BuildProfileUrl(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var siteUrl = (_seo.SiteUrl ?? "").TrimEnd('/');
        return string.IsNullOrEmpty(siteUrl)
            ? $"/uye/{slug}"
            : $"{siteUrl}/uye/{slug}";
    }
}
