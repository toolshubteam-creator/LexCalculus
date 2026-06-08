using System.Text.Json;
using System.Text.Json.Serialization;
using LexCalculus.Core.Calculators.Akturya;
using LexCalculus.Core.Calculators.AileMiras;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Calculators.Faiz;
using LexCalculus.Core.Calculators.Gayrimenkul;
using LexCalculus.Core.Calculators.IsHukuku;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Core.Services;
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
    private readonly ICalculator<KiraArtisiInput, KiraArtisiResult> _kiraArtisiCalculator;
    private readonly ICalculator<MenfiTespitFaizInput, MenfiTespitFaizResult> _menfiTespitFaizCalculator;
    private readonly ICalculator<ArsaPayiInput, ArsaPayiResult> _arsaPayiCalculator;
    private readonly ICalculator<KamulastirmaBedeliInput, KamulastirmaBedeliResult> _kamulastirmaCalculator;
    private readonly ICalculator<EcrimisilInput, EcrimisilResult> _ecrimisilCalculator;
    private readonly ICalculator<KatKarsiligiInsaatInput, KatKarsiligiInsaatResult> _katKarsiligiCalculator;
    private readonly ICalculator<HasilatKiraInput, HasilatKiraResult> _hasilatKiraCalculator;
    private readonly ICalculator<NafakaInput, NafakaResult> _nafakaCalculator;
    private readonly ICalculator<MalRejimiTasfiyesiInput, MalRejimiTasfiyesiResult> _malRejimiCalculator;
    private readonly ICalculator<MirasPayiInput, MirasPayiResult> _mirasPayiCalculator;
    private readonly ICalculator<TenkisInput, TenkisResult> _tenkisCalculator;
    private readonly ICalculationHistoryService _historyService;
    private readonly ITenantContext _tenantContext;

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
        ICalculator<AkdiTemerrutFaizInput, AkdiTemerrutFaizResult> akdiTemerrutCalculator,
        ICalculator<KiraArtisiInput, KiraArtisiResult> kiraArtisiCalculator,
        ICalculator<MenfiTespitFaizInput, MenfiTespitFaizResult> menfiTespitFaizCalculator,
        ICalculator<ArsaPayiInput, ArsaPayiResult> arsaPayiCalculator,
        ICalculator<KamulastirmaBedeliInput, KamulastirmaBedeliResult> kamulastirmaCalculator,
        ICalculator<EcrimisilInput, EcrimisilResult> ecrimisilCalculator,
        ICalculator<KatKarsiligiInsaatInput, KatKarsiligiInsaatResult> katKarsiligiCalculator,
        ICalculator<HasilatKiraInput, HasilatKiraResult> hasilatKiraCalculator,
        ICalculator<NafakaInput, NafakaResult> nafakaCalculator,
        ICalculator<MalRejimiTasfiyesiInput, MalRejimiTasfiyesiResult> malRejimiCalculator,
        ICalculator<MirasPayiInput, MirasPayiResult> mirasPayiCalculator,
        ICalculator<TenkisInput, TenkisResult> tenkisCalculator,
        ICalculationHistoryService historyService,
        ITenantContext tenantContext)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
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
        _kiraArtisiCalculator = kiraArtisiCalculator ?? throw new ArgumentNullException(nameof(kiraArtisiCalculator));
        _menfiTespitFaizCalculator = menfiTespitFaizCalculator ?? throw new ArgumentNullException(nameof(menfiTespitFaizCalculator));
        _arsaPayiCalculator = arsaPayiCalculator ?? throw new ArgumentNullException(nameof(arsaPayiCalculator));
        _kamulastirmaCalculator = kamulastirmaCalculator ?? throw new ArgumentNullException(nameof(kamulastirmaCalculator));
        _ecrimisilCalculator = ecrimisilCalculator ?? throw new ArgumentNullException(nameof(ecrimisilCalculator));
        _katKarsiligiCalculator = katKarsiligiCalculator ?? throw new ArgumentNullException(nameof(katKarsiligiCalculator));
        _hasilatKiraCalculator = hasilatKiraCalculator ?? throw new ArgumentNullException(nameof(hasilatKiraCalculator));
        _nafakaCalculator = nafakaCalculator ?? throw new ArgumentNullException(nameof(nafakaCalculator));
        _malRejimiCalculator = malRejimiCalculator ?? throw new ArgumentNullException(nameof(malRejimiCalculator));
        _mirasPayiCalculator = mirasPayiCalculator ?? throw new ArgumentNullException(nameof(mirasPayiCalculator));
        _tenkisCalculator = tenkisCalculator ?? throw new ArgumentNullException(nameof(tenkisCalculator));
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
    }

    private async Task LogHistoryAsync<TInput, TResult>(
        CalculatorMetadata meta,
        TInput input,
        TResult result,
        decimal? totalAmount,
        string? unit,
        bool shareWithTenant,
        CancellationToken cancellationToken)
    {
        int? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(idClaim, out var parsed))
                userId = parsed;
        }

        // Tenant'sız kullanıcılar paylaşamaz; UI checkbox'u zaten görünmüyor,
        // gelse de defansif olarak null'a çekiyoruz.
        int? tenantId = (shareWithTenant && _tenantContext.CurrentTenantId.HasValue)
            ? _tenantContext.CurrentTenantId
            : null;

        await _historyService.LogAsync(
            userId,
            meta.Category.ToShortName(),
            meta.Slug,
            meta.Title,
            input,
            result,
            totalAmount,
            unit,
            tenantId,
            cancellationToken);
    }

    private static readonly JsonSerializerOptions RestoreJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private async Task<TInput?> RestoreFromHistoryAsync<TInput>(
        int? restoreId, CancellationToken ct)
        where TInput : class
    {
        if (!restoreId.HasValue || restoreId.Value <= 0) return null;

        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idClaim, out var userId)) return null;

        var entry = await _historyService.GetByIdForUserAsync(restoreId.Value, userId, ct);
        if (entry == null)
        {
            TempData["RestoreWarning"] =
                "Eski hesap yüklenemedi (bulunamadı veya size ait değil).";
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TInput>(entry.InputJson, RestoreJsonOptions);
        }
        catch (JsonException)
        {
            TempData["RestoreWarning"] =
                "Eski hesap yüklenemedi (kayıt eski olabilir, hesaplayıcı güncellenmiş olabilir).";
            return null;
        }
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
    public async Task<IActionResult> KidemTazminati([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/IsHukuku/KidemTazminati.cshtml", await RestoreFromHistoryAsync<KidemTazminatiInput>(restore, ct) ?? new KidemTazminatiInput());
    }

    [HttpPost("is-hukuku/kidem-tazminati")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KidemTazminati(KidemTazminatiInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("is-hukuku/ihbar-tazminati")]
    public async Task<IActionResult> IhbarTazminati([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/IsHukuku/IhbarTazminati.cshtml", await RestoreFromHistoryAsync<IhbarTazminatiInput>(restore, ct) ?? new IhbarTazminatiInput());
    }

    [HttpPost("is-hukuku/ihbar-tazminati")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IhbarTazminati(IhbarTazminatiInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("is-hukuku/yillik-izin-ucreti")]
    public async Task<IActionResult> YillikIzin([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/IsHukuku/YillikIzin.cshtml", await RestoreFromHistoryAsync<YillikIzinInput>(restore, ct) ?? new YillikIzinInput());
    }

    [HttpPost("is-hukuku/yillik-izin-ucreti")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> YillikIzin(YillikIzinInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("is-hukuku/fazla-mesai")]
    public async Task<IActionResult> FazlaMesai([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/IsHukuku/FazlaMesai.cshtml", await RestoreFromHistoryAsync<FazlaMesaiInput>(restore, ct) ?? new FazlaMesaiInput());
    }

    [HttpPost("is-hukuku/fazla-mesai")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FazlaMesai(FazlaMesaiInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("is-hukuku/ise-iade-tazminati")]
    public async Task<IActionResult> IseIade([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/IsHukuku/IseIade.cshtml", await RestoreFromHistoryAsync<IseIadeInput>(restore, ct) ?? new IseIadeInput());
    }

    [HttpPost("is-hukuku/ise-iade-tazminati")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IseIade(IseIadeInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("is-hukuku/asgari-ucret-kontrol")]
    public async Task<IActionResult> AsgariUcret([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/IsHukuku/AsgariUcret.cshtml", await RestoreFromHistoryAsync<AsgariUcretInput>(restore, ct) ?? new AsgariUcretInput());
    }

    [HttpPost("is-hukuku/asgari-ucret-kontrol")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AsgariUcret(AsgariUcretInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("is-hukuku/mobbing-tazminati")]
    public async Task<IActionResult> Mobbing([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/IsHukuku/Mobbing.cshtml", await RestoreFromHistoryAsync<MobbingInput>(restore, ct) ?? new MobbingInput());
    }

    [HttpPost("is-hukuku/mobbing-tazminati")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Mobbing(MobbingInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("akturya/destekten-yoksun-kalma")]
    public async Task<IActionResult> DestektenYoksunKalma([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/Akturya/DestektenYoksunKalma.cshtml", await RestoreFromHistoryAsync<DesteKtenYoksunKalmaInput>(restore, ct) ?? new DesteKtenYoksunKalmaInput());
    }

    [HttpPost("akturya/destekten-yoksun-kalma")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DestektenYoksunKalma(DesteKtenYoksunKalmaInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("akturya/maluliyet-tazminati")]
    public async Task<IActionResult> Maluliyet([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/Akturya/Maluliyet.cshtml", await RestoreFromHistoryAsync<MaluliyetInput>(restore, ct) ?? new MaluliyetInput());
    }

    [HttpPost("akturya/maluliyet-tazminati")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Maluliyet(MaluliyetInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("akturya/gecici-is-goremezlik")]
    public async Task<IActionResult> GeciciIsGoremezlik([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/Akturya/GeciciIsGoremezlik.cshtml", await RestoreFromHistoryAsync<GeciciIsGoremezlikInput>(restore, ct) ?? new GeciciIsGoremezlikInput());
    }

    [HttpPost("akturya/gecici-is-goremezlik")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GeciciIsGoremezlik(GeciciIsGoremezlikInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("akturya/bakici-gideri")]
    public async Task<IActionResult> BakiciGideri([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/Akturya/BakiciGideri.cshtml", await RestoreFromHistoryAsync<BakiciGideriInput>(restore, ct) ?? new BakiciGideriInput());
    }

    [HttpPost("akturya/bakici-gideri")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BakiciGideri(BakiciGideriInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("akturya/arac-deger-kaybi")]
    public async Task<IActionResult> AracDegerKaybi([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/Akturya/AracDegerKaybi.cshtml", await RestoreFromHistoryAsync<AracDegerKaybiInput>(restore, ct) ?? new AracDegerKaybiInput());
    }

    [HttpPost("akturya/arac-deger-kaybi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AracDegerKaybi(AracDegerKaybiInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("faiz/yasal-faiz")]
    public async Task<IActionResult> YasalFaiz([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/Faiz/YasalFaiz.cshtml", await RestoreFromHistoryAsync<YasalFaizInput>(restore, ct) ?? new YasalFaizInput());
    }

    [HttpPost("faiz/yasal-faiz")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> YasalFaiz(YasalFaizInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("faiz/ticari-temerrut-faizi")]
    public async Task<IActionResult> TicariTemerrutFaiz([FromQuery] int? restore, CancellationToken ct)
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

        return View("~/Views/Hesapla/Faiz/TicariTemerrutFaiz.cshtml", await RestoreFromHistoryAsync<TicariTemerrutFaizInput>(restore, ct) ?? new TicariTemerrutFaizInput());
    }

    [HttpPost("faiz/ticari-temerrut-faizi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TicariTemerrutFaiz(TicariTemerrutFaizInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("faiz/akdi-temerrut-faizi")]
    public async Task<IActionResult> AkdiTemerrutFaiz([FromQuery] int? restore, CancellationToken ct)
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

        var input = await RestoreFromHistoryAsync<AkdiTemerrutFaizInput>(restore, ct) ?? new AkdiTemerrutFaizInput
        {
            SozlesmeOranlari = new List<SozlesmeOranDonem> { new() }
        };

        return View("~/Views/Hesapla/Faiz/AkdiTemerrutFaiz.cshtml", input);
    }

    [HttpPost("faiz/akdi-temerrut-faizi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AkdiTemerrutFaiz(AkdiTemerrutFaizInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
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
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("faiz/kira-artisi")]
    public async Task<IActionResult> KiraArtisi([FromQuery] int? restore, CancellationToken ct)
    {
        var meta = _registry.Find(CalculatorCategory.Faiz, "kira-artisi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        var input = await RestoreFromHistoryAsync<KiraArtisiInput>(restore, ct) ?? new KiraArtisiInput
        {
            YenilenmeTarihi = DateTime.Today
        };

        return View("~/Views/Hesapla/Faiz/KiraArtisi.cshtml", input);
    }

    [HttpPost("faiz/kira-artisi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KiraArtisi(KiraArtisiInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
    {
        var meta = _registry.Find(CalculatorCategory.Faiz, "kira-artisi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Faiz/KiraArtisi.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _kiraArtisiCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("faiz/menfi-tespit-faizi")]
    public async Task<IActionResult> MenfiTespitFaiz([FromQuery] int? restore, CancellationToken ct)
    {
        var meta = _registry.Find(CalculatorCategory.Faiz, "menfi-tespit-faizi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        var input = await RestoreFromHistoryAsync<MenfiTespitFaizInput>(restore, ct) ?? new MenfiTespitFaizInput
        {
            TahsilTarihi = DateTime.Today.AddYears(-1),
            HesapTarihi = DateTime.Today
        };

        return View("~/Views/Hesapla/Faiz/MenfiTespitFaiz.cshtml", input);
    }

    [HttpPost("faiz/menfi-tespit-faizi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MenfiTespitFaiz(MenfiTespitFaizInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
    {
        var meta = _registry.Find(CalculatorCategory.Faiz, "menfi-tespit-faizi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Faiz/MenfiTespitFaiz.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _menfiTespitFaizCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("gayrimenkul/arsa-payi")]
    public async Task<IActionResult> ArsaPayi([FromQuery] int? restore, CancellationToken ct)
    {
        var meta = _registry.Find(CalculatorCategory.Gayrimenkul, "arsa-payi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        var input = await RestoreFromHistoryAsync<ArsaPayiInput>(restore, ct) ?? new ArsaPayiInput
        {
            BagimsizBolumler = new List<BagimsizBolumGirdi> { new(), new() }
        };

        return View("~/Views/Hesapla/Gayrimenkul/ArsaPayi.cshtml", input);
    }

    [HttpPost("gayrimenkul/arsa-payi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArsaPayi(ArsaPayiInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
    {
        var meta = _registry.Find(CalculatorCategory.Gayrimenkul, "arsa-payi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Gayrimenkul/ArsaPayi.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _arsaPayiCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("gayrimenkul/kamulastirma-bedeli")]
    public async Task<IActionResult> KamulastirmaBedeli([FromQuery] int? restore, CancellationToken ct)
    {
        var meta = _registry.Find(CalculatorCategory.Gayrimenkul, "kamulastirma-bedeli");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/Gayrimenkul/KamulastirmaBedeli.cshtml",
            await RestoreFromHistoryAsync<KamulastirmaBedeliInput>(restore, ct) ?? new KamulastirmaBedeliInput());
    }

    [HttpPost("gayrimenkul/kamulastirma-bedeli")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KamulastirmaBedeli(KamulastirmaBedeliInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
    {
        var meta = _registry.Find(CalculatorCategory.Gayrimenkul, "kamulastirma-bedeli");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Gayrimenkul/KamulastirmaBedeli.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _kamulastirmaCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("gayrimenkul/ecrimisil")]
    public async Task<IActionResult> Ecrimisil([FromQuery] int? restore, CancellationToken ct)
    {
        var meta = _registry.Find(CalculatorCategory.Gayrimenkul, "ecrimisil");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/Gayrimenkul/Ecrimisil.cshtml",
            await RestoreFromHistoryAsync<EcrimisilInput>(restore, ct) ?? new EcrimisilInput());
    }

    [HttpPost("gayrimenkul/ecrimisil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ecrimisil(EcrimisilInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
    {
        var meta = _registry.Find(CalculatorCategory.Gayrimenkul, "ecrimisil");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Gayrimenkul/Ecrimisil.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _ecrimisilCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("gayrimenkul/kat-karsiligi-insaat")]
    public async Task<IActionResult> KatKarsiligiInsaat([FromQuery] int? restore, CancellationToken ct)
    {
        var meta = _registry.Find(CalculatorCategory.Gayrimenkul, "kat-karsiligi-insaat");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/Gayrimenkul/KatKarsiligiInsaat.cshtml",
            await RestoreFromHistoryAsync<KatKarsiligiInsaatInput>(restore, ct) ?? new KatKarsiligiInsaatInput());
    }

    [HttpPost("gayrimenkul/kat-karsiligi-insaat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KatKarsiligiInsaat(KatKarsiligiInsaatInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
    {
        var meta = _registry.Find(CalculatorCategory.Gayrimenkul, "kat-karsiligi-insaat");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Gayrimenkul/KatKarsiligiInsaat.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _katKarsiligiCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("gayrimenkul/hasilat-kira")]
    public async Task<IActionResult> HasilatKira([FromQuery] int? restore, CancellationToken ct)
    {
        var meta = _registry.Find(CalculatorCategory.Gayrimenkul, "hasilat-kira");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/Gayrimenkul/HasilatKira.cshtml",
            await RestoreFromHistoryAsync<HasilatKiraInput>(restore, ct) ?? new HasilatKiraInput());
    }

    [HttpPost("gayrimenkul/hasilat-kira")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HasilatKira(HasilatKiraInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
    {
        var meta = _registry.Find(CalculatorCategory.Gayrimenkul, "hasilat-kira");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/Gayrimenkul/HasilatKira.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _hasilatKiraCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("aile-miras/nafaka")]
    public async Task<IActionResult> Nafaka([FromQuery] int? restore, CancellationToken ct)
    {
        var meta = _registry.Find(CalculatorCategory.AileMiras, "nafaka");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        var input = await RestoreFromHistoryAsync<NafakaInput>(restore, ct) ?? new NafakaInput
        {
            Cocuklar = new List<NafakaCocukGirdi> { new() },
            HesapTarihi = DateTime.Today
        };

        return View("~/Views/Hesapla/AileMiras/Nafaka.cshtml", input);
    }

    [HttpPost("aile-miras/nafaka")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nafaka(NafakaInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
    {
        var meta = _registry.Find(CalculatorCategory.AileMiras, "nafaka");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/AileMiras/Nafaka.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _nafakaCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("aile-miras/mal-rejimi-tasfiyesi")]
    public async Task<IActionResult> MalRejimiTasfiyesi([FromQuery] int? restore, CancellationToken ct)
    {
        var meta = _registry.Find(CalculatorCategory.AileMiras, "mal-rejimi-tasfiyesi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        return View("~/Views/Hesapla/AileMiras/MalRejimiTasfiyesi.cshtml",
            await RestoreFromHistoryAsync<MalRejimiTasfiyesiInput>(restore, ct) ?? new MalRejimiTasfiyesiInput());
    }

    [HttpPost("aile-miras/mal-rejimi-tasfiyesi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MalRejimiTasfiyesi(MalRejimiTasfiyesiInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
    {
        var meta = _registry.Find(CalculatorCategory.AileMiras, "mal-rejimi-tasfiyesi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/AileMiras/MalRejimiTasfiyesi.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _malRejimiCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("aile-miras/miras-payi")]
    public async Task<IActionResult> MirasPayi([FromQuery] int? restore, CancellationToken ct)
    {
        var meta = _registry.Find(CalculatorCategory.AileMiras, "miras-payi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        var input = await RestoreFromHistoryAsync<MirasPayiInput>(restore, ct) ?? new MirasPayiInput
        {
            Yapi = new MirasciYapisiInput { SagKalanEsVar = true, SagCocukSayisi = 2 }
        };

        return View("~/Views/Hesapla/AileMiras/MirasPayi.cshtml", input);
    }

    [HttpPost("aile-miras/miras-payi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MirasPayi(MirasPayiInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
    {
        var meta = _registry.Find(CalculatorCategory.AileMiras, "miras-payi");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/AileMiras/MirasPayi.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _mirasPayiCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }

    [HttpGet("aile-miras/tenkis")]
    public async Task<IActionResult> Tenkis([FromQuery] int? restore, CancellationToken ct)
    {
        var meta = _registry.Find(CalculatorCategory.AileMiras, "tenkis");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{meta.Title} — Lex Calculus",
            Description = meta.ShortDescription,
            Keywords = string.Join(", ", meta.Keywords)
        };

        var input = await RestoreFromHistoryAsync<TenkisInput>(restore, ct) ?? new TenkisInput
        {
            Yapi = new MirasciYapisiInput { SagKalanEsVar = true, SagCocukSayisi = 1 },
            Bagislar = new List<BagisGirdi> { new() { Tarih = DateTime.Today } }
        };

        return View("~/Views/Hesapla/AileMiras/Tenkis.cshtml", input);
    }

    [HttpPost("aile-miras/tenkis")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Tenkis(TenkisInput input, [FromForm] bool shareWithTenant = false, CancellationToken cancellationToken = default)
    {
        var meta = _registry.Find(CalculatorCategory.AileMiras, "tenkis");
        if (meta is null) return NotFound();

        ViewData["Title"] = meta.Title;
        ViewData["Meta"] = meta;
        ViewData["PageMeta"] = new SeoMeta { Title = $"{meta.Title} — Lex Calculus", Description = meta.ShortDescription };

        const string viewPath = "~/Views/Hesapla/AileMiras/Tenkis.cshtml";
        if (!ModelState.IsValid) return View(viewPath, input);

        var result = await _tenkisCalculator.CalculateAsync(input, cancellationToken);

        if (!result.IsValid)
        {
            foreach (var (field, message) in result.ValidationErrors)
                ModelState.AddModelError(field, message);
            return View(viewPath, input);
        }

        ViewData["Result"] = result;
        await LogHistoryAsync(meta, input, result, result.TotalAmount, result.Unit, shareWithTenant, cancellationToken);
        return View(viewPath, input);
    }
}
