using System.Diagnostics;
using LexCalculus.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        ViewData["Title"] = "Ana Sayfa";
        return View();
    }

    public IActionResult Privacy()
    {
        ViewData["Title"] = "Gizlilik Politikası";
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
