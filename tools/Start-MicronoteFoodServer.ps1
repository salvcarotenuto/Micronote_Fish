$ErrorActionPreference = 'Stop'

$projectPath = 'C:\Codex\Micronote_Food\app\src\MicronoteFood.Web\MicronoteFood.Web.csproj'
$workingDirectory = 'C:\Codex\Micronote_Food\app'
$dotnetPath = 'C:\Program Files\dotnet\dotnet.exe'
$url = 'http://localhost:5209'
$outLog = 'C:\Codex\Micronote_Food\app\micronote-run.out.log'
$errLog = 'C:\Codex\Micronote_Food\app\micronote-run.err.log'

try {
    $response = Invoke-WebRequest -Uri "$url/PrimaNota" -UseBasicParsing -TimeoutSec 3
    if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
        Start-Process $url
        exit 0
    }
}
catch {
    # Server not running yet.
}

Start-Process `
    -FilePath $dotnetPath `
    -ArgumentList @('run', '--project', $projectPath, '--urls', $url) `
    -WorkingDirectory $workingDirectory `
    -RedirectStandardOutput $outLog `
    -RedirectStandardError $errLog `
    -WindowStyle Hidden

for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Milliseconds 500
    try {
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
            Start-Process $url
            exit 0
        }
    }
    catch {
        # Waiting for the web server.
    }
}

