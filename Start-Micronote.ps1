param(
    [int]$Port = 5210,
    [string]$OpenPath = "/",
    [switch]$NoBuild,
    [switch]$NoBrowser,
    [switch]$NoWatch
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\MicronoteFood.Web\MicronoteFood.Web.csproj"
$projectFullPath = [System.IO.Path]::GetFullPath($project)
$outLog = Join-Path $root "micronote-fish-run.out.log"
$errLog = Join-Path $root "micronote-fish-run.err.log"
$pidFile = Join-Path $root "micronote-fish-run.pid"
$baseUrl = "http://localhost:$Port"

if (-not $OpenPath.StartsWith("/")) {
    $OpenPath = "/$OpenPath"
}

$targetUrl = "$baseUrl$OpenPath"

function Stop-MicronoteProcess {
    $processes = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
        ($_.Name -eq "dotnet.exe" -and $_.CommandLine -and $_.CommandLine.Contains($projectFullPath)) -or
        ($_.Name -eq "MicronoteFood.Web.exe" -and $_.ExecutablePath -and $_.ExecutablePath.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase))
    }

    foreach ($item in $processes) {
        Stop-Process -Id $item.ProcessId -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path -LiteralPath $pidFile) {
        $storedPid = 0
        if ([int]::TryParse((Get-Content -LiteralPath $pidFile -Raw).Trim(), [ref]$storedPid)) {
            Stop-Process -Id $storedPid -Force -ErrorAction SilentlyContinue
        }
        Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue
    }

    foreach ($attempt in 1..20) {
        $stillRunning = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
            ($_.Name -eq "dotnet.exe" -and $_.CommandLine -and $_.CommandLine.Contains($projectFullPath)) -or
            ($_.Name -eq "MicronoteFood.Web.exe" -and $_.ExecutablePath -and $_.ExecutablePath.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase))
        }
        if (-not $stillRunning) {
            break
        }
        Start-Sleep -Milliseconds 250
    }
}

Write-Host "Riavvio Micronote Fish su $baseUrl"
Stop-MicronoteProcess

if (-not $NoBuild) {
    Write-Host "Compilazione dell'applicazione..."
    $configuration = if ($NoWatch) { "Release" } else { "Debug" }
    & dotnet build $project -c $configuration --no-restore --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Compilazione non riuscita. Il browser non verra aperto."
    }
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER = "1"
$env:DOTNET_WATCH_RESTART_ON_RUDE_EDIT = "1"

$runArguments = if ($NoWatch) {
    @("run", "--no-build", "-c", "Release", "--no-launch-profile", "--project", $project, "--urls", $baseUrl)
} else {
    @("watch", "--project", $project, "run", "--no-launch-profile", "--urls", $baseUrl)
}

$process = Start-Process -FilePath "dotnet" `
    -ArgumentList $runArguments `
    -WorkingDirectory $root `
    -RedirectStandardOutput $outLog `
    -RedirectStandardError $errLog `
    -WindowStyle Hidden `
    -PassThru

Set-Content -LiteralPath $pidFile -Value $process.Id

$ready = $false
foreach ($attempt in 1..120) {
    if ($process.HasExited) {
        break
    }

    try {
        $response = Invoke-WebRequest -Uri $baseUrl -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
            $ready = $true
            break
        }
    }
    catch {
        Start-Sleep -Milliseconds 500
    }
}

if (-not $ready) {
    Write-Host "Micronote Fish non ha risposto entro il tempo previsto."
    Write-Host "Controllare i log:"
    Write-Host "  $outLog"
    Write-Host "  $errLog"
    exit 1
}

Write-Host "Micronote Fish pronto: $targetUrl"
if (-not $NoBrowser) {
    Start-Process $targetUrl
}

exit 0
