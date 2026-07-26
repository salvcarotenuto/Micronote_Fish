param(
    [int]$Port = 5210
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\MicronoteFood.Web\MicronoteFood.Web.csproj"
$outLog = Join-Path $root "micronote-fish-run.out.log"
$errLog = Join-Path $root "micronote-fish-run.err.log"
$pidFile = Join-Path $root "micronote-fish-run.pid"
$url = "http://localhost:$Port"

Write-Host "Avvio Micronote Fish su $url"

if (Test-Path -LiteralPath $pidFile) {
    $previousPid = [int](Get-Content -LiteralPath $pidFile -Raw)
    Stop-Process -Id $previousPid -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue
}

$process = Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "--project", $project, "--urls", $url) `
    -WorkingDirectory $root `
    -RedirectStandardOutput $outLog `
    -RedirectStandardError $errLog `
    -WindowStyle Hidden `
    -PassThru

Set-Content -LiteralPath $pidFile -Value $process.Id

$ready = $false
for ($attempt = 1; $attempt -le 30; $attempt++) {
    Start-Sleep -Milliseconds 500

    try {
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -eq 200) {
            $ready = $true
            break
        }
    }
    catch {
        # Il server sta ancora compilando o avviandosi.
    }
}

if ($ready) {
    Write-Host "Micronote Fish pronto: $url"
    Start-Process $url
    exit 0
}

Write-Host "Micronote Fish non ha risposto entro il tempo previsto."
Write-Host "Controllare i log:"
Write-Host "  $outLog"
Write-Host "  $errLog"
exit 1
