param(
    [switch]$NoWait
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$runtimeNode = Join-Path $root 'vben-web\\.runtime\\node-v22.22.0-win-x64'
$vite = Join-Path $root 'vben-web\node_modules\vite\bin\vite.js'
$logDirectory = Join-Path $root '.run-logs'
$runtimeDataDirectory = Join-Path $root '.runtime-data'
$localSettings = Join-Path $runtimeDataDirectory 'local-settings.ps1'

if (-not (Test-Path $runtimeNode)) { throw "Local Node runtime not found: $runtimeNode" }
if (-not (Test-Path $vite)) { throw "Vite is not installed: $vite" }

New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $runtimeDataDirectory | Out-Null
if (-not (Test-Path $localSettings)) {
    throw "Local settings not found: $localSettings. Copy scripts/local-settings.example.ps1 and fill in the local password."
}
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

Start-ServiceIfNeeded 'backend' 5005 (Join-Path $root 'Admin.NET') ". '$localSettings'; `$env:LOCALAPPDATA='$runtimeDataDirectory'; `$env:Logging__EventLog__LogLevel__Default='None'; dotnet run --no-build --framework net8.0 --project Admin.NET.Web.Entry\\Admin.NET.Web.Entry.csproj --launch-profile Admin.NET.Web.Entry"
Start-ServiceIfNeeded 'legacy-web' 8888 (Join-Path $root 'Web') 'npm.cmd run dev -- --port 8888 --strictPort'
# Invoke the installed Vite entry with Node 22 so pnpm bin lookup cannot fall back
# to the system Node version or miss the workspace-root executable.
Start-ServiceIfNeeded 'vben-web' 5666 (Join-Path $root 'vben-web\apps\web-antd') "& '$runtimeNode\\node.exe' '$vite' --mode development --port 5666 --strictPort"

if (-not $NoWait) {
    $deadline = (Get-Date).AddSeconds(60)
    foreach ($port in @(5005, 8888, 5666)) {
        while ((Get-Date) -lt $deadline -and -not (Test-PortListening $port)) {
            Start-Sleep -Seconds 1
        }
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

    if (-not $running) {
        $serviceLogName = switch ($service.Port) {
            5005 { 'backend.log' }
            8888 { 'legacy-web.log' }
            5666 { 'vben-web.error.log' }
        }
        $serviceLog = Join-Path $logDirectory $serviceLogName
        if (Test-Path $serviceLog) {
            Write-Host "  Last log lines ($serviceLogName):" -ForegroundColor DarkYellow
            Get-Content $serviceLog -Tail 8 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
        }
    }
}
