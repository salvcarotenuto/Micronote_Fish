using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.RazorPages; using MicronoteFood.Web.Data; using MicronoteFood.Web.Models;
namespace MicronoteFood.Web.Pages.Banche;
public sealed class EditModel(BankRepository repository) : PageModel
{
    [BindProperty] public BankEditModel Bank { get; set; } = new();
    public bool IsNew => Bank.Code == 0; public int DisplayCode { get; private set; }
    public async Task<IActionResult> OnGetAsync(int? code, CancellationToken token) { if(code.HasValue){Bank=await repository.GetAsync(code.Value,token) ?? new(); if(Bank.Code==0)return NotFound();} else DisplayCode=await repository.NextCodeAsync(token); return Page(); }
    public async Task<IActionResult> OnPostAsync(CancellationToken token) { Normalize(); if(!ModelState.IsValid){if(Bank.Code==0)DisplayCode=await repository.NextCodeAsync(token);return Page();} if(Bank.Code==0)await repository.InsertAsync(Bank,token); else if(!await repository.UpdateAsync(Bank,token))return NotFound(); return RedirectToPage("./Index"); }
    private void Normalize(){Bank.Name=Bank.Name.Trim(); Bank.Branch=Bank.Branch?.Trim(); Bank.Website=Bank.Website?.Trim(); Bank.Email=Bank.Email?.Trim(); Bank.Phone=Bank.Phone?.Trim(); Bank.Mobile=Bank.Mobile?.Trim(); Bank.Abi=Bank.Abi?.Trim(); Bank.Cab=Bank.Cab?.Trim(); Bank.Swift=Bank.Swift?.Trim(); Bank.Sia=Bank.Sia?.Trim(); Bank.Account=Bank.Account?.Trim(); Bank.Iban=Bank.Iban?.Trim(); Bank.Notes=Bank.Notes?.Trim();}
}
