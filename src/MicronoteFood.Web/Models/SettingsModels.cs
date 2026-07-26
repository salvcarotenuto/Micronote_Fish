using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed class SettingsEditModel
{
    [Display(Name = "Ragione sociale")]
    [StringLength(100, ErrorMessage = "Il campo Ragione sociale non puo superare 100 caratteri.")]
    public string? RagioneSociale { get; set; } = "";

    [Display(Name = "Attivita esercitata")]
    [StringLength(250, ErrorMessage = "Il campo Attivita esercitata non puo superare 250 caratteri.")]
    public string? AttivitaEsercitata { get; set; } = "";

    [Display(Name = "Codice fiscale")]
    [StringLength(16, ErrorMessage = "Il campo Codice fiscale non puo superare 16 caratteri.")]
    public string? CodiceFiscale { get; set; } = "";

    [Display(Name = "Partita iva")]
    [StringLength(11, ErrorMessage = "Il campo Partita IVA non puo superare 11 caratteri.")]
    public string? PartitaIva { get; set; } = "";

    [Display(Name = "Numero REA")]
    [StringLength(12, ErrorMessage = "Il campo Numero REA non puo superare 12 caratteri.")]
    public string? NumeroRea { get; set; } = "";

    [Display(Name = "Telefono")]
    [StringLength(40, ErrorMessage = "Il campo Telefono non puo superare 40 caratteri.")]
    public string? Telefono { get; set; } = "";

    [Display(Name = "Sito web")]
    [StringLength(50, ErrorMessage = "Il campo Sito web non puo superare 50 caratteri.")]
    public string? SitoWeb { get; set; } = "";

    [Display(Name = "Natura giuridica")]
    [StringLength(50, ErrorMessage = "Il campo Natura giuridica non puo superare 50 caratteri.")]
    public string? NaturaGiuridica { get; set; } = "";

    [Display(Name = "Citta")]
    [StringLength(50, ErrorMessage = "Il campo Citta non puo superare 50 caratteri.")]
    public string? SedeLegaleCitta { get; set; } = "";

    [Display(Name = "Provincia")]
    [StringLength(2, ErrorMessage = "Il campo Provincia non puo superare 2 caratteri.")]
    public string? SedeLegaleProvincia { get; set; } = "";

    [Display(Name = "Cap")]
    [StringLength(5, ErrorMessage = "Il campo CAP non puo superare 5 caratteri.")]
    public string? SedeLegaleCap { get; set; } = "";

    [Display(Name = "Indirizzo")]
    [StringLength(80, ErrorMessage = "Il campo Indirizzo non puo superare 80 caratteri.")]
    public string? SedeLegaleIndirizzo { get; set; } = "";

    [Display(Name = "PEC")]
    [StringLength(100, ErrorMessage = "Il campo PEC non puo superare 100 caratteri.")]
    public string? AzPec { get; set; } = "";

    [Display(Name = "Nazione")]
    [StringLength(50, ErrorMessage = "Il campo Nazione non puo superare 50 caratteri.")]
    public string? SedeLegaleNazione { get; set; } = "";

    [Display(Name = "Citta")]
    [StringLength(50, ErrorMessage = "Il campo Citta non puo superare 50 caratteri.")]
    public string? SedeOperativaCitta { get; set; } = "";

    [Display(Name = "Provincia")]
    [StringLength(2, ErrorMessage = "Il campo Provincia non puo superare 2 caratteri.")]
    public string? SedeOperativaProvincia { get; set; } = "";

    [Display(Name = "Cap")]
    [StringLength(5, ErrorMessage = "Il campo CAP non puo superare 5 caratteri.")]
    public string? SedeOperativaCap { get; set; } = "";

    [Display(Name = "Indirizzo")]
    [StringLength(80, ErrorMessage = "Il campo Indirizzo non puo superare 80 caratteri.")]
    public string? SedeOperativaIndirizzo { get; set; } = "";

    [Display(Name = "E-mail")]
    [StringLength(100, ErrorMessage = "Il campo E-mail non puo superare 100 caratteri.")]
    public string? AzEmail { get; set; } = "";

    [Display(Name = "Nazione")]
    [StringLength(50, ErrorMessage = "Il campo Nazione non puo superare 50 caratteri.")]
    public string? SedeOperativaNazione { get; set; } = "";

    [Display(Name = "Codice SDI azienda")]
    [StringLength(7, ErrorMessage = "Il campo Codice SDI azienda non puo superare 7 caratteri.")]
    public string? FeCodiceSdiAzienda { get; set; } = "";

    [Display(Name = "PEC destinazione SDI")]
    [StringLength(100, ErrorMessage = "Il campo PEC destinazione SDI non puo superare 100 caratteri.")]
    public string? FePecDestinazioneSdi { get; set; } = "";

    [Display(Name = "Cartella FE acquisti")]
    [StringLength(260, ErrorMessage = "Il campo Cartella FE acquisti non puo superare 260 caratteri.")]
    public string? CartellaFeAcquisti { get; set; } = "";

    [Display(Name = "Cognome")]
    [StringLength(50, ErrorMessage = "Il campo Cognome non puo superare 50 caratteri.")]
    public string? TitolareCognome { get; set; } = "";

    [Display(Name = "Nome")]
    [StringLength(50, ErrorMessage = "Il campo Nome non puo superare 50 caratteri.")]
    public string? TitolareNome { get; set; } = "";

    [Display(Name = "Data di nascita")]
    [StringLength(10, ErrorMessage = "Il campo Data di nascita non puo superare 10 caratteri.")]
    public string? TitolareDataNascita { get; set; } = "";

    [Display(Name = "Codice fiscale")]
    [StringLength(16, ErrorMessage = "Il campo Codice fiscale non puo superare 16 caratteri.")]
    public string? TitolareCodiceFiscale { get; set; } = "";

    [Display(Name = "Citta")]
    [StringLength(50, ErrorMessage = "Il campo Citta non puo superare 50 caratteri.")]
    public string? TitolareCitta { get; set; } = "";

    [Display(Name = "Provincia")]
    [StringLength(2, ErrorMessage = "Il campo Provincia non puo superare 2 caratteri.")]
    public string? TitolareProvincia { get; set; } = "";

    [Display(Name = "Cap")]
    [StringLength(5, ErrorMessage = "Il campo CAP non puo superare 5 caratteri.")]
    public string? TitolareCap { get; set; } = "";

    [Display(Name = "Indirizzo")]
    [StringLength(80, ErrorMessage = "Il campo Indirizzo non puo superare 80 caratteri.")]
    public string? TitolareIndirizzo { get; set; } = "";

    [Display(Name = "Telefono")]
    [StringLength(40, ErrorMessage = "Il campo Telefono non puo superare 40 caratteri.")]
    public string? TitolareTelefono { get; set; } = "";

    [Display(Name = "E-mail")]
    [StringLength(100, ErrorMessage = "Il campo E-mail non puo superare 100 caratteri.")]
    public string? TitolareEmail { get; set; } = "";

    [Display(Name = "PEC")]
    [StringLength(100, ErrorMessage = "Il campo PEC non puo superare 100 caratteri.")]
    public string? TitolarePec { get; set; } = "";

    [Display(Name = "Esenzione IVA")]
    public bool AttivitaEsenzioneIva { get; set; }

    [Display(Name = "Codice esenzione IVA")]
    [StringLength(10, ErrorMessage = "Il campo Codice esenzione IVA non puo superare 10 caratteri.")]
    public string? CodiceEsenzioneIva { get; set; } = "";

    [Display(Name = "IVA spese incasso")]
    [StringLength(10, ErrorMessage = "Il campo IVA spese incasso non puo superare 10 caratteri.")]
    public string? IvaSpeseIncasso { get; set; } = "";

    [Display(Name = "Aliquota IVA vendite")]
    [StringLength(10, ErrorMessage = "Il campo Aliquota IVA vendite non puo superare 10 caratteri.")]
    public string? AliqIvaVendite { get; set; } = "";

    [Display(Name = "Aliquota IVA articoli")]
    [StringLength(10, ErrorMessage = "Il campo Aliquota IVA articoli non puo superare 10 caratteri.")]
    public string? AliquotaRitenutaIrpef { get; set; } = "";

    [Display(Name = " ")]
    [StringLength(10, ErrorMessage = "Il campo riservato non puo superare 10 caratteri.")]
    public string? AliquotaContributiInps { get; set; } = "";

    [Display(Name = " ")]
    [StringLength(20, ErrorMessage = "Il campo riservato non puo superare 20 caratteri.")]
    public string? QuotaAperturaContratto { get; set; } = "";

    [Display(Name = " ")]
    [StringLength(20, ErrorMessage = "Il campo riservato non puo superare 20 caratteri.")]
    public string? QuotaStandardContratto { get; set; } = "";

    [Display(Name = "Aliquota IVA")]
    [StringLength(10, ErrorMessage = "Il campo Aliquota IVA non puo superare 10 caratteri.")]
    public string? FattAliquotaIva { get; set; } = "";

    [Display(Name = "Pagamento standard")]
    [StringLength(50, ErrorMessage = "Il campo Pagamento standard non puo superare 50 caratteri.")]
    public string? FattPagamentoStandard { get; set; } = "";

    [Display(Name = "Codice IBAN")]
    [StringLength(50, ErrorMessage = "Il campo Codice IBAN non puo superare 50 caratteri.")]
    public string? FattIban { get; set; } = "";

    [Display(Name = "Codice SWIFT")]
    [StringLength(12, ErrorMessage = "Il campo Codice SWIFT non puo superare 12 caratteri.")]
    public string? FattSwift { get; set; } = "";

    [Display(Name = "Banca")]
    [StringLength(50, ErrorMessage = "Il campo Banca non puo superare 50 caratteri.")]
    public string? FattBanca { get; set; } = "";

    [Display(Name = "Note in fattura")]
    [StringLength(255, ErrorMessage = "Il campo Note in fattura non puo superare 255 caratteri.")]
    public string? FattDescrizioneStandard { get; set; } = "";

    [Display(Name = "Descrizione fattura di apertura")]
    [StringLength(255, ErrorMessage = "Il campo Descrizione fattura di apertura non puo superare 255 caratteri.")]
    public string? FattDescrizioneApertura { get; set; } = "";

    [Display(Name = "Note informativa privacy")]
    [StringLength(1000, ErrorMessage = "Il campo Note informativa privacy non puo superare 1000 caratteri.")]
    public string? FattNotePrivacy { get; set; } = "";

    [Display(Name = "Messaggio di posta elettronica")]
    [StringLength(255, ErrorMessage = "Il campo Messaggio di posta elettronica non puo superare 255 caratteri.")]
    public string? FattMessaggioEmail { get; set; } = "";

    [Display(Name = "Matrice nome XML")]
    [StringLength(255, ErrorMessage = "Il campo Matrice nome XML non puo superare 255 caratteri.")]
    public string? FeMatriceNomeXml { get; set; } = "";

    [Display(Name = "Regime fiscale")]
    [StringLength(10, ErrorMessage = "Il campo Regime fiscale non puo superare 10 caratteri.")]
    public string? FeRegimeFiscale { get; set; } = "";

    [Display(Name = "Tipo ritenuta")]
    [StringLength(10, ErrorMessage = "Il campo Tipo ritenuta non puo superare 10 caratteri.")]
    public string? FeTipoRitenuta { get; set; } = "";

    [Display(Name = "Causale ritenuta")]
    [StringLength(10, ErrorMessage = "Il campo Causale ritenuta non puo superare 10 caratteri.")]
    public string? FeCausaleRitenuta { get; set; } = "";

    [Display(Name = "Nome mittente")]
    [StringLength(100, ErrorMessage = "Il campo Nome mittente non puo superare 100 caratteri.")]
    public string? MailOrdNomeMittente { get; set; } = "";

    [Display(Name = "E-mail mittente")]
    [StringLength(100, ErrorMessage = "Il campo E-mail mittente non puo superare 100 caratteri.")]
    public string? MailOrdEmailMittente { get; set; } = "";

    [Display(Name = "Server SMTP")]
    [StringLength(100, ErrorMessage = "Il campo Server SMTP non puo superare 100 caratteri.")]
    public string? MailOrdServerSmtp { get; set; } = "";

    [Display(Name = "Porta SMTP")]
    [StringLength(5, ErrorMessage = "Il campo Porta SMTP non puo superare 5 caratteri.")]
    public string? MailOrdPortaSmtp { get; set; } = "";

    [Display(Name = "Autenticazione")]
    public bool MailOrdAutenticazione { get; set; }

    [Display(Name = "Sicurezza")]
    [StringLength(10, ErrorMessage = "Il campo Sicurezza non puo superare 10 caratteri.")]
    public string? MailOrdSicurezza { get; set; } = "";

    [Display(Name = "Nome utente")]
    [StringLength(100, ErrorMessage = "Il campo Nome utente non puo superare 100 caratteri.")]
    public string? MailOrdUsername { get; set; } = "";

    [Display(Name = "Password")]
    [StringLength(100, ErrorMessage = "Il campo Password non puo superare 100 caratteri.")]
    public string? MailOrdPassword { get; set; } = "";

    [Display(Name = "Nome mittente")]
    [StringLength(100, ErrorMessage = "Il campo Nome mittente non puo superare 100 caratteri.")]
    public string? MailPecNomeMittente { get; set; } = "";

    [Display(Name = "E-mail mittente")]
    [StringLength(100, ErrorMessage = "Il campo E-mail mittente non puo superare 100 caratteri.")]
    public string? MailPecEmailMittente { get; set; } = "";

    [Display(Name = "Server SMTP")]
    [StringLength(100, ErrorMessage = "Il campo Server SMTP non puo superare 100 caratteri.")]
    public string? MailPecServerSmtp { get; set; } = "";

    [Display(Name = "Porta SMTP")]
    [StringLength(5, ErrorMessage = "Il campo Porta SMTP non puo superare 5 caratteri.")]
    public string? MailPecPortaSmtp { get; set; } = "";

    [Display(Name = "Autenticazione")]
    public bool MailPecAutenticazione { get; set; }

    [Display(Name = "Sicurezza")]
    [StringLength(10, ErrorMessage = "Il campo Sicurezza non puo superare 10 caratteri.")]
    public string? MailPecSicurezza { get; set; } = "";

    [Display(Name = "Nome utente")]
    [StringLength(100, ErrorMessage = "Il campo Nome utente non puo superare 100 caratteri.")]
    public string? MailPecUsername { get; set; } = "";

    [Display(Name = "Password")]
    [StringLength(100, ErrorMessage = "Il campo Password non puo superare 100 caratteri.")]
    public string? MailPecPassword { get; set; } = "";

    [Display(Name = "Copie automatiche")]
    public bool BackupAbilitaAutomatica { get; set; }

    [Display(Name = "Posizione copie")]
    [StringLength(260, ErrorMessage = "Il campo Posizione copie non puo superare 260 caratteri.")]
    public string? BackupPosizione { get; set; } = "";

    public bool BackupLunedi { get; set; }
    public bool BackupMartedi { get; set; }
    public bool BackupMercoledi { get; set; }
    public bool BackupGiovedi { get; set; }
    public bool BackupVenerdi { get; set; }
    public bool BackupSabato { get; set; }
    public bool BackupDomenica { get; set; }

    [Display(Name = "Orario copie")]
    [StringLength(5, ErrorMessage = "Il campo Orario copie non puo superare 5 caratteri.")]
    public string? BackupOrario { get; set; } = "";

    [Display(Name = "Cancella vecchie")]
    public bool BackupCancellaVecchie { get; set; }

    [Display(Name = "Giorni conservazione")]
    [StringLength(3, ErrorMessage = "Il campo Giorni conservazione non puo superare 3 caratteri.")]
    public string? BackupGiorniVecchie { get; set; } = "";

    [Display(Name = "Dati azienda")]
    [StringLength(500, ErrorMessage = "Il campo Dati azienda non puo superare 500 caratteri.")]
    public string? StampaDatiAzienda { get; set; } = "";

    [Display(Name = "Font")]
    [StringLength(50, ErrorMessage = "Il campo Font non puo superare 50 caratteri.")]
    public string? StampaFontName { get; set; } = "";

    [Display(Name = "Dimensione")]
    [StringLength(3, ErrorMessage = "Il campo Dimensione non puo superare 3 caratteri.")]
    public string? StampaFontSize { get; set; } = "";

    [Display(Name = "Allineamento")]
    [StringLength(1, ErrorMessage = "Il campo Allineamento non puo superare 1 carattere.")]
    public string? StampaAlign { get; set; } = "";

    [Display(Name = "Testo timbro")]
    [StringLength(500, ErrorMessage = "Il campo Testo timbro non puo superare 500 caratteri.")]
    public string? TimbroTxt { get; set; } = "";
}


