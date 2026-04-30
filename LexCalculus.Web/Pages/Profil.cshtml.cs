#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using LexCalculus.Core.Common;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Enums;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Web.Pages;

[Authorize]
public class ProfilModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _ctx;
    private readonly IPublicProfileService _publicProfile;
    private readonly ILogger<ProfilModel> _logger;

    public ProfilModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext ctx,
        IPublicProfileService publicProfile,
        ILogger<ProfilModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _ctx = ctx;
        _publicProfile = publicProfile;
        _logger = logger;
    }

    public string Email { get; set; } = "";

    /// <summary>
    /// View'da ShowTenant toggle'ını koşullu render etmek için.
    /// </summary>
    public bool HasTenant { get; set; }

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

        // Faz 4.1 P1/3 — public profile alanları
        [Display(Name = "Profilim kamuya açık")]
        public bool IsPublicProfile { get; set; }

        [Display(Name = "Hukuk büromu profilimde göster")]
        public bool ShowTenant { get; set; }

        [StringLength(100)]
        [Display(Name = "Profil URL kısayolu")]
        public string? PublicSlug { get; set; }

        [StringLength(2000)]
        [Display(Name = "Hakkınızda")]
        public string? Bio { get; set; }

        [StringLength(80)]
        [Display(Name = "Şehir")]
        public string? City { get; set; }
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

        Input = new InputModel
        {
            FullName = user.FullName ?? "",
            MeslekTuru = profile.MeslekTuru,
            MeslekTuruDiger = profile.MeslekTuruDiger,
            BaroNo = profile.BaroNo,
            PhoneNumber = user.PhoneNumber,
            NotificationsEmailEnabled = user.NotificationsEmailEnabled,
            IsPublicProfile = profile.IsPublicProfile,
            ShowTenant = profile.ShowTenant && HasTenant,
            PublicSlug = profile.PublicSlug,
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

        // Slug yönetimi (Faz 4.1 P1/3 fix — server-side otomatik slugify):
        // Kullanıcı girdisi her zaman SlugHelper üzerinden temizlenir; sert regex
        // validation yerine kullanıcı dostu input (Türkçe karakter, büyük harf, boşluk)
        // kabul edilir. Çakışma hâlâ kullanıcıya bildirilir (otomatik suffix kullanmıyoruz —
        // kullanıcı kendi slug'ını seçmek isteyebilir).
        var sanitizedInput = SlugHelper.Generate(Input.PublicSlug?.Trim());

        if (Input.IsPublicProfile)
        {
            if (!string.IsNullOrEmpty(sanitizedInput))
            {
                if (sanitizedInput != profile.PublicSlug)
                {
                    if (await _publicProfile.IsSlugTakenAsync(sanitizedInput, user.Id, ct))
                    {
                        // Razor Pages binder PublicSlug field'ını "Input.PublicSlug" anahtarıyla
                        // bağlar; asp-validation-for="Input.PublicSlug" tag helper bu key'i arar.
                        // nameof(Input.PublicSlug) sadece "PublicSlug" döndürür, prefix uyumsuzluğu
                        // nedeniyle field-altı span boş kalırdı (Faz 4.1 P1/3 fix-2).
                        ModelState.AddModelError("Input.PublicSlug",
                            "Bu profil URL'i başka bir kullanıcı tarafından kullanılıyor.");
                        return Page();
                    }
                    profile.PublicSlug = sanitizedInput;
                }
                // sanitize sonrası mevcut slug ile aynı → dokunma
            }
            else if (string.IsNullOrEmpty(profile.PublicSlug))
            {
                // İlk defa public açılıyor + kullanıcı slug yazmadı (veya yazdığı tamamen
                // sembolden ibaretti) → DisplayName tabanlı otomatik üret.
                profile.PublicSlug = await _publicProfile.GenerateUniquePublicSlugAsync(
                    profile.DisplayName, user.Id, ct);
            }
            // else: kullanıcı boş bıraktı ve mevcut slug var → koru
        }
        else
        {
            // Profil gizli: slug korunur (re-enable için). Kullanıcı yeni bir şey yazdıysa
            // (sanitized) güncelle, çakışma kontrolü yine yapılır.
            if (!string.IsNullOrEmpty(sanitizedInput) && sanitizedInput != profile.PublicSlug)
            {
                if (await _publicProfile.IsSlugTakenAsync(sanitizedInput, user.Id, ct))
                {
                    ModelState.AddModelError("Input.PublicSlug",
                        "Bu profil URL'i başka bir kullanıcı tarafından kullanılıyor.");
                    return Page();
                }
                profile.PublicSlug = sanitizedInput;
            }
        }

        await _ctx.SaveChangesAsync(ct);

        await _signInManager.RefreshSignInAsync(user);

        StatusMessage = "Profiliniz güncellendi.";
        return RedirectToPage();
    }
}
