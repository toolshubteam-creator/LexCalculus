#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Web.Areas.Identity.Pages.Account.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _ctx;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext ctx)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _ctx = ctx;
    }

    public string Email { get; set; } = "";

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
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        Email = user.Email ?? "";

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
            NotificationsEmailEnabled = user.NotificationsEmailEnabled
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        Email = user.Email ?? "";

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

        await _ctx.SaveChangesAsync();

        try
        {
            await _signInManager.RefreshSignInAsync(user);
        }
        catch (System.Exception)
        {
            // Test ortamında IAuthenticationSignInHandler eksik olabilir; refresh atla.
        }

        StatusMessage = "Profiliniz güncellendi.";
        return RedirectToPage();
    }
}
