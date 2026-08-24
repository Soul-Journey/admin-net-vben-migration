param()

$ErrorActionPreference = 'Stop'
$ports = @(5005, 8888, 5666)

$listeners = foreach ($port in $ports) {
    Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
}

$processIds = @($listeners | Select-Object -ExpandProperty OwningProcess -Unique)

if ($processIds.Count -eq 0) {
    Write-Host '[STOPPED] No Admin.NET services are listening.' -ForegroundColor Green
    exit 0
}

foreach ($processId in $processIds) {
    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        continue
    }

    $processPorts = @(
        $listeners |
            Where-Object OwningProcess -eq $processId |
            Select-Object -ExpandProperty LocalPort -Unique |
            Sort-Object
    )
    Write-Host ("[STOPPING] {0} (PID {1}) ports: {2}" -f $process.ProcessName, $processId, ($processPorts -join ', ')) -ForegroundColor Yellow
    Stop-Process -Id $processId -Force
}

Start-Sleep -Milliseconds 500

$remainingPorts = @(
    foreach ($port in $ports) {
        if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
            $port
        }
    }
)

if ($remainingPorts.Count -gt 0) {
    throw "Failed to stop services on ports: $($remainingPorts -join ', ')"
}

Write-Host '[STOPPED] Backend, legacy Web, and Vben Web have been stopped.' -ForegroundColor Green
