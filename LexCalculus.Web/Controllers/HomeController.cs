using System.Diagnostics;
using LexCalculus.Core.Models.Seo;
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

        ViewData["PageMeta"] = new SeoMeta
        {
            Title = "Lex Calculus — Hukuki Hesaplama Platformu",
            Description = "Türkiye'nin avukat, hâkim ve bilirkişiler için hukuki hesaplama platformu — Anno MMXXVI",
            Keywords = "hukuki hesaplama, kıdem tazminatı, faiz hesaplama, bilirkişi, hukuk, avukat",
            JsonLd = """
            {
              "@context": "https://schema.org",
              "@type": "Organization",
              "name": "Lex Calculus",
              "url": "https://lexcalculus.com",
              "description": "Türkiye'nin hukuki hesaplama platformu",
              "foundingDate": "2026"
            }
            """
        };

        return View();
    }

    public IActionResult Privacy()
    {
        ViewData["Title"] = "Gizlilik Politikası";
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = "Gizlilik Politikası — Lex Calculus",
            Description = "Lex Calculus kişisel veri işleme politikası ve KVKK uyumluluk bildirimi.",
            OgType = "article"
        };
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
