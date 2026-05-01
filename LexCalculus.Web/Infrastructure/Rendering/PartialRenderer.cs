using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace LexCalculus.Web.Infrastructure.Rendering;

/// <summary>
/// EmailTemplateRenderer pattern reuse — Razor partial'ı string'e render eder.
/// Path öncelik sırası:
///   1. /Views/Shared/{viewName}.cshtml (genel partial konumu)
///   2. FindView fallback (current request action context)
/// </summary>
public sealed class PartialRenderer : IPartialRenderer
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PartialRenderer(
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IServiceProvider serviceProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> RenderAsync<TModel>(
        string viewName, TModel model, CancellationToken ct = default)
    {
        var actionContext = GetActionContext();

        var viewResult = _viewEngine.GetView(
            executingFilePath: null,
            viewPath: $"/Views/Shared/{viewName}.cshtml",
            isMainPage: false);

        if (!viewResult.Success)
            viewResult = _viewEngine.FindView(actionContext, viewName, isMainPage: false);

        if (!viewResult.Success)
            throw new InvalidOperationException(
                $"Partial view bulunamadı: {viewName}. " +
                $"Aranan: /Views/Shared/{viewName}.cshtml + FindView fallback. " +
                $"Aranan path'ler: {string.Join(", ", viewResult.SearchedLocations)}");

        await using var sw = new StringWriter();
        var viewData = new ViewDataDictionary<TModel>(
            new EmptyModelMetadataProvider(),
            new ModelStateDictionary())
        {
            Model = model
        };
        var tempData = new TempDataDictionary(actionContext.HttpContext, _tempDataProvider);

        var viewContext = new ViewContext(
            actionContext, viewResult.View, viewData, tempData, sw, new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return sw.ToString();
    }

    private ActionContext GetActionContext()
    {
        // Mevcut request varsa onu kullan (URL helper, antiforgery vb. için faydalı);
        // yoksa boş context oluştur (test ortamı).
        var httpContext = _httpContextAccessor.HttpContext
            ?? new DefaultHttpContext { RequestServices = _serviceProvider };
        return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
    }
}
