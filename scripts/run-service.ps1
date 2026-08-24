param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('backend', 'legacy-web', 'vben-web')]
    [string]$Service
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$runtimeNode = Join-Path $root 'vben-web\.runtime\node-v22.22.0-win-x64'
$node = Join-Path $runtimeNode 'node.exe'
$vite = Join-Path $root 'vben-web\node_modules\vite\bin\vite.js'
$logDirectory = Join-Path $root '.run-logs'
$runtimeDataDirectory = Join-Path $root '.runtime-data'
$localSettings = Join-Path $runtimeDataDirectory 'local-settings.ps1'

New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $runtimeDataDirectory | Out-Null
$env:Path = "$runtimeNode;$($env:Path)"

switch ($Service) {
    'backend' {
        if (-not (Test-Path $localSettings)) {
            throw "Local settings not found: $localSettings. Copy scripts/local-settings.example.ps1 and fill in the local password."
        }
        . $localSettings
        $env:LOCALAPPDATA = $runtimeDataDirectory
        $env:Logging__EventLog__LogLevel__Default = 'None'
        Set-Location (Join-Path $root 'Admin.NET')
        & dotnet run --no-build --framework net8.0 --project 'Admin.NET.Web.Entry\Admin.NET.Web.Entry.csproj' --launch-profile Admin.NET.Web.Entry `
            1> (Join-Path $logDirectory 'backend.log') `
            2> (Join-Path $logDirectory 'backend.error.log')
    }
    'legacy-web' {
        Set-Location (Join-Path $root 'Web')
        & npm.cmd run dev -- --port 8888 --strictPort `
            1> (Join-Path $logDirectory 'legacy-web.log') `
            2> (Join-Path $logDirectory 'legacy-web.error.log')
    }
    'vben-web' {
        if (-not (Test-Path $node)) { throw "Local Node runtime not found: $node" }
        if (-not (Test-Path $vite)) { throw "Vite is not installed: $vite" }
        Set-Location (Join-Path $root 'vben-web\apps\web-antd')
        & $node $vite --mode development --port 5666 --strictPort `
            1> (Join-Path $logDirectory 'vben-web.log') `
            2> (Join-Path $logDirectory 'vben-web.error.log')
    }
}

if ($LASTEXITCODE -ne 0) {
    throw "$Service exited with code $LASTEXITCODE. Check $logDirectory."
}
