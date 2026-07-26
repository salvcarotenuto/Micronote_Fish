$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$webProject = Join-Path $root 'src\MicronoteFood.Web\MicronoteFood.Web.csproj'
$toolProject = Join-Path $PSScriptRoot 'ImportLegacySuppliers.csproj'
$prefixes = @(
    'ConnectionStrings:MicronoteServer = ',
    'ConnectionStrings:MicronoteDb = '
)

if ($args.Count -ne 1) {
    throw 'Specificare il percorso del file TSV.'
}

$connection = $null
$secrets = dotnet user-secrets list --project $webProject
foreach ($prefix in $prefixes) {
    $item = $secrets |
        Where-Object { $_.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -First 1
    if (-not [string]::IsNullOrWhiteSpace($item)) {
        $connection = $item.Substring($prefix.Length)
        break
    }
}
if ([string]::IsNullOrWhiteSpace($connection)) {
    throw 'Connessione MySQL locale non trovata nei segreti del progetto.'
}

$previous = $env:MICROFISH_DB_CONNECTION
$env:MICROFISH_DB_CONNECTION = $connection
try {
    dotnet run --project $toolProject -- $args[0]
}
finally {
    $env:MICROFISH_DB_CONNECTION = $previous
}
