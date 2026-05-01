namespace LexCalculus.Web.Infrastructure.Rendering;

/// <summary>
/// AJAX endpoint'lerin Razor partial view'ları string olarak render edip
/// JSON response içinde döndürmesi için. /Views/Shared/{viewName}.cshtml
/// pattern'i kullanılır. Faz 4.9 P2.
/// </summary>
public interface IPartialRenderer
{
    Task<string> RenderAsync<TModel>(string viewName, TModel model, CancellationToken ct = default);
}
