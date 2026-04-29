namespace LexCalculus.Web.Areas.Admin.Models.Users;

public sealed class UserListFilterViewModel
{
    public string? Role { get; set; }
    public bool? IsActive { get; set; }

    public bool HasAnyFilter =>
        !string.IsNullOrWhiteSpace(Role) || IsActive.HasValue;
}
