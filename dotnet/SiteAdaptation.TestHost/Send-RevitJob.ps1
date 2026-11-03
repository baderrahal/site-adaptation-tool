# R448, developers only. Sends one buttons job to the Revit listener of a slot and waits for its result. The spec is a -RoundSpec of
# Run-TestHost.ps1 of kind addinRun with buttons, which makes the run folder, the config copy and the model copy as for a run of its own,
# only no Revit starts. One job at a time in the order they arrive, the job may wait behind another. It takes no window lock, the
# Start-RevitListener.ps1 of the slot holds it. Exit code 0 ok, 1 failed or refused, 2 no result in the time, 3 Revit gone, 4 Revit frozen, 5 ran with findings.
param(
    [Parameter(Mandatory)]
    [string]$RoundSpec,
    # The slot of python\window_lock.py when this runs under it, else named here
    [string]$Slot,
    [int]$TimeoutMinutes = 60,
    [int]$HeartbeatMinutes = 10,
    [int]$NotRespondingMinutes = 5
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TestHostPaths.ps1')
. (Join-Path $PSScriptRoot 'FreezeGuard.ps1')
. (Join-Path $PSScriptRoot 'ListenerPaths.ps1')

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$config = Get-Content -Raw (Join-Path $repo 'config\project.json') | ConvertFrom-Json
$folders = Get-ListenerFolders -Root ([string]$config.testHost.listener.root) -Slot $(if ($Slot) { $Slot } else { Get-ListenerSlot })
$session = Read-ListenerSession -Folders $folders

$prepared = Join-Path $folders.out ('prepared_' + [guid]::NewGuid().ToString('N') + '.json')
& (Join-Path $PSScriptRoot 'Run-TestHost.ps1') -RoundSpec $RoundSpec -ListenerJobOut $prepared | Out-Host
$job = Get-Content -Raw -LiteralPath $prepared | ConvertFrom-Json -AsHashtable
Remove-Item -LiteralPath $prepared
$job.runId = Split-Path (Split-Path $job.model) -Leaf
$inputSources = $job.inputSources
$job.Remove('inputSources')
$job.token = $session.token
$job.createdUtc = (Get-Date).ToUniversalTime().ToString('o')

$start = Get-Date
Write-ListenerJob -Folders $folders -Job $job
"Job $($job.runId) sent to slot $($folders.slot), Revit $($session.pid), buttons $($job.buttons -join ' ')"
$wait = Wait-ListenerResult -Folders $folders -RunId $job.runId -ProcessId $session.pid -TimeoutSeconds ($TimeoutMinutes * 60) -Start $start -HeartbeatMinutes $HeartbeatMinutes -NotRespondingMinutes $NotRespondingMinutes
$log = Join-Path $folders.out ($job.runId + '.log.json')
switch ($wait.state) {
    'result' {
        $result = $wait.result
        Get-ListenerResultLines -Result $result
        "Result file: $(Join-Path $folders.out ($job.runId + '.result.json')), log: $log, run folder: $($result.runFolder)"
        $newInputs = @(Get-NewInputFiles -Sources $inputSources -Before $job.inputs -InputRoots (Get-RunInputRoots -Config $config))
        if ($newInputs.Count -gt 0) {
            "A real input of the job changed (D19), new under an input root: $(($newInputs | ForEach-Object { Split-Path $_ -Leaf }) -join ', ')"
            exit 1
        }
        exit (Get-ListenerExitCode -Result $result)
    }
    'timeout' {
        "$($job.runId): $($wait.reason). Its result will still be written to $($folders.out)"
        exit 2
    }
    'gone' {
        Write-ListenerFailure -Folders $folders -RunId $job.runId -Reason $wait.reason
        "$($job.runId): $($wait.reason). Start the listener again and send the job again"
        exit 3
    }
    default {
        # R104. This Revit only. Its windows and the journal tail go into the log of the job, as in a run of its own
        $journals = Join-Path $env:LOCALAPPDATA 'Autodesk\Revit\Autodesk Revit 2024\Journals'
        $frozen = Stop-FrozenRevit -Process (Get-Process -Id $session.pid) -Reason $wait.reason -LogPath $log -JournalFolder $journals -JournalPath $session.journal -Start $start
        Write-ListenerFailure -Folders $folders -RunId $job.runId -Reason ('freeze guard: ' + $wait.reason)
        "Freeze guard: $($wait.reason). Revit $($frozen.pid) ended, open step: $($frozen.step). Start the listener again and send the job again"
        exit 4
    }
}
