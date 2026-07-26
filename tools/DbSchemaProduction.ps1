$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$toolProject = Join-Path $root 'tools\DbSchema\DbSchema.csproj'
$productionSettings = Join-Path $root 'src\MicronoteFood.Web\appsettings.Production.json'
$settings = Get-Content -LiteralPath $productionSettings -Raw | ConvertFrom-Json
$remote = $settings.ConnectionStrings.MicronoteServer
if ([string]::IsNullOrWhiteSpace($remote)) {
    throw "Connessione remota non trovata."
}

$previous = $env:MICRONOTE_DB_CONNECTION
$env:MICRONOTE_DB_CONNECTION = $remote
try {
    dotnet run --project $toolProject -- $args
}
finally {
    $env:MICRONOTE_DB_CONNECTION = $previous
}
