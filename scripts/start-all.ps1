param(
    [switch]$NoWait
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$runtimeNode = Join-Path $root 'vben-web\\.runtime\\node-v22.22.0-win-x64'
$pnpm = Join-Path $root 'vben-web\\.runtime\\npm-global\\node_modules\\pnpm\\bin\\pnpm.cjs'
$logDirectory = Join-Path $root '.run-logs'

if (-not (Test-Path $runtimeNode)) { throw "Local Node runtime not found: $runtimeNode" }
if (-not (Test-Path $pnpm)) { throw "Local pnpm not found: $pnpm" }

New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$env:Path = "$runtimeNode;$($env:Path)"

function Test-PortListening([int]$Port) {
    return $null -ne (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1)
}

function Start-ServiceIfNeeded([string]$Name, [int]$Port, [string]$WorkingDirectory, [string]$Command) {
    if (Test-PortListening $Port) {
        Write-Host "[RUNNING] $Name  http://localhost:$Port" -ForegroundColor Green
        return
    }

    $log = Join-Path $logDirectory "$Name.log"
    $errorLog = Join-Path $logDirectory "$Name.error.log"
    Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -WorkingDirectory $WorkingDirectory `
        -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $Command) `
        -RedirectStandardOutput $log -RedirectStandardError $errorLog
    Write-Host "[STARTING] $Name  http://localhost:$Port" -ForegroundColor Yellow
}

Start-ServiceIfNeeded 'backend' 5005 (Join-Path $root 'Admin.NET') "dotnet run --no-build --framework net8.0 --project Admin.NET.Web.Entry\\Admin.NET.Web.Entry.csproj --launch-profile Admin.NET.Web.Entry"
Start-ServiceIfNeeded 'legacy-web' 8888 (Join-Path $root 'Web') 'npm.cmd run dev'
# Do not run the root 'dev' script: npm lifecycle command lookup may select an old
# pnpm binary from node_modules. Invoke Vite through the pinned pnpm 11 runtime.
Start-ServiceIfNeeded 'vben-web' 5666 (Join-Path $root 'vben-web') "& '$runtimeNode\\node.exe' '$pnpm' --filter @vben/web-antd exec vite --mode development --port 5666"

if (-not $NoWait) {
    $deadline = (Get-Date).AddSeconds(45)
    while ((Get-Date) -lt $deadline -and -not (Test-PortListening 5666)) {
        Start-Sleep -Seconds 2
    }
}

Write-Host ''
foreach ($service in @(
    @{ Name = 'Backend'; Port = 5005 },
    @{ Name = 'Legacy Web'; Port = 8888 },
    @{ Name = 'Vben Web'; Port = 5666 }
)) {
    $running = Test-PortListening $service.Port
    $color = if ($running) { 'Green' } else { 'Red' }
    $status = if ($running) { 'READY' } else { 'NOT RUNNING - check .run-logs' }
    Write-Host ("[{0}] {1}: http://localhost:{2}" -f $status, $service.Name, $service.Port) -ForegroundColor $color
}
