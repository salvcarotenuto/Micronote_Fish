using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.RazorPages; using MicronoteFood.Web.Data; using MicronoteFood.Web.Models;
namespace MicronoteFood.Web.Pages.Banche;
public sealed class IndexModel(BankRepository repository) : PageModel
{
    public IReadOnlyList<BankListItem> Banks { get; private set; } = [];
    [TempData] public string? ErrorMessage { get; set; }
    public async Task OnGetAsync(CancellationToken token) => Banks = await repository.ListAsync(token);
    public async Task<IActionResult> OnPostDeleteAsync(int code, CancellationToken token) { var result=await repository.DeleteAsync(code,token); if(!result.Deleted) ErrorMessage=result.Message; return RedirectToPage(); }
}
