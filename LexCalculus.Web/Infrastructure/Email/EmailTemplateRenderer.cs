using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace LexCalculus.Web.Infrastructure.Email;

/// <summary>
/// Renders a Razor view to a string for use in email bodies.
/// Templates live under /Views/Emails/.
/// </summary>
public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync<TModel>(string viewName, TModel model, CancellationToken ct = default);
}

public sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _serviceProvider;

    public EmailTemplateRenderer(
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IServiceProvider serviceProvider)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> RenderAsync<TModel>(string viewName, TModel model, CancellationToken ct = default)
    {
        var actionContext = GetActionContext();

        // Önce tam path ile dene (/Views/Emails/X.cshtml), bulunmazsa relative
        var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath: $"/Views/Emails/{viewName}.cshtml", isMainPage: false);
        if (!viewResult.Success)
            viewResult = _viewEngine.FindView(actionContext, viewName, isMainPage: false);

        if (!viewResult.Success)
            throw new InvalidOperationException(
                $"E-posta şablonu bulunamadı: {viewName}. " +
                $"Aranan yer: /Views/Emails/{viewName}.cshtml");

        var view = viewResult.View;
        await using var stringWriter = new StringWriter();

        var viewDataDict = new ViewDataDictionary<TModel>(
            new EmptyModelMetadataProvider(),
            new ModelStateDictionary())
        {
            Model = model
        };
        var tempData = new TempDataDictionary(actionContext.HttpContext, _tempDataProvider);

        var viewContext = new ViewContext(
            actionContext, view, viewDataDict, tempData, stringWriter, new HtmlHelperOptions());

        await view.RenderAsync(viewContext);
        return stringWriter.ToString();
    }

    private ActionContext GetActionContext()
    {
        var httpContext = new DefaultHttpContext { RequestServices = _serviceProvider };
        return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
    }
}
