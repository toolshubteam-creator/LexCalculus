using LexCalculus.Core.Services;
using LexCalculus.Web.Areas.Admin.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
[Route("admin/kullanicilar")]
public sealed class UsersController : Controller
{
    private readonly IUserAdminService _users;

    public UsersController(IUserAdminService users)
    {
        _users = users;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] UserListFilterViewModel? filter,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        filter ??= new UserListFilterViewModel();
        const int pageSize = 25;

        var result = await _users.GetUsersAsync(
            page, pageSize,
            roleFilter: string.IsNullOrWhiteSpace(filter.Role) ? null : filter.Role,
            isActiveFilter: filter.IsActive,
            ct: ct);

        var vm = new UserListPageViewModel
        {
            Result = result,
            Filter = filter
        };

        ViewData["Title"] = "Kullanıcılar";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Kullanıcılar", null)
        };

        return View(vm);
    }
}
