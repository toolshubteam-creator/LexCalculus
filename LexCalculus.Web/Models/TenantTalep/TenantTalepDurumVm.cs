using LexCalculus.Core.Services;

namespace LexCalculus.Web.Models.TenantTalep;

public sealed class TenantTalepDurumVm
{
    public TenantRequestDto? ActiveRequest { get; init; }
    public required IReadOnlyList<TenantRequestDto> History { get; init; }
    public bool UserAlreadyInTenant { get; init; }
}
