$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $root 'src\MicronoteFood.Web\MicronoteFood.Web.csproj'
$toolProject = Join-Path $root 'tools\DropVenditeLegacyTotals\DropVenditeLegacyTotals.csproj'
$productionSettings = Join-Path $root 'src\MicronoteFood.Web\appsettings.Production.json'
$prefix = 'ConnectionStrings:MicronoteDb = '

$secret = dotnet user-secrets list --project $webProject |
    Where-Object { $_.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) } |
    Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($secret)) { throw "Connessione locale non trovata." }
$settings = Get-Content -LiteralPath $productionSettings -Raw | ConvertFrom-Json
$remote = $settings.ConnectionStrings.MicronoteServer
if ([string]::IsNullOrWhiteSpace($remote)) { throw "Connessione remota non trovata." }

$previousLocal = $env:MICRONOTE_LOCAL_DB_CONNECTION
$previousRemote = $env:MICRONOTE_REMOTE_DB_CONNECTION
$env:MICRONOTE_LOCAL_DB_CONNECTION = $secret.Substring($prefix.Length)
$env:MICRONOTE_REMOTE_DB_CONNECTION = $remote
try {
    dotnet run --project $toolProject -- $args
}
finally {
    $env:MICRONOTE_LOCAL_DB_CONNECTION = $previousLocal
    $env:MICRONOTE_REMOTE_DB_CONNECTION = $previousRemote
}
