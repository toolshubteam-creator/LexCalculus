using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Models.Seo;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Controllers;

[Route("hesapla")]
public class HesaplaController : Controller
{
    private readonly ICalculatorRegistry _registry;

    public HesaplaController(ICalculatorRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// /hesapla — catalog of all calculators grouped by category.
    /// </summary>
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Hesaplama Araçları";
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = "Hukuki Hesaplama Araçları — Lex Calculus",
            Description = "Türkiye hukuk sisteminde sıkça başvurulan hesaplamalar: kıdem tazminatı, faiz, destekten yoksun kalma ve daha fazlası.",
            Keywords = "hukuki hesaplama, hesaplama araçları, kıdem tazminatı, faiz hesabı"
        };

        var categories = _registry.GetActiveCategories()
            .Select(cat => new { Category = cat, Tools = _registry.GetByCategory(cat) })
            .ToList();

        ViewData["Categories"] = categories;
        return View();
    }

    /// <summary>
    /// /hesapla/{categorySlug} — landing page for a category.
    /// </summary>
    [HttpGet("{categorySlug}")]
    public IActionResult Category(string categorySlug)
    {
        var category = CalculatorCategoryExtensions.FromSlug(categorySlug);
        if (category is null) return NotFound();

        var tools = _registry.GetByCategory(category.Value);
        if (tools.Count == 0) return NotFound();

        var displayName = category.Value.ToDisplayName();

        ViewData["Title"] = displayName;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{displayName} Hesaplama Araçları — Lex Calculus",
            Description = $"{displayName} alanında hukuki hesaplama araçları: " +
                string.Join(", ", tools.Take(3).Select(t => t.Title)) + " ve daha fazlası.",
            OgType = "website"
        };

        ViewData["Category"] = category.Value;
        ViewData["CategoryDisplayName"] = displayName;
        ViewData["Tools"] = tools;

        return View();
    }

    /// <summary>
    /// /hesapla/{categorySlug}/{toolSlug} — individual tool page.
    /// In Phase 2.4 this just shows the metadata + "coming soon" notice.
    /// In Phase 2.5+ each real calculator will override this with its own action.
    /// </summary>
    [HttpGet("{categorySlug}/{toolSlug}")]
    public IActionResult Tool(string categorySlug, string toolSlug)
    {
        var category = CalculatorCategoryExtensions.FromSlug(categorySlug);
        if (category is null) return NotFound();

        var meta = _registry.Find(category.Value, toolSlug);
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords.DefaultIfEmpty(meta.Title))
        };

        ViewData["Meta"] = meta;
        return View();
    }
}
