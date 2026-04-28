using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Dashboard";
        return View();
    }
}
