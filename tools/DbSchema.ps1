$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\MicronoteFood.Web\MicronoteFood.Web.csproj'
$toolProject = Join-Path $root 'tools\DbSchema\DbSchema.csproj'
$prefix = 'ConnectionStrings:MicronoteDb = '

$secret = dotnet user-secrets list --project $project |
    Where-Object { $_.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($secret)) {
    throw "Stringa di connessione 'ConnectionStrings:MicronoteDb' non trovata nei user-secrets del progetto web."
}

$previous = $env:MICRONOTE_DB_CONNECTION
$env:MICRONOTE_DB_CONNECTION = $secret.Substring($prefix.Length)
try {
    dotnet run --project $toolProject -- $args
}
finally {
    $env:MICRONOTE_DB_CONNECTION = $previous
}
