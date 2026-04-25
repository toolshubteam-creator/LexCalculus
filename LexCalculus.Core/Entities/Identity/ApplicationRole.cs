using Microsoft.AspNetCore.Identity;

namespace LexCalculus.Core.Entities.Identity;

/// <summary>
/// Application role — extends ASP.NET Identity role with int primary key
/// and human-readable description.
/// </summary>
public class ApplicationRole : IdentityRole<int>
{
    /// <summary>
    /// Optional admin-facing description of what this role grants.
    /// Not shown to end users.
    /// </summary>
    public string? Description { get; set; }
}
