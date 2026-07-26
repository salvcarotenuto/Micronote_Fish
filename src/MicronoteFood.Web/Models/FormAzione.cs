namespace MicronoteFood.Web.Models;

public static class FormAzione
{
    public const int Nessuna = 0;
    public const int Visualizzazione = 1;
    public const int Inserimento = 2;
    public const int Modifica = 3;
    public const int Cancellazione = 4;
    public const int Zoom = 5;
    public const int Stampa = 8;
    public const int Login = 11;
    public const int Logout = 12;

    public const int Modale = 100;
    public const int OrigineFe = 200;
    public const int CodiceBloccato = 400;

    public static int Base(int azione) => Math.Abs(azione) % 100;

    public static int Contesto(int azione) => Math.Abs(azione) / 100;

    public static bool HasContesto(int azione, int contesto)
    {
        if (contesto <= 0 || contesto % 100 != 0)
        {
            return false;
        }

        var flag = contesto / 100;
        return (Contesto(azione) & flag) == flag;
    }

    public static int WithContesto(int azioneBase, params int[] contesti)
    {
        var mask = 0;
        foreach (var contesto in contesti)
        {
            if (contesto > 0 && contesto % 100 == 0)
            {
                mask |= contesto / 100;
            }
        }

        return azioneBase + (mask * 100);
    }

    public static bool IsVisualizzazione(int azione) => Base(azione) == Visualizzazione;

    public static bool IsInserimento(int azione) => Base(azione) == Inserimento;

    public static bool IsModifica(int azione) => Base(azione) == Modifica;

    public static bool IsCancellazione(int azione) => Base(azione) == Cancellazione;

    public static bool IsZoom(int azione) => Base(azione) == Zoom;

    public static bool IsStampa(int azione) => Base(azione) == Stampa;

    public static bool IsModale(int azione) => HasContesto(azione, Modale);

    public static bool IsOrigineFe(int azione) => HasContesto(azione, OrigineFe);

    public static bool IsCodiceBloccato(int azione) => HasContesto(azione, CodiceBloccato);

    public static bool IsReadonly(int azione) =>
        IsVisualizzazione(azione)
        || IsCancellazione(azione)
        || IsZoom(azione)
        || IsStampa(azione);

    public static int Normalize(int azione, int fallback)
    {
        var azioneBase = Base(azione);
        if (azioneBase is Visualizzazione or Inserimento or Modifica or Cancellazione or Zoom or Stampa or Login or Logout)
        {
            return azione;
        }

        return fallback;
    }

    public static int ForRecord(bool hasRecord, params int[] contesti) =>
        WithContesto(hasRecord ? Modifica : Inserimento, contesti);
}

