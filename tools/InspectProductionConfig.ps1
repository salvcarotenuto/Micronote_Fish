$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$candidates = @(
    (Join-Path $root 'publish\CONNESSIONE-PRODUZIONE.promemoria.txt'),
    (Join-Path $root 'src\MicronoteFood.Web\appsettings.Production.json')
)

foreach ($path in $candidates) {
    if (-not (Test-Path -LiteralPath $path)) {
        continue
    }

    $content = Get-Content -LiteralPath $path -Raw
    $redacted = [regex]::Replace(
        $content,
        '(?i)(password|pwd|user\s*id|uid)\s*=\s*[^;"\r\n]+',
        '$1=***')
    $redacted = [regex]::Replace(
        $redacted,
        '(?i)("?(?:password|pwd|user\s*id|uid)"?\s*:\s*")[^"]*',
        '$1***')

    Write-Output "FILE: $path"
    $redacted -split "`r?`n" |
        Where-Object {
            $_ -match '(?i)server|host|database|initial catalog|connection|stringa|smarter'
        } |
        ForEach-Object { Write-Output $_ }
}
