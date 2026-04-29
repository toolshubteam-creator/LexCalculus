// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using LexCalculus.Core.Email;
using LexCalculus.Core.Email.Models;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Web.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateRenderer _emailRenderer;
        private readonly ApplicationDbContext _ctx;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailService emailService,
            IEmailTemplateRenderer emailRenderer,
            ApplicationDbContext ctx)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailService = emailService;
            _emailRenderer = emailRenderer;
            _ctx = ctx;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "E-posta zorunludur.")]
            [EmailAddress]
            [Display(Name = "E-posta")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Ad-Soyad zorunludur.")]
            [StringLength(100)]
            [Display(Name = "Ad-Soyad")]
            public string FullName { get; set; } = "";

            [Required]
            [StringLength(100, ErrorMessage = "{0} en az {2} en fazla {1} karakter olmalı.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Şifre")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Şifre Tekrar")]
            [Compare("Password", ErrorMessage = "Şifre ve şifre tekrarı eşleşmiyor.")]
            public string ConfirmPassword { get; set; }

            [Display(Name = "Mesleğiniz")]
            public MeslekTuru? MeslekTuru { get; set; }

            [StringLength(50)]
            [Display(Name = "Mesleğinizi yazınız")]
            public string MeslekTuruDiger { get; set; }

            [StringLength(50)]
            [Display(Name = "Baro/Sicil Numarası")]
            public string BaroNo { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid)
                return Page();

            // MeslekTuru = Diger validation
            if (Input.MeslekTuru == LexCalculus.Core.Enums.MeslekTuru.Diger
                && string.IsNullOrWhiteSpace(Input.MeslekTuruDiger))
            {
                ModelState.AddModelError(nameof(Input.MeslekTuruDiger),
                    "Diğer seçtiğinizde mesleğinizi yazmanız gerekir.");
                return Page();
            }

            // 1. ApplicationUser oluştur
            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                FullName = Input.FullName.Trim(),
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                NotificationsEmailEnabled = true
            };

            var createResult = await _userManager.CreateAsync(user, Input.Password);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }

            _logger.LogInformation("User created with email {Email}.", Input.Email);

            // 2. Kullanici rolü ata
            var roleResult = await _userManager.AddToRoleAsync(user, "Kullanici");
            if (!roleResult.Succeeded)
            {
                // Kritik bug — rol seed edilmemiş. User'ı geri al.
                await _userManager.DeleteAsync(user);
                _logger.LogError("Kullanici rolü atanamadı. Errors: {Errors}",
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                ModelState.AddModelError(string.Empty, "Sistem hatası: rol ataması başarısız. Lütfen yöneticiye bildirin.");
                return Page();
            }

            // 3. UserProfile oluştur (transaction safety: profile fail olursa user rollback)
            try
            {
                var profile = new UserProfile
                {
                    UserId = user.Id,
                    DisplayName = Input.FullName.Trim(),
                    BaroNo = string.IsNullOrWhiteSpace(Input.BaroNo) ? null : Input.BaroNo.Trim(),
                    MeslekTuru = Input.MeslekTuru,
                    MeslekTuruDiger = Input.MeslekTuru == LexCalculus.Core.Enums.MeslekTuru.Diger
                        ? Input.MeslekTuruDiger?.Trim()
                        : null
                };

                _ctx.UserProfiles.Add(profile);
                await _ctx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UserProfile oluşturma hatası, user {UserId} geri alınıyor.", user.Id);
                await _userManager.DeleteAsync(user);
                ModelState.AddModelError(string.Empty, "Profil oluşturma sırasında bir hata oluştu. Lütfen tekrar deneyin.");
                return Page();
            }

            // 4. Email confirmation token + URL
            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId, code, returnUrl },
                protocol: Request.Scheme);

            // 5. EmailConfirmation şablon ile gönder
            try
            {
                var siteUrl = $"{Request.Scheme}://{Request.Host}";
                var model = new EmailConfirmationModel
                {
                    DisplayName = Input.FullName.Trim(),
                    ConfirmationUrl = callbackUrl ?? siteUrl,
                    SiteUrl = siteUrl
                };

                var html = await _emailRenderer.RenderAsync("EmailConfirmation", model);
                var emailMessage = new EmailMessage(
                    ToAddress: Input.Email,
                    ToDisplayName: Input.FullName.Trim(),
                    Subject: "Lex Calculus — E-posta Adresinizi Doğrulayın",
                    HtmlBody: html);

                await _emailService.SendAsync(emailMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Confirmation email gönderilemedi: {Email}", Input.Email);
                // User+profile zaten oluşturuldu — kullanıcı manuel "Yeniden gönder" yapabilir
                // veya admin tarafından aktive edilebilir. Kayıt akışını bozma.
            }

            // 6. Redirect: RegisterConfirmation (Parça 2b-iii'te oluşacak)
            if (_userManager.Options.SignIn.RequireConfirmedAccount)
            {
                return RedirectToPage("RegisterConfirmation",
                    new { email = Input.Email, returnUrl });
            }

            try
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
            }
            catch (Exception ex)
            {
                // Test ortamında IAuthenticationSignInHandler eksik olabilir; auto sign-in'i atla.
                _logger.LogWarning(ex, "Auto sign-in atlandı (kullanıcı oluştu, manuel login bekleniyor): {Email}",
                    Input.Email);
            }
            return LocalRedirect(returnUrl);
        }
    }
}
