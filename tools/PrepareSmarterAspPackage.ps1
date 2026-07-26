$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$version = '1.26.07.24'
$source = Join-Path $root "publish\READY-TO-UPLOAD\000_MicronoteFood-v$version"
$zip = Join-Path $root "publish\READY-TO-UPLOAD\000_UPLOAD-MicronoteFood-v$version.zip"

if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw "Cartella pubblicata non trovata: $source"
}
if (Test-Path -LiteralPath $zip) {
    throw "ZIP già esistente: $zip"
}

$required = @(
    'MicronoteFood.Web.exe',
    'MicronoteFood.Web.dll',
    'appsettings.json',
    'appsettings.Production.json',
    'web.config',
    'wwwroot'
)
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $name))) {
        throw "Elemento obbligatorio mancante: $name"
    }
}

$forbiddenNames = @(
    'appsettings.Development.json',
    'appsettings.Secrets.json',
    'CONNESSIONE-PRODUZIONE.promemoria.txt'
)
$forbidden = Get-ChildItem -LiteralPath $source -Recurse -Force |
    Where-Object { $forbiddenNames -contains $_.Name }
if ($forbidden) {
    throw "File riservati/non pubblicabili trovati: $($forbidden.FullName -join ', ')"
}

$versionInfo = (Get-Item -LiteralPath (Join-Path $source 'MicronoteFood.Web.exe')).VersionInfo
if ($versionInfo.FileVersion -ne '1.26.7.24') {
    throw "Versione eseguibile inattesa: $($versionInfo.FileVersion)"
}

Compress-Archive -Path (Join-Path $source '*') -DestinationPath $zip -CompressionLevel Optimal

$archive = Get-Item -LiteralPath $zip
$hash = Get-FileHash -LiteralPath $zip -Algorithm SHA256
$fileCount = (Get-ChildItem -LiteralPath $source -Recurse -File).Count

Write-Output "Versione: $($versionInfo.FileVersion)"
Write-Output "File pubblicati: $fileCount"
Write-Output "ZIP: $($archive.FullName)"
Write-Output "Dimensione: $($archive.Length) byte"
Write-Output "SHA256: $($hash.Hash)"
