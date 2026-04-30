using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Web.Models.Davet;

public sealed class DavetKayitVm
{
    public string Token { get; set; } = "";

    /// <summary>Davet email'i — readonly, server-side verify edilir.</summary>
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Ad-Soyad zorunlu.")]
    [StringLength(100)]
    [Display(Name = "Ad-Soyad")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Şifre zorunlu.")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalı.")]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = "";

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Şifre ve şifre tekrarı eşleşmiyor.")]
    [Display(Name = "Şifre Tekrar")]
    public string ConfirmPassword { get; set; } = "";
}
