using LexCalculus.Core.Calculators.Akturya;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Calculators.Faiz;
using LexCalculus.Core.Calculators.IsHukuku;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Web.Models.Hesapla;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Controllers;

[Route("hesapla")]
public class HesaplaController : Controller
{
    private readonly ICalculatorRegistry _registry;
    private readonly ICalculator<KidemTazminatiInput, KidemTazminatiResult> _kidemCalculator;
    private readonly ICalculator<IhbarTazminatiInput, IhbarTazminatiResult> _ihbarCalculator;
    private readonly ICalculator<YillikIzinInput, YillikIzinResult> _yillikIzinCalculator;
    private readonly ICalculator<FazlaMesaiInput, FazlaMesaiResult> _fazlaMesaiCalculator;
    private readonly ICalculator<IseIadeInput, IseIadeResult> _iseIadeCalculator;
    private readonly ICalculator<AsgariUcretInput, AsgariUcretResult> _asgariUcretCalculator;
    private readonly ICalculator<MobbingInput, MobbingResult> _mobbingCalculator;
    private readonly ICalculator<DesteKtenYoksunKalmaInput, DesteKtenYoksunKalmaResult> _destekKalculator;
    private readonly ICalculator<MaluliyetInput, MaluliyetResult> _maluliyetCalculator;
    private readonly ICalculator<GeciciIsGoremezlikInput, GeciciIsGoremezlikResult> _gecIsGoremezlikCalculator;
    private readonly ICalculator<BakiciGideriInput, BakiciGideriResult> _bakiciCalculator;
    private readonly ICalculator<AracDegerKaybiInput, AracDegerKaybiResult> _aracDegerCalculator;
    private readonly ICalculator<YasalFaizInput, YasalFaizResult> _yasalFaizCalculator;
    private readonly ICalculator<TicariTemerrutFaizInput, TicariTemerrutFaizResult> _ticariFaizCalculator;
    private readonly ICalculator<AkdiTemerrutFaizInput, AkdiTemerrutFaizResult> _akdiTemerrutCalculator;

    public HesaplaController(
        ICalculatorRegistry registry,
        ICalculator<KidemTazminatiInput, KidemTazminatiResult> kidemCalculator,
        ICalculator<IhbarTazminatiInput, IhbarTazminatiResult> ihbarCalculator,
        ICalculator<YillikIzinInput, YillikIzinResult> yillikIzinCalculator,
        ICalculator<FazlaMesaiInput, FazlaMesaiResult> fazlaMesaiCalculator,
        ICalculator<IseIadeInput, IseIadeResult> iseIadeCalculator,
        ICalculator<AsgariUcretInput, AsgariUcretResult> asgariUcretCalculator,
        ICalculator<MobbingInput, MobbingResult> mobbingCalculator,
        ICalculator<DesteKtenYoksunKalmaInput, DesteKtenYoksunKalmaResult> destekKalculator,
        ICalculator<MaluliyetInput, MaluliyetResult> maluliyetCalculator,
        ICalculator<GeciciIsGoremezlikInput, GeciciIsGoremezlikResult> gecIsGoremezlikCalculator,
        ICalculator<BakiciGideriInput, BakiciGideriResult> bakiciCalculator,
        ICalculator<AracDegerKaybiInput, AracDegerKaybiResult> aracDegerCalculator,
        ICalculator<YasalFaizInput, YasalFaizResult> yasalFaizCalculator,
        ICalculator<TicariTemerrutFaizInput, TicariTemerrutFaizResult> ticariFaizCalculator,
        ICalculator<AkdiTemerrutFaizInput, AkdiTemerrutFaizResult> akdiTemerrutCalculator)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _kidemCalculator = kidemCalculator ?? throw new ArgumentNullException(nameof(kidemCalculator));
        _ihbarCalculator = ihbarCalculator ?? throw new ArgumentNullException(nameof(ihbarCalculator));
        _yillikIzinCalculator = yillikIzinCalculator ?? throw new ArgumentNullException(nameof(yillikIzinCalculator));
        _fazlaMesaiCalculator = fazlaMesaiCalculator ?? throw new ArgumentNullException(nameof(fazlaMesaiCalculator));
        _iseIadeCalculator = iseIadeCalculator ?? throw new ArgumentNullException(nameof(iseIadeCalculator));
        _asgariUcretCalculator = asgariUcretCalculator ?? throw new ArgumentNullException(nameof(asgariUcretCalculator));
        _mobbingCalculator = mobbingCalculator ?? throw new ArgumentNullException(nameof(mobbingCalculator));
        _destekKalculator = destekKalculator ?? throw new ArgumentNullException(nameof(destekKalculator));
        _maluliyetCalculator = maluliyetCalculator ?? throw new ArgumentNullException(nameof(maluliyetCalculator));
        _gecIsGoremezlikCalculator = gecIsGoremezlikCalculator ?? throw new ArgumentNullException(nameof(gecIsGoremezlikCalculator));
        _bakiciCalculator = bakiciCalculator ?? throw new ArgumentNullException(nameof(bakiciCalculator));
        _aracDegerCalculator = aracDegerCalculator ?? throw new ArgumentNullException(nameof(aracDegerCalculator));
        _yasalFaizCalculator = yasalFaizCalculator ?? throw new ArgumentNullException(nameof(yasalFaizCalculator));
        _ticariFaizCalculator = ticariFaizCalculator ?? throw new ArgumentNullException(nameof(ticariFaizCalculator));
        _akdiTemerrutCalculator = akdiTemerrutCalculator ?? throw new ArgumentNullException(nameof(akdiTemerrutCalculator));
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
    public async Task<IActionResult> KidemTazminati(KidemTazminatiInput input, CancellationToken cancellationToken)
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

