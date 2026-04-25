using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LexCalculus.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ResetPasswordConfirmation : PageModel
{
    public void OnGet()
    {
    }
}
