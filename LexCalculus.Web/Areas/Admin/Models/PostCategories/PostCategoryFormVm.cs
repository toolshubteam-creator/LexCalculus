using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Web.Areas.Admin.Models.PostCategories;

public sealed class PostCategoryFormVm
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Kategori adı zorunludur.")]
    [StringLength(80)]
    [Display(Name = "Kategori Adı")]
    public string Name { get; set; } = "";

    [StringLength(500)]
    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Display(Name = "Sıra")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}
