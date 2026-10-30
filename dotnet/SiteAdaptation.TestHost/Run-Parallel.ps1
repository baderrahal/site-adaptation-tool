# Runs several test host jobs of R110 side by side, each in its own Revit window through Run-TestHost.ps1.
# It reads the total and free memory first, starts a new window only while free memory stays above -MinFreeGB,
# never runs more than -MaxWindows at once and starts them in the order given.
# A window that hangs past its limit is stopped by its own Run-TestHost.ps1. When free memory falls under -StopBelowGB
# the largest Revit of this run is stopped, that one only. Nothing in the registry is read or written.
param(
    [Parameter(Mandatory = $true)]
    [string[]]$Codes,
    [switch]$Upgrade,
    [int]$MaxWindows = 3,
    [double]$MinFreeGB = 8,
    [double]$StopBelowGB = 1,
    [int]$StartGapSeconds = 45,
    [int]$TimeoutMinutes = 30
)

$ErrorActionPreference = 'Stop'
if ($MaxWindows -lt 1 -or $MaxWindows -gt 3) {
    throw 'MaxWindows is 1 to 3.'
}
$run = Join-Path $PSScriptRoot 'Run-TestHost.ps1'
# J8, Q127. The work root of machine.json, as Run-TestHost.ps1 takes it
. (Join-Path $PSScriptRoot 'TestHostPaths.ps1')
$docs = Get-WorkRoot -AppData $env:APPDATA -MyDocuments ([Environment]::GetFolderPath('MyDocuments'))
$shell = (Get-Process -Id $PID).Path

function Get-FreeGB {
    (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB
}

$os = Get-CimInstance Win32_OperatingSystem
"Memory: total {0:0.0} GB, free {1:0.0} GB. A window starts only while free memory is above {2} GB, at most {3} at once." -f ($os.TotalVisibleMemorySize / 1MB), ($os.FreePhysicalMemory / 1MB), $MinFreeGB, $MaxWindows

$queue = [System.Collections.Generic.Queue[string]]::new([string[]]$Codes)
$running = @{}
$lastStart = [datetime]::MinValue
$stopped = @()
while ($queue.Count -gt 0 -or $running.Count -gt 0) {
    foreach ($code in @($running.Keys)) {
        if ($running[$code].HasExited) {
            "{0} ended at {1}" -f $code, (Get-Date -Format HH:mm:ss)
            $running.Remove($code)
        }
    }
    $free = Get-FreeGB
    if ($running.Count -gt 0 -and $free -lt $StopBelowGB) {
        # The Revit windows of this run are the children of the shells this script started
        $shells = @($running.Values | ForEach-Object { $_.Id })
        $revits = @(Get-CimInstance Win32_Process -Filter "Name='Revit.exe'" | Where-Object { $shells -contains $_.ParentProcessId })
        if ($revits.Count -gt 0) {
            $largest = $revits | Sort-Object WorkingSetSize -Descending | Select-Object -First 1
            "Free memory {0:0.0} GB is under {1} GB, stopping the largest Revit {2} ({3:0} MB), that one only" -f $free, $StopBelowGB, $largest.ProcessId, ($largest.WorkingSetSize / 1MB)
            Stop-Process -Id $largest.ProcessId -Force
            $stopped += $largest.ProcessId
        }
    }
    $gapPassed = ((Get-Date) - $lastStart).TotalSeconds -ge $StartGapSeconds
    if ($queue.Count -gt 0 -and $running.Count -lt $MaxWindows -and $gapPassed) {
        if ($free -gt $MinFreeGB) {
            $code = $queue.Dequeue()
            $name = if ($Upgrade) { $code + '_upgrade' } else { $code }
            $console = Join-Path $docs ('testhost_' + $name + '_console.txt')
            $arguments = @('-NoProfile', '-File', $run, '-Code', $code, '-TimeoutMinutes', $TimeoutMinutes)
            if ($Upgrade) {
                $arguments += '-Upgrade'
            }
            $running[$code] = Start-Process -FilePath $shell -ArgumentList $arguments -PassThru -WindowStyle Hidden -RedirectStandardOutput $console -RedirectStandardError ($console + '.err')
            $lastStart = Get-Date
            "{0} started at {1} with free memory {2:0.0} GB, windows now {3}" -f $code, (Get-Date -Format HH:mm:ss), $free, $running.Count
        }
        elseif ($running.Count -eq 0) {
            "Free memory {0:0.0} GB is not above {1} GB and no window runs, nothing more is started. Left: {2}" -f $free, $MinFreeGB, ($queue -join ', ')
            break
        }
    }
    Start-Sleep -Seconds 5
}

'Per job:'
$rows = foreach ($code in $Codes) {
    $name = if ($Upgrade) { $code + '_upgrade' } else { $code }
    $file = Join-Path $docs ('testhost_' + $name + '_run.json')
    if (Test-Path $file) {
        $r = Get-Content -Raw $file | ConvertFrom-Json
        [pscustomobject]@{
            job     = $name
            pid     = $r.pid
            start   = ([datetime]$r.startUtc).ToLocalTime().ToString('HH:mm:ss')
            end     = ([datetime]$r.endUtc).ToLocalTime().ToString('HH:mm:ss')
            minutes = $r.minutes
            peakMb  = $r.peakMb
            result  = $(if ($stopped -contains $r.pid) { 'stopped for memory' } elseif ($r.stopReason) { 'stopped: ' + $r.stopReason } else { $r.logResult })
        }
    }
    else {
        [pscustomobject]@{
            job    = $name
            result = 'not run'
        }
    }
}
$rows | Format-Table -AutoSize | Out-String -Width 250
