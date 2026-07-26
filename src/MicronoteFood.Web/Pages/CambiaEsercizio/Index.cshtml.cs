using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.CambiaEsercizio;

public class IndexModel(ApplicationState applicationState) : PageModel
{
    public int CurrentExercise => applicationState.Esercizio;

    public int? SavedExercise { get; private set; }

    [BindProperty]
    [Display(Name = "Nuovo esercizio")]
    [Required(ErrorMessage = "Indicare un esercizio valido.")]
    [Range(1900, 2200, ErrorMessage = "Esercizio fuori intervallo.")]
    public int NewExercise { get; set; }

    public void OnGet()
    {
        NewExercise = applicationState.Esercizio;
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        applicationState.Esercizio = NewExercise;
        SavedExercise = NewExercise;
        return Page();
    }
}
