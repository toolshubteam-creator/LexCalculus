using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Web.Areas.Admin.Models;

public sealed class TestEmailFormViewModel
{
    [Required(ErrorMessage = "Alıcı e-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    public string ToAddress { get; set; } = "";

    [StringLength(100)]
    public string? RecipientName { get; set; }

    public string Subject { get; set; } = "Lex Calculus — E-posta Testi";

    public string ActiveProvider { get; set; } = "";
}