        var result = await _kidemCalculator.CalculateAsync(input, cancellationToken);

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

    [HttpGet("is-hukuku/ihbar-tazminati")]
    public IActionResult IhbarTazminati()
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "ihbar-tazminati");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} Hesaplama — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/IsHukuku/IhbarTazminati.cshtml", new IhbarTazminatiInput());
    }

    [HttpPost("is-hukuku/ihbar-tazminati")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IhbarTazminati(IhbarTazminatiInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "ihbar-tazminati");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} Hesaplama — Lex Calculus",
            Description = meta.ShortDescription
        };

        const string viewPath = "~/Views/Hesapla/IsHukuku/IhbarTazminati.cshtml";

        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _ihbarCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }

    [HttpGet("is-hukuku/yillik-izin-ucreti")]
    public IActionResult YillikIzin()
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "yillik-izin-ucreti");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} Hesaplama — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/IsHukuku/YillikIzin.cshtml", new YillikIzinInput());
    }

    [HttpPost("is-hukuku/yillik-izin-ucreti")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> YillikIzin(YillikIzinInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "yillik-izin-ucreti");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} Hesaplama — Lex Calculus",
            Description = meta.ShortDescription
        };

        const string viewPath = "~/Views/Hesapla/IsHukuku/YillikIzin.cshtml";

        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _yillikIzinCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }

    [HttpGet("is-hukuku/fazla-mesai")]
    public IActionResult FazlaMesai()
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "fazla-mesai");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} Hesaplama — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/IsHukuku/FazlaMesai.cshtml", new FazlaMesaiInput());
    }

    [HttpPost("is-hukuku/fazla-mesai")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FazlaMesai(FazlaMesaiInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "fazla-mesai");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} Hesaplama — Lex Calculus",
            Description = meta.ShortDescription
        };

        const string viewPath = "~/Views/Hesapla/IsHukuku/FazlaMesai.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _fazlaMesaiCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }

    [HttpGet("is-hukuku/ise-iade-tazminati")]
    public IActionResult IseIade()
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "ise-iade-tazminati");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} Hesaplama — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/IsHukuku/IseIade.cshtml", new IseIadeInput());
    }

    [HttpPost("is-hukuku/ise-iade-tazminati")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IseIade(IseIadeInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "ise-iade-tazminati");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} Hesaplama — Lex Calculus",
            Description = meta.ShortDescription
        };

        const string viewPath = "~/Views/Hesapla/IsHukuku/IseIade.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _iseIadeCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }

    [HttpGet("is-hukuku/asgari-ucret-kontrol")]
    public IActionResult AsgariUcret()
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "asgari-ucret-kontrol");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/IsHukuku/AsgariUcret.cshtml", new AsgariUcretInput());
    }

    [HttpPost("is-hukuku/asgari-ucret-kontrol")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AsgariUcret(AsgariUcretInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "asgari-ucret-kontrol");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription
        };

        const string viewPath = "~/Views/Hesapla/IsHukuku/AsgariUcret.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _asgariUcretCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }

    [HttpGet("is-hukuku/mobbing-tazminati")]
    public IActionResult Mobbing()
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "mobbing-tazminati");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/IsHukuku/Mobbing.cshtml", new MobbingInput());
    }

    [HttpPost("is-hukuku/mobbing-tazminati")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Mobbing(MobbingInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.IsHukuku, "mobbing-tazminati");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription
        };

        const string viewPath = "~/Views/Hesapla/IsHukuku/Mobbing.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _mobbingCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }

    [HttpGet("akturya/destekten-yoksun-kalma")]
    public IActionResult DestektenYoksunKalma()
    {
        var meta = _registry.Find(CalculatorCategory.Akturya, "destekten-yoksun-kalma");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/Akturya/DestektenYoksunKalma.cshtml", new DesteKtenYoksunKalmaInput());
    }

    [HttpPost("akturya/destekten-yoksun-kalma")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DestektenYoksunKalma(DesteKtenYoksunKalmaInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.Akturya, "destekten-yoksun-kalma");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription
        };

        const string viewPath = "~/Views/Hesapla/Akturya/DestektenYoksunKalma.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _destekKalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }

    [HttpGet("akturya/maluliyet-tazminati")]
    public IActionResult Maluliyet()
    {
        var meta = _registry.Find(CalculatorCategory.Akturya, "maluliyet-tazminati");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/Akturya/Maluliyet.cshtml", new MaluliyetInput());
    }

    [HttpPost("akturya/maluliyet-tazminati")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Maluliyet(MaluliyetInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.Akturya, "maluliyet-tazminati");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Akturya/Maluliyet.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _maluliyetCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }

    [HttpGet("akturya/gecici-is-goremezlik")]
    public IActionResult GeciciIsGoremezlik()
    {
        var meta = _registry.Find(CalculatorCategory.Akturya, "gecici-is-goremezlik");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/Akturya/GeciciIsGoremezlik.cshtml", new GeciciIsGoremezlikInput());
    }

    [HttpPost("akturya/gecici-is-goremezlik")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GeciciIsGoremezlik(GeciciIsGoremezlikInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.Akturya, "gecici-is-goremezlik");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Akturya/GeciciIsGoremezlik.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _gecIsGoremezlikCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }

    [HttpGet("akturya/bakici-gideri")]
    public IActionResult BakiciGideri()
    {
        var meta = _registry.Find(CalculatorCategory.Akturya, "bakici-gideri");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/Akturya/BakiciGideri.cshtml", new BakiciGideriInput());
    }

    [HttpPost("akturya/bakici-gideri")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BakiciGideri(BakiciGideriInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.Akturya, "bakici-gideri");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Akturya/BakiciGideri.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _bakiciCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }

    [HttpGet("akturya/arac-deger-kaybi")]
    public IActionResult AracDegerKaybi()
    {
        var meta = _registry.Find(CalculatorCategory.Akturya, "arac-deger-kaybi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/Akturya/AracDegerKaybi.cshtml", new AracDegerKaybiInput());
    }

    [HttpPost("akturya/arac-deger-kaybi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AracDegerKaybi(AracDegerKaybiInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.Akturya, "arac-deger-kaybi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Akturya/AracDegerKaybi.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _aracDegerCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }

    [HttpGet("faiz/yasal-faiz")]
    public IActionResult YasalFaiz()
    {
        var meta = _registry.Find(CalculatorCategory.Faiz, "yasal-faiz");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/Faiz/YasalFaiz.cshtml", new YasalFaizInput());
    }

    [HttpPost("faiz/yasal-faiz")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> YasalFaiz(YasalFaizInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.Faiz, "yasal-faiz");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Faiz/YasalFaiz.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _yasalFaizCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }

    [HttpGet("faiz/ticari-temerrut-faizi")]
    public IActionResult TicariTemerrutFaiz()
    {
        var meta = _registry.Find(CalculatorCategory.Faiz, "ticari-temerrut-faizi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/Faiz/TicariTemerrutFaiz.cshtml", new TicariTemerrutFaizInput());
    }

    [HttpPost("faiz/ticari-temerrut-faizi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TicariTemerrutFaiz(TicariTemerrutFaizInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.Faiz, "ticari-temerrut-faizi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Faiz/TicariTemerrutFaiz.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _ticariFaizCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }

    [HttpGet("faiz/akdi-temerrut-faizi")]
    public IActionResult AkdiTemerrutFaiz()
    {
        var meta = _registry.Find(CalculatorCategory.Faiz, "akdi-temerrut-faizi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        var input = new AkdiTemerrutFaizInput
        {
            SozlesmeOranlari = new List<SozlesmeOranDonem> { new() }
        };

        return View("~/Views/Hesapla/Faiz/AkdiTemerrutFaiz.cshtml", input);
    }

    [HttpPost("faiz/akdi-temerrut-faizi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AkdiTemerrutFaiz(AkdiTemerrutFaizInput input, CancellationToken cancellationToken)
    {
        var meta = _registry.Find(CalculatorCategory.Faiz, "akdi-temerrut-faizi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Faiz/AkdiTemerrutFaiz.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _akdiTemerrutCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        return View(viewPath, input);
    }
}
