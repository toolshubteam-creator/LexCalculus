using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Services;

namespace LexCalculus.Web.Areas.Admin.Models.TenantTalepleri;

public sealed class TenantTalepleriDetailVm
{
    public required TenantRequestDetailDto Detail { get; init; }
    public TenantTalepleriApproveVm Approve { get; init; } = new();
    public TenantTalepleriRejectVm Reject { get; init; } = new();
}

public sealed class TenantTalepleriApproveVm
{
    [Required(ErrorMessage = "Tenant adı zorunlu.")]
    [StringLength(200)]
    [Display(Name = "Final ad")]
    public string FinalName { get; set; } = "";

    [StringLength(100)]
    [RegularExpression(@"^[a-z0-9-]*$",
        ErrorMessage = "Slug sadece küçük harf, rakam ve tire içerebilir.")]
    [Display(Name = "Final slug")]
    public string? FinalSlug { get; set; }
}

public sealed class TenantTalepleriRejectVm
{
    [Required(ErrorMessage = "Red gerekçesi zorunlu.")]
    [StringLength(1000)]
    [Display(Name = "Red gerekçesi")]
    public string RejectionReason { get; set; } = "";
}
