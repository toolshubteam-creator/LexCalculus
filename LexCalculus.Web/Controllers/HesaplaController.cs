using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Calculators.IsHukuku;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Web.Models.Hesapla;
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

        var sections = _registry.GetActiveCategories()
            .Select(cat => new CategorySection
            {
                Category = cat,
                Tools = _registry.GetByCategory(cat)
            })
            .ToList();

        var model = new HesaplaIndexViewModel { Sections = sections };
        return View(model);
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

        var model = new HesaplaCategoryViewModel
        {
            Category = category.Value,
            DisplayName = displayName,
            Tools = tools
        };

        return View(model);
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

    /// <summary>
    /// Kıdem Tazminatı — GET form sayfası.
    /// Spesifik route catch-all Tool action'ından önce match eder.
    /// </summary>
    [HttpGet("is-hukuku/kidem-tazminati")]
    public IActionResult KidemTazminati()
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "kidem-tazminati");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} Hesaplama — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords),
            JsonLd = """
            {
              "@context": "https://schema.org",
              "@type": "WebApplication",
              "name": "Kıdem Tazminatı Hesaplama",
              "applicationCategory": "BusinessApplication",
              "operatingSystem": "Web"
            }
            """
        };
        ViewData["Meta"] = meta;

        return View("~/Views/Hesapla/IsHukuku/KidemTazminati.cshtml", new KidemTazminatiInput());
    }

    [HttpPost("is-hukuku/kidem-tazminati")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KidemTazminati(
        KidemTazminatiInput input,
        [FromServices] ICalculator<KidemTazminatiInput, KidemTazminatiResult> calculator,
        CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "kidem-tazminati");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} Hesaplama — Lex Calculus",
            Description = meta.ShortDescription
        };

        const string viewPath = "~/Views/Hesapla/IsHukuku/KidemTazminati.cshtml";

        if (!ModelState.IsValid)
        {
            return View(viewPath, input);
        }

        var result = await calculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
            {
                ModelState.AddModelError(field, message);
            }
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }
}
