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

    /// <summary>Render edilecek şablon adı (Views/Emails/{TemplateName}.cshtml).</summary>
    public string TemplateName { get; set; } = "TestEmail";

    public string ActiveProvider { get; set; } = "";

    /// <summary>Admin smoke için seçilebilir şablonlar (değer = view adı).</summary>
    public static readonly IReadOnlyList<(string Value, string Label)> AvailableTemplates = new[]
    {
        ("TestEmail", "Test E-postası"),
        ("Connection", "Bağlantı İsteği / Kabul"),
        ("Comment", "Makale Yorumu"),
        ("ContentReport", "İçerik Moderasyon Bildirimi"),
        ("MessageDigest", "Mesaj Dijesti"),
    };
}
