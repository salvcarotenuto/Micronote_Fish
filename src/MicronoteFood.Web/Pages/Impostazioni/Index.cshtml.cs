using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicronoteFood.Web.Pages.Impostazioni;

public sealed class IndexModel(SettingsRepository repository, PaymentCodeRepository paymentCodes, MicronoteDb database) : PageModel
{
    [BindProperty]
    [ValidateNever]
    public SettingsEditModel Settings { get; set; } = new();

    [BindProperty]
    public int ActiveTab { get; set; }

    public bool Saved { get; private set; }

    public SelectList PaymentOptions { get; private set; } = EmptySelectList();
    public SelectList FeRegimeOptions { get; private set; } = EmptySelectList();
    public SelectList FeWithholdingTypeOptions { get; private set; } = EmptySelectList();
    public SelectList FeWithholdingCauseOptions { get; private set; } = EmptySelectList();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Settings = await repository.GetAsync(cancellationToken);
        NormalizeSmtpSecurityOptions();
        await LoadLookupsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Normalize();
        ValidateSettings();

        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        await repository.SaveAsync(Settings, cancellationToken);
        await LoadLookupsAsync(cancellationToken);
        Saved = true;
        return Page();
    }


    public async Task<IActionResult> OnPostTestSmtpAsync(CancellationToken cancellationToken)
    {
        SmtpTestRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<SmtpTestRequest>(Request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return new JsonResult(new SmtpTestResponse(false, "Richiesta di test SMTP non valida."));
        }

        request ??= new SmtpTestRequest();
        var server = (request.Server ?? "").Trim();
        if (server.Length == 0)
        {
            return new JsonResult(new SmtpTestResponse(false, "Server SMTP non indicato."));
        }

        if (!int.TryParse((request.Port ?? "").Trim(), out var port) || port <= 0 || port > 65535)
        {
            return new JsonResult(new SmtpTestResponse(false, "Porta SMTP non valida."));
        }

        if (request.Authentication && string.IsNullOrWhiteSpace(request.Username))
        {
            return new JsonResult(new SmtpTestResponse(false, "Nome utente obbligatorio quando l'autenticazione e' attiva."));
        }

        try
        {
            await TestSmtpConnectionAsync(request, server, port, cancellationToken);
            return new JsonResult(new SmtpTestResponse(true, "Il server SMTP ha risposto correttamente."));
        }
        catch (OperationCanceledException)
        {
            return new JsonResult(new SmtpTestResponse(false, "Tempo massimo di connessione superato."));
        }
        catch (Exception ex)
        {
            return new JsonResult(new SmtpTestResponse(false, FormatSmtpError(ex.Message, server, port, request)));
        }
    }

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        await LoadPaymentOptionsAsync(cancellationToken);
        await LoadFeRegimeOptionsAsync(cancellationToken);
        await LoadFeWithholdingTypeOptionsAsync(cancellationToken);
        await LoadFeWithholdingCauseOptionsAsync(cancellationToken);
    }

    private async Task LoadFeRegimeOptionsAsync(CancellationToken cancellationToken)
    {
        FeRegimeOptions = await LoadCodeDescriptionOptionsAsync(
            "feregimif",
            Settings.FeRegimeFiscale,
            cancellationToken);
    }

    private async Task LoadFeWithholdingTypeOptionsAsync(CancellationToken cancellationToken)
    {
        FeWithholdingTypeOptions = await LoadCodeDescriptionOptionsAsync(
            "fetiporit",
            Settings.FeTipoRitenuta,
            cancellationToken);
    }

    private async Task LoadFeWithholdingCauseOptionsAsync(CancellationToken cancellationToken)
    {
        FeWithholdingCauseOptions = await LoadCodeDescriptionOptionsAsync(
            "fecausrit",
            Settings.FeCausaleRitenuta,
            cancellationToken);
    }

    private async Task<SelectList> LoadCodeDescriptionOptionsAsync(
        string tableName,
        string? selectedValue,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                COALESCE(Codice, '') AS Codice,
                COALESCE(Descrizione, '') AS Descrizione
            FROM {tableName}
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        var options = new List<SelectListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = reader.GetString("Codice").Trim().ToUpperInvariant();
            var description = reader.GetString("Descrizione").Trim();
            if (code.Length == 0)
            {
                continue;
            }

            options.Add(new SelectListItem
            {
                Value = code,
                Text = string.IsNullOrWhiteSpace(description) ? code : $"{code} - {description}"
            });
        }

        return new SelectList(options, "Value", "Text", FiscalCodeService.NormalizeFiscalCode(selectedValue));
    }

    private async Task LoadPaymentOptionsAsync(CancellationToken cancellationToken)
    {
        var payments = await paymentCodes.ListAsync(cancellationToken);
        var options = payments.Select(payment => new SelectListItem
        {
            Value = payment.Code.ToString("000"),
            Text = $"{payment.Code:000} - {payment.Description}"
        });

        var selectedPayment = DigitsOnly(Settings.FattPagamentoStandard, 3).PadLeft(3, '0');
        if (selectedPayment == "000")
        {
            selectedPayment = "";
        }

        PaymentOptions = new SelectList(options, "Value", "Text", selectedPayment);
    }

    private static SelectList EmptySelectList() => new(Array.Empty<SelectListItem>(), "Value", "Text");

    private void Normalize()
    {
        Settings.CodiceFiscale = FiscalCodeService.NormalizeFiscalCode(Settings.CodiceFiscale);
        Settings.PartitaIva = FiscalCodeService.NormalizeVatNumber(Settings.PartitaIva);
        Settings.NumeroRea = FiscalCodeService.NormalizeFiscalCode(Settings.NumeroRea);
        Settings.SedeLegaleProvincia = FiscalCodeService.NormalizeFiscalCode(Settings.SedeLegaleProvincia);
        Settings.SedeLegaleCap = DigitsOnly(Settings.SedeLegaleCap, 5);
        Settings.SedeOperativaProvincia = FiscalCodeService.NormalizeFiscalCode(Settings.SedeOperativaProvincia);
        Settings.SedeOperativaCap = DigitsOnly(Settings.SedeOperativaCap, 5);
        Settings.FeCodiceSdiAzienda = FiscalCodeService.NormalizeFiscalCode(Settings.FeCodiceSdiAzienda);
        Settings.TitolareCodiceFiscale = FiscalCodeService.NormalizeFiscalCode(Settings.TitolareCodiceFiscale);
        Settings.TitolareProvincia = FiscalCodeService.NormalizeFiscalCode(Settings.TitolareProvincia);
        Settings.TitolareCap = DigitsOnly(Settings.TitolareCap, 5);
        Settings.CodiceEsenzioneIva = FiscalCodeService.NormalizeFiscalCode(Settings.CodiceEsenzioneIva);
        Settings.FattIban = FiscalCodeService.NormalizeFiscalCode(Settings.FattIban);
        Settings.FattSwift = FiscalCodeService.NormalizeFiscalCode(Settings.FattSwift);
        Settings.FattPagamentoStandard = DigitsOnly(Settings.FattPagamentoStandard, 3).PadLeft(3, '0');
        if (Settings.FattPagamentoStandard == "000")
        {
            Settings.FattPagamentoStandard = "";
        }
        Settings.FeMatriceNomeXml = NormalizeXmlNameMatrix(Settings.FeMatriceNomeXml, Settings.PartitaIva);
        Settings.FeRegimeFiscale = FiscalCodeService.NormalizeFiscalCode(Settings.FeRegimeFiscale);
        Settings.FeTipoRitenuta = FiscalCodeService.NormalizeFiscalCode(Settings.FeTipoRitenuta);
        Settings.FeCausaleRitenuta = FiscalCodeService.NormalizeFiscalCode(Settings.FeCausaleRitenuta);
        Settings.MailOrdPortaSmtp = DigitsOnly(Settings.MailOrdPortaSmtp, 5);
        Settings.MailPecPortaSmtp = DigitsOnly(Settings.MailPecPortaSmtp, 5);
        NormalizeSmtpSecurityOptions();
        Settings.BackupGiorniVecchie = DigitsOnly(Settings.BackupGiorniVecchie, 3);
        Settings.AzPec = (Settings.AzPec ?? "").Trim().ToLowerInvariant();
        Settings.AzEmail = (Settings.AzEmail ?? "").Trim().ToLowerInvariant();
        Settings.FePecDestinazioneSdi = (Settings.FePecDestinazioneSdi ?? "").Trim().ToLowerInvariant();
        Settings.TitolareEmail = (Settings.TitolareEmail ?? "").Trim().ToLowerInvariant();
        Settings.TitolarePec = (Settings.TitolarePec ?? "").Trim().ToLowerInvariant();
        Settings.MailOrdEmailMittente = (Settings.MailOrdEmailMittente ?? "").Trim().ToLowerInvariant();
        Settings.MailPecEmailMittente = (Settings.MailPecEmailMittente ?? "").Trim().ToLowerInvariant();
    }

    private static string FormatSmtpError(string message, string server, int port, SmtpTestRequest request)
    {
        var security = NormalizeSmtpSecurity(request.Security) switch
        {
            "1" => "Nessuna",
            "2" => "STARTTLS",
            "3" => "SSL/TLS",
            _ => "non indicata"
        };
        var authentication = request.Authentication ? "si" : "no";
        return $"{message}\n\nParametri usati dalla maschera: server {server}, porta {port}, sicurezza {security}, autenticazione {authentication}.";
    }
    private static async Task TestSmtpConnectionAsync(
        SmtpTestRequest request,
        string server,
        int port,
        CancellationToken cancellationToken)
    {
        var security = NormalizeSmtpSecurity(request.Security);
        if (security == "")
        {
            throw new InvalidOperationException("Indicare il tipo di sicurezza SMTP.");
        }

        if (security is not ("1" or "2" or "3"))
        {
            throw new InvalidOperationException("Tipo di sicurezza SMTP non valido.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        using var client = new TcpClient();
        await client.ConnectAsync(server, port, timeout.Token);
        await using var networkStream = client.GetStream();

        if (security == "3")
        {
            await using var sslStream = new SslStream(networkStream, false, (_, _, _, _) => true);
            await sslStream.AuthenticateAsClientAsync(server);
            using var reader = new StreamReader(sslStream, Encoding.ASCII, leaveOpen: true);
            await using var writer = new StreamWriter(sslStream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
            await CompleteSmtpHandshakeAsync(reader, writer, request, timeout.Token);
            return;
        }

        using var plainReader = new StreamReader(networkStream, Encoding.ASCII, leaveOpen: true);
        await using var plainWriter = new StreamWriter(networkStream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
        await ExpectSmtpCodeAsync(plainReader, 220, timeout.Token);
        await SendSmtpCommandAsync(plainReader, plainWriter, "EHLO micronote.local", 250, timeout.Token);

        if (security == "2")
        {
            await SendSmtpCommandAsync(plainReader, plainWriter, "STARTTLS", 220, timeout.Token);
            await using var sslStream = new SslStream(networkStream, false, (_, _, _, _) => true);
            await sslStream.AuthenticateAsClientAsync(server);
            using var tlsReader = new StreamReader(sslStream, Encoding.ASCII, leaveOpen: true);
            await using var tlsWriter = new StreamWriter(sslStream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
            await SendSmtpCommandAsync(tlsReader, tlsWriter, "EHLO micronote.local", 250, timeout.Token);
            if (request.Authentication)
            {
                await AuthenticateSmtpAsync(tlsReader, tlsWriter, request, timeout.Token);
            }

            await WriteSmtpCommandAsync(tlsWriter, "QUIT", timeout.Token);
            return;
        }

        if (request.Authentication)
        {
            await AuthenticateSmtpAsync(plainReader, plainWriter, request, timeout.Token);
        }

        await WriteSmtpCommandAsync(plainWriter, "QUIT", timeout.Token);
    }

    private static async Task CompleteSmtpHandshakeAsync(
        StreamReader reader,
        StreamWriter writer,
        SmtpTestRequest request,
        CancellationToken cancellationToken)
    {
        await ExpectSmtpCodeAsync(reader, 220, cancellationToken);
        await SendSmtpCommandAsync(reader, writer, "EHLO micronote.local", 250, cancellationToken);
        if (request.Authentication)
        {
            await AuthenticateSmtpAsync(reader, writer, request, cancellationToken);
        }

        await WriteSmtpCommandAsync(writer, "QUIT", cancellationToken);
    }

    private static async Task AuthenticateSmtpAsync(
        StreamReader reader,
        StreamWriter writer,
        SmtpTestRequest request,
        CancellationToken cancellationToken)
    {
        await SendSmtpCommandAsync(reader, writer, "AUTH LOGIN", 334, cancellationToken);
        await SendSmtpCommandAsync(reader, writer, Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Username ?? "")), 334, cancellationToken);
        await SendSmtpCommandAsync(reader, writer, Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Password ?? "")), 235, cancellationToken);
    }

    private static async Task SendSmtpCommandAsync(
        StreamReader reader,
        StreamWriter writer,
        string command,
        int expectedCode,
        CancellationToken cancellationToken)
    {
        await WriteSmtpCommandAsync(writer, command, cancellationToken);
        await ExpectSmtpCodeAsync(reader, expectedCode, cancellationToken);
    }

    private static async Task WriteSmtpCommandAsync(StreamWriter writer, string command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await writer.WriteLineAsync(command);
        await writer.FlushAsync();
    }

    private static async Task ExpectSmtpCodeAsync(StreamReader reader, int expectedCode, CancellationToken cancellationToken)
    {
        var response = await ReadSmtpResponseAsync(reader, cancellationToken);
        if (!response.StartsWith(expectedCode.ToString("000"), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Risposta SMTP inattesa: {response}");
        }
    }

    private static async Task<string> ReadSmtpResponseAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var response = new StringBuilder();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync();
            if (line is null)
            {
                throw new InvalidOperationException("Connessione SMTP chiusa dal server.");
            }

            if (response.Length > 0)
            {
                response.Append(' ');
            }

            response.Append(line);
            if (line.Length < 4 || line[3] != '-')
            {
                return response.ToString();
            }
        }
    }

    public sealed class SmtpTestRequest
    {
        public string? Server { get; set; }
        public string? Port { get; set; }
        public string? Security { get; set; }
        public bool Authentication { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public sealed record SmtpTestResponse(bool Ok, string Message);

    private void NormalizeSmtpSecurityOptions()
    {
        Settings.MailOrdSicurezza = NormalizeSmtpSecurity(Settings.MailOrdSicurezza);
        Settings.MailPecSicurezza = NormalizeSmtpSecurity(Settings.MailPecSicurezza);
    }

    private static string NormalizeSmtpSecurity(string? value)
    {
        return (value ?? "").Trim().ToLowerInvariant() switch
        {
            "" or "0" => "",
            "1" or "none" or "nessuna" => "1",
            "2" or "tls" or "starttls" => "2",
            "3" or "ssl" or "ssl/tls" => "3",
            var other => other
        };
    }
    private static string NormalizeXmlNameMatrix(string? value, string? vatNumber)
    {
        var current = (value ?? "").Trim().ToUpperInvariant();
        var vat = DigitsOnly(vatNumber, 11);
        if (vat.Length == 11 && (current.Length == 0 || Regex.IsMatch(current, @"^IT\d{11}_$")))
        {
            return $"IT{vat}_";
        }

        return current;
    }

    private static string DigitsOnly(string? value, int maxLength) =>
        new((value ?? "").Where(char.IsDigit).Take(maxLength).ToArray());

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(Settings.RagioneSociale))
        {
            ModelState.AddModelError("Settings.RagioneSociale", "Campo Ragione sociale obbligatorio.");
        }

        if (string.IsNullOrWhiteSpace(Settings.CodiceFiscale))
        {
            ModelState.AddModelError("Settings.CodiceFiscale", "Campo Codice fiscale obbligatorio.");
        }

        if (string.IsNullOrWhiteSpace(Settings.PartitaIva))
        {
            ModelState.AddModelError("Settings.PartitaIva", "Campo Partita IVA obbligatorio.");
        }

        if (!string.IsNullOrWhiteSpace(Settings.CodiceFiscale)
            && !FiscalCodeService.IsValidFiscalCode(Settings.CodiceFiscale))
        {
            ModelState.AddModelError(
                "Settings.CodiceFiscale",
                "Codice fiscale non valido.");
        }

        if (!string.IsNullOrWhiteSpace(Settings.PartitaIva)
            && !FiscalCodeService.IsValidVatNumber(Settings.PartitaIva))
        {
            ModelState.AddModelError("Settings.PartitaIva", "Partita IVA non valida.");
        }

        ValidateProvince(Settings.SedeLegaleProvincia, "Settings.SedeLegaleProvincia", "Provincia sede legale");
        ValidatePostalCode(Settings.SedeLegaleCap, "Settings.SedeLegaleCap", "CAP sede legale");
        ValidateProvince(Settings.SedeOperativaProvincia, "Settings.SedeOperativaProvincia", "Provincia sede operativa");
        ValidatePostalCode(Settings.SedeOperativaCap, "Settings.SedeOperativaCap", "CAP sede operativa");
        ValidateProvince(Settings.TitolareProvincia, "Settings.TitolareProvincia", "Provincia titolare");
        ValidatePostalCode(Settings.TitolareCap, "Settings.TitolareCap", "CAP titolare");
        if (string.IsNullOrWhiteSpace(Settings.AzPec))
        {
            ModelState.AddModelError("Settings.AzPec", "Campo PEC obbligatorio.");
        }

        if (string.IsNullOrWhiteSpace(Settings.AzEmail))
        {
            ModelState.AddModelError("Settings.AzEmail", "Campo E-mail obbligatorio.");
        }

        ValidateEmail(Settings.AzPec, "Settings.AzPec", "PEC");
        ValidateEmail(Settings.AzEmail, "Settings.AzEmail", "E-mail");
        ValidateEmail(Settings.FePecDestinazioneSdi, "Settings.FePecDestinazioneSdi", "PEC destinazione SDI");
        ValidateEmail(Settings.TitolareEmail, "Settings.TitolareEmail", "E-mail titolare");
        ValidateEmail(Settings.TitolarePec, "Settings.TitolarePec", "PEC titolare");
        ValidateEmail(Settings.MailOrdEmailMittente, "Settings.MailOrdEmailMittente", "E-mail mittente ordinaria");
        ValidateEmail(Settings.MailPecEmailMittente, "Settings.MailPecEmailMittente", "E-mail mittente PEC");
        ValidateDate(Settings.TitolareDataNascita, "Settings.TitolareDataNascita", "Data di nascita");
        ValidateTime(Settings.BackupOrario, "Settings.BackupOrario", "Orario copie");

        if (!string.IsNullOrWhiteSpace(Settings.FeCodiceSdiAzienda)
            && Settings.FeCodiceSdiAzienda.Length != 7)
        {
            ModelState.AddModelError(
                "Settings.FeCodiceSdiAzienda",
                "Il Codice SDI azienda deve essere di 7 caratteri.");
        }

    }

    private void ValidateProvince(string? value, string key, string label)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Length != 2)
        {
            ModelState.AddModelError(key, $"{label}: inserire 2 lettere.");
        }
    }

    private void ValidatePostalCode(string? value, string key, string label)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Length != 5)
        {
            ModelState.AddModelError(key, $"{label}: inserire 5 cifre.");
        }
    }

    private void ValidateEmail(string? value, string key, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var validator = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
        if (!validator.IsValid(value))
        {
            ModelState.AddModelError(key, $"{label}: indirizzo non valido.");
        }
    }

    private void ValidateDate(string? value, string key, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!DateOnly.TryParse(value, out _))
        {
            ModelState.AddModelError(key, $"{label}: data non valida.");
        }
    }

    private void ValidateTime(string? value, string key, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!Regex.IsMatch(value, @"^\d{2}:\d{2}$"))
        {
            ModelState.AddModelError(key, $"{label}: orario non valido.");
        }
    }
}






