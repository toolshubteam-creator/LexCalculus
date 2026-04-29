using LexCalculus.Core.Services;

namespace LexCalculus.Web.Areas.Admin.Models.Users;

public sealed class UserListPageViewModel
{
    public required UserListPage Result { get; init; }
    public required UserListFilterViewModel Filter { get; init; }
}
