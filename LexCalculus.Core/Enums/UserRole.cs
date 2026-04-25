namespace LexCalculus.Core.Enums;

/// <summary>
/// Application-level user roles. These values are seeded into the ASP.NET Identity
/// AspNetRoles table as strings via <c>ToString()</c> during database initialization.
/// </summary>
public enum UserRole
{
    Admin = 1,
    Editor = 2,
    Premium = 3,
    Free = 4
}
