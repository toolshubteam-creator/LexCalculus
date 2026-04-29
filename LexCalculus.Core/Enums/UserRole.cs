namespace LexCalculus.Core.Enums;

/// <summary>
/// Application-level user roles. These values are seeded into the ASP.NET Identity
/// AspNetRoles table as strings via <c>ToString()</c> during database initialization.
/// Faz 3.6: Premium ve Free kaldırıldı; default rol artık <see cref="Kullanici"/>.
/// </summary>
public enum UserRole
{
    Admin = 1,
    Editor = 2,
    Kullanici = 3
}
