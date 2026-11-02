# R448. The job folder of the Revit listener as the scripts see it, dot-sourced by Start-RevitListener.ps1, Send-RevitJob.ps1 and the tests.
# RevitListenerQueue of Common is the other side of these files. The token of a listener goes into a file of its out folder, as a run
# job holds its token in its job file. It only says a job was meant for this Revit session, it keeps nobody out

# The slot python/window_lock.py gives the command it runs, so a listener never opens a window around the lock
function Get-ListenerSlot {
    $slot = $env:SITE_ADAPTATION_WINDOW_SLOT
    if (-not $slot) {
        throw 'SITE_ADAPTATION_WINDOW_SLOT is not set, start the listener under python\window_lock.py'
    }
    $slot
}

function Get-ListenerFolders([string]$Root, [string]$Slot) {
    if ($Slot -notmatch '^[1-9][0-9]?$') {
        throw "The slot of the listener is not a number from 1 to 99: $Slot"
    }
    if (-not $Root) {
        throw 'config/project.json has no testHost.listener.root'
    }
    $in = Join-Path $Root ('in-' + $Slot)
    $out = Join-Path $Root ('out-' + $Slot)
    [pscustomobject]@{
        root    = $Root
        slot    = $Slot
        in      = $in
        out     = $out
        claimed = Join-Path $in 'claimed'
        beat    = Join-Path $out 'heartbeat.json'
        ready   = Join-Path $out 'ready.json'
        session = Join-Path $out 'session.json'
        refused = Join-Path $Root ('listener-' + $Slot + '.refused.json')
    }
}

# One move makes the whole file appear, so a reader never sees half of it
function Write-WholeFile([string]$Path, [string]$Text) {
    $writing = $Path + '.writing'
    Set-Content -LiteralPath $writing -Value $Text -Encoding utf8
    Move-Item -LiteralPath $writing -Destination $Path
}

# The session of the Revit on this slot, or why a job cannot go to it
function Read-ListenerSession($Folders) {
    if (-not (Test-Path -LiteralPath $Folders.session) -or -not (Test-Path -LiteralPath $Folders.ready)) {
        throw "No listener is ready on slot $($Folders.slot), start it first (steps/revit-listener.md)"
    }
    $session = Get-Content -Raw -LiteralPath $Folders.session | ConvertFrom-Json
    $ready = Get-Content -Raw -LiteralPath $Folders.ready | ConvertFrom-Json
    if ($ready.pid -ne $session.pid) {
        throw "The listener of slot $($Folders.slot) is process $($ready.pid) and its session names $($session.pid), start it again"
    }
    $process = Get-Process -Id $session.pid -ErrorAction SilentlyContinue
    if (-not $process -or $process.ProcessName -ne 'Revit') {
        throw "The Revit of slot $($Folders.slot), process $($session.pid), is gone, start the listener again"
    }
    $session
}

function Write-ListenerJob($Folders, $Job) {
    $name = [string]$Job.runId + '.job.json'
    foreach ($used in @((Join-Path $Folders.in $name), (Join-Path $Folders.claimed $name), (Join-Path $Folders.out ([string]$Job.runId + '.result.json')))) {
        if (Test-Path -LiteralPath $used) {
            throw "The run id $($Job.runId) was sent before: $used"
        }
    }
    Write-WholeFile -Path (Join-Path $Folders.in $name) -Text ($Job | ConvertTo-Json -Depth 5)
}

# A result in the name of a Revit that can no longer write one, never over one it wrote
function Write-ListenerFailure($Folders, [string]$RunId, [string]$Reason) {
    $path = Join-Path $Folders.out ($RunId + '.result.json')
    if (-not (Test-Path -LiteralPath $path)) {
        $failure = [ordered]@{
            runId       = $RunId
            result      = 'failed'
            error       = $Reason
            finishedUtc = (Get-Date).ToUniversalTime().ToString('o')
        }
        Write-WholeFile -Path $path -Text ($failure | ConvertTo-Json)
    }
}

# R104. Waits for the result, with the freeze guard of a run on the beat and the answer of that Revit. The job may wait behind another
# job, so no first log step is asked for. State result, timeout, gone or frozen, nothing is stopped here
function Wait-ListenerResult {
    param(
        $Folders,
        [string]$RunId,
        [int]$ProcessId,
        [int]$TimeoutSeconds,
        [datetime]$Start,
        [double]$HeartbeatMinutes = 10,
        [double]$NotRespondingMinutes = 5,
        [double]$LookSeconds = 1
    )
    $path = Join-Path $Folders.out ($RunId + '.result.json')
    $notRespondingSince = $null
    $until = (Get-Date).AddSeconds($TimeoutSeconds)
    while ($true) {
        if (Test-Path -LiteralPath $path) {
            return [pscustomobject]@{
                state  = 'result'
                result = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
                reason = $null
            }
        }
        $now = Get-Date
        $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        $state = $null
        $reason = $null
        if (-not $process) {
            $state = 'gone'
            $reason = "the Revit of slot $($Folders.slot), process $ProcessId, is gone and left no result"
        }
        else {
            if ($process.Responding) {
                $notRespondingSince = $null
            }
            elseif (-not $notRespondingSince) {
                $notRespondingSince = $now
            }
            $clock = Read-FreezeClock -LogPath $null -BeatPath $Folders.beat -Start $Start
            $reason = Get-FreezeReason -Start $Start -Now $now -FirstStepAt $Start -LastBeatAt $clock.lastBeatAt -NotRespondingSince $notRespondingSince -HasJob $true -HeartbeatMinutes $HeartbeatMinutes -NotRespondingMinutes $NotRespondingMinutes
            if ($reason) {
                $state = 'frozen'
            }
            elseif ($now -gt $until) {
                $state = 'timeout'
                $reason = "no result in $TimeoutSeconds s, the job may still run or wait"
            }
        }
        if ($state) {
            return [pscustomobject]@{
                state  = $state
                result = $null
                reason = $reason
            }
        }
        Start-Sleep -Seconds $LookSeconds
    }
}
