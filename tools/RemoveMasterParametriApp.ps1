$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $root 'src\MicronoteFood.Web\MicronoteFood.Web.csproj'
$toolProject = Join-Path $root 'tools\RemoveMasterParametriApp\RemoveMasterParametriApp.csproj'
$prefix = 'ConnectionStrings:MicronoteDb = '

$secret = dotnet user-secrets list --project $webProject |
    Where-Object { $_.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) } |
    Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($secret)) {
    throw "Connessione locale non trovata."
}

$previous = $env:MICRONOTE_DB_CONNECTION
$env:MICRONOTE_DB_CONNECTION = $secret.Substring($prefix.Length)
try {
    dotnet run --project $toolProject
}
finally {
    $env:MICRONOTE_DB_CONNECTION = $previous
}
