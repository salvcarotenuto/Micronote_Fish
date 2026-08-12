param(
    [string]$OpenPath = "/"
)

$ErrorActionPreference = "Stop"

$appRoot = Split-Path -Parent $PSScriptRoot
$launcher = Join-Path $appRoot "Start-Micronote.ps1"

& $launcher -Port 5210 -OpenPath $OpenPath
exit $LASTEXITCODE
