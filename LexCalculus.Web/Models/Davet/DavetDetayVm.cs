using LexCalculus.Core.Services;

namespace LexCalculus.Web.Models.Davet;

public sealed class DavetDetayVm
{
    public required InvitationLookupResult Lookup { get; init; }
    public required string Token { get; init; }

    public bool UserAuthenticated { get; init; }
    public string? CurrentUserEmail { get; init; }
    public bool EmailMatches { get; init; }
    public bool CurrentUserAlreadyInTenant { get; init; }
}
