<#
.SYNOPSIS
    Runs the suite against a fresh SafetyOpsApp in Docker: reset the app's data, start it, wait
    for /healthz, build, test, and stop it again.

.EXAMPLE
    pwsh scripts/e2e.ps1                                  # app clone next to this repo
    pwsh scripts/e2e.ps1 -AppPath ~/src/SafetyOpsApp -Filter "FullyQualifiedName~Navigation"
    pwsh scripts/e2e.ps1 -KeepApp                         # leave the app running afterwards
#>
param(
    [string]$AppPath = (Join-Path $PSScriptRoot '../../SafetyOpsApp'),
    [string]$Filter,
    [switch]$KeepApp
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$AppPath = Resolve-Path $AppPath

function Compose([string[]]$arguments) {
    docker compose --project-directory $AppPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "docker compose $arguments failed" }
}

Write-Host "Starting SafetyOpsApp from $AppPath with fresh demo data"
Compose @('down', '-v')
# On Docker Desktop for Windows the first `up` after `down -v` sometimes exits without starting
# the container; a second `up` is harmless when it did start.
Compose @('up', '-d', '--build')
Compose @('up', '-d')

$healthy = $false
foreach ($attempt in 1..60) {
    try { Invoke-WebRequest -Uri 'http://localhost:8080/healthz' -UseBasicParsing -TimeoutSec 2 | Out-Null; $healthy = $true; break }
    catch { Start-Sleep -Seconds 2 }
}
if (-not $healthy) { Compose @('logs', '--no-color'); throw 'SafetyOpsApp did not become healthy' }

try {
    dotnet build (Join-Path $repo 'SafetyOpsTests-Selenium.slnx')
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    $testArgs = @('test', (Join-Path $repo 'SafetyOpsTests-Selenium.slnx'), '--no-build', '--logger', 'console;verbosity=normal')
    if ($Filter) { $testArgs += @('--filter', $Filter) }
    dotnet @testArgs
    $testExit = $LASTEXITCODE
}
finally {
    if (-not $KeepApp) { Compose @('down', '-v') }
}
exit $testExit
