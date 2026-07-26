using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace MicronoteFood.Web.Services;

public sealed class SmtpConnectionTester
{
    public async Task<SmtpTestResponse> TestAsync(SmtpTestRequest? request, CancellationToken cancellationToken)
    {
        request ??= new SmtpTestRequest();
        var server = (request.Server ?? "").Trim();
        if (server.Length == 0)
        {
            return new SmtpTestResponse(false, "Server SMTP non indicato.");
        }

        if (!int.TryParse((request.Port ?? "").Trim(), out var port) || port <= 0 || port > 65535)
        {
            return new SmtpTestResponse(false, "Porta SMTP non valida.");
        }

        if (request.Authentication && string.IsNullOrWhiteSpace(request.Username))
        {
            return new SmtpTestResponse(false, "Nome utente obbligatorio quando l'autenticazione e' attiva.");
        }

        try
        {
            await TestSmtpConnectionAsync(request, server, port, cancellationToken);
            return new SmtpTestResponse(true, "Il server SMTP ha risposto correttamente.");
        }
        catch (OperationCanceledException)
        {
            return new SmtpTestResponse(false, "Tempo massimo di connessione superato.");
        }
        catch (Exception ex)
        {
            return new SmtpTestResponse(false, FormatSmtpError(ex.Message, server, port, request));
        }
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
        if (security.Length == 0)
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
