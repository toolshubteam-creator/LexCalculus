namespace LexCalculus.Core.Email;

/// <summary>
/// Renders a Razor view to a string for use in email bodies.
/// Templates live under /Views/Emails/. Interface in Core so Jobs and other
/// non-Web projects can depend on it without referencing Web.
/// Implementation (EmailTemplateRenderer) lives in LexCalculus.Web because
/// it needs IRazorViewEngine.
/// </summary>
public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync<TModel>(string viewName, TModel model, CancellationToken ct = default);
}
