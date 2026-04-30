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

namespace LexCalculus.Web.Pages;

[Authorize]
public class ProfilModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _ctx;
    private readonly IPublicProfileService _publicProfile;

    public ProfilModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext ctx,
        IPublicProfileService publicProfile)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _ctx = ctx;
        _publicProfile = publicProfile;
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
        [RegularExpression(@"^[a-z0-9-]+$",
            ErrorMessage = "Slug sadece küçük harf, rakam ve tire içerebilir.")]
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
            return Page();

        if (Input.MeslekTuru == LexCalculus.Core.Enums.MeslekTuru.Diger
            && string.IsNullOrWhiteSpace(Input.MeslekTuruDiger))
        {
            ModelState.AddModelError(nameof(Input.MeslekTuruDiger),
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

        // Slug yönetimi
        var requestedSlug = string.IsNullOrWhiteSpace(Input.PublicSlug)
            ? null
            : Input.PublicSlug.Trim().ToLowerInvariant();

        if (Input.IsPublicProfile)
        {
            // Profil kamuya açıksa: kullanıcı slug yazdıysa onu validate et,
            // yazmadıysa otomatik üret. Mevcut slug varsa korunur (kullanıcı boş bıraktıysa
            // ve eski slug varsa eski tutulur).
            if (!string.IsNullOrEmpty(requestedSlug))
            {
                if (requestedSlug != profile.PublicSlug)
                {
                    if (await _publicProfile.IsSlugTakenAsync(requestedSlug, user.Id, ct))
                    {
                        ModelState.AddModelError(nameof(Input.PublicSlug),
                            "Bu profil URL'i başka bir kullanıcı tarafından kullanılıyor.");
                        return Page();
                    }
                    profile.PublicSlug = requestedSlug;
                }
                // değişmediyse dokunma
            }
            else if (string.IsNullOrEmpty(profile.PublicSlug))
            {
                // İlk defa public açılıyor, slug üret
                profile.PublicSlug = await _publicProfile.GenerateUniquePublicSlugAsync(
                    profile.DisplayName, user.Id, ct);
            }
            // else: kullanıcı slug'ı boş bıraktı ve mevcut slug var → koru
        }
        else
        {
            // Profil gizli: kullanıcı slug yazdıysa korunur (re-enable için);
            // yazmadıysa eski slug korunur. Hiçbir şey silinmez.
            if (!string.IsNullOrEmpty(requestedSlug) && requestedSlug != profile.PublicSlug)
            {
                if (await _publicProfile.IsSlugTakenAsync(requestedSlug, user.Id, ct))
                {
                    ModelState.AddModelError(nameof(Input.PublicSlug),
                        "Bu profil URL'i başka bir kullanıcı tarafından kullanılıyor.");
                    return Page();
                }
                profile.PublicSlug = requestedSlug;
            }
        }

        await _ctx.SaveChangesAsync(ct);

        await _signInManager.RefreshSignInAsync(user);

        StatusMessage = "Profiliniz güncellendi.";
        return RedirectToPage();
    }
}
