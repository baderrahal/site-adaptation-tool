# Lane B 5b. The start of the dialog watcher before Revit (R108) and the freeze guard of one Revit window (R104). Dot-sourced by
# Run-TestHost.ps1 and by the tests. In 5b a probe started Revit outside the test host and a security box stayed open with nothing
# to answer it, so every start goes through here and a window that stops moving is ended with its reason

# R108. Starts the watcher and waits for its first report, which it writes before it waits for the pid of Revit. Revit starts only
# after this returns, so no security box can open before the watcher looks
function Start-WatcherFirst([string]$WatcherExe, [string[]]$Arguments, [string]$ReadyPath, [int]$TimeoutSeconds = 60) {
    if (Test-Path -LiteralPath $ReadyPath) {
        throw "The watcher report $ReadyPath is there already, from an earlier run"
    }
    $watcher = Start-Process -FilePath $WatcherExe -ArgumentList ($Arguments | ForEach-Object { '"' + $_ + '"' }) -PassThru -WindowStyle Hidden
    $until = (Get-Date).AddSeconds($TimeoutSeconds)
    while (-not (Test-Path -LiteralPath $ReadyPath)) {
        if ($watcher.HasExited) {
            throw "the dialog watcher ended before it was ready, exit code $($watcher.ExitCode)"
        }
        if ((Get-Date) -gt $until) {
            Stop-Process -Id $watcher.Id -Force
            throw "the dialog watcher wrote no report in $TimeoutSeconds s"
        }
        Start-Sleep -Milliseconds 200
    }
    $watcher
}

# The pid goes in whole or not at all, the watcher reads a part written file as no pid
function Set-WatchedPid([string]$Path, [int]$ProcessId) {
    Set-Content -LiteralPath ($Path + '.next') -Value $ProcessId -Encoding ascii
    Move-Item -LiteralPath ($Path + '.next') -Destination $Path -Force
}

# R104. The first step of the test host log and its last heartbeat, each by the write time of its file, and only when written
# after the start of this window
function Read-FreezeClock([string]$LogPath, [string]$BeatPath, [datetime]$Start) {
    $firstStepAt = $null
    if ($LogPath -and (Test-Path -LiteralPath $LogPath)) {
        $item = Get-Item -LiteralPath $LogPath
        if ($item.LastWriteTime -gt $Start) {
            try {
                if (@((Get-Content -Raw -LiteralPath $LogPath | ConvertFrom-Json).steps).Count -gt 0) {
                    $firstStepAt = $item.LastWriteTime
                }
            }
            catch {
                # The test host may be writing the log right now, the next look reads it
            }
        }
    }
    $lastBeatAt = $null
    if ($BeatPath -and (Test-Path -LiteralPath $BeatPath)) {
        $item = Get-Item -LiteralPath $BeatPath
        if ($item.LastWriteTime -gt $Start) {
            $lastBeatAt = $item.LastWriteTime
        }
    }
    [pscustomobject]@{
        firstStepAt = $firstStepAt
        lastBeatAt  = $lastBeatAt
    }
}

# R104. The first limit a window crossed, or null. A window with no job writes no step and no heartbeat, so only the answer of
# Revit counts for it
function Get-FreezeReason {
    param(
        [datetime]$Start,
        [datetime]$Now,
        $FirstStepAt,
        $LastBeatAt,
        $NotRespondingSince,
        [bool]$HasJob,
        [double]$FirstStepMinutes = 5,
        [double]$HeartbeatMinutes = 10,
        [double]$NotRespondingMinutes = 5
    )
    if ($HasJob) {
        if ($null -eq $FirstStepAt -and ($Now - $Start).TotalMinutes -gt $FirstStepMinutes) {
            return "no first log step within $FirstStepMinutes minutes of the Revit start"
        }
        $beat = if ($null -ne $LastBeatAt) { [datetime]$LastBeatAt } else { $Start }
        if (($Now - $beat).TotalMinutes -gt $HeartbeatMinutes) {
            return "no heartbeat for $HeartbeatMinutes minutes"
        }
    }
    if ($null -ne $NotRespondingSince -and ($Now - [datetime]$NotRespondingSince).TotalMinutes -gt $NotRespondingMinutes) {
        return "Revit not responding for $NotRespondingMinutes minutes"
    }
    $null
}

if (-not ('SiteAdaptation.FreezeWindows' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SiteAdaptation
{
    public static class FreezeWindows
    {
        private delegate bool EnumProc(IntPtr window, IntPtr data);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumProc callback, IntPtr data);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder text, int length);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        // Each top level window of the process as handle, class, title and whether it shows
        public static List<string[]> Of(int processId)
        {
            var found = new List<string[]>();
            EnumWindows((window, data) =>
            {
                uint owner;
                GetWindowThreadProcessId(window, out owner);
                if (owner == (uint)processId)
                {
                    var title = new StringBuilder(512);
                    var name = new StringBuilder(256);
                    GetWindowText(window, title, title.Capacity);
                    GetClassName(window, name, name.Capacity);
                    found.Add(new[] { window.ToInt64().ToString(), name.ToString(), title.ToString(), IsWindowVisible(window) ? "1" : "0" });
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }
    }
}
'@
}

function Get-ProcessWindows([int]$ProcessId) {
    foreach ($window in [SiteAdaptation.FreezeWindows]::Of($ProcessId)) {
        [pscustomobject]@{
            handle  = [long]$window[0]
            class   = $window[1]
            title   = $window[2]
            visible = $window[3] -eq '1'
        }
    }
}

# The last lines of the newest journal written since the start. Revit holds the journal open, so it is read with sharing
function Get-JournalTail([string]$Folder, [datetime]$Start, [int]$Lines = 40) {
    $journal = Get-ChildItem -LiteralPath $Folder -Filter 'journal.*.txt' -File -ErrorAction SilentlyContinue |
        Where-Object LastWriteTime -ge $Start | Sort-Object LastWriteTime | Select-Object -Last 1
    if (-not $journal) {
        return [pscustomobject]@{
            journal = $null
            tail    = @()
        }
    }
    $stream = [IO.File]::Open($journal.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try {
        $all = ([IO.StreamReader]::new($stream)).ReadToEnd() -split "`r?`n"
    }
    finally {
        $stream.Dispose()
    }
    if ($all.Count -gt 0 -and $all[-1] -eq '') {
        $all = $all[0..($all.Count - 2)]
    }
    [pscustomobject]@{
        journal = $journal.FullName
        tail    = @($all | Select-Object -Last $Lines)
    }
}

# R104. Logs the windows and the journal tail, ends this process only and fails the open step of the log with the reason. The
# windows are read before the end, the log is written after it, so the test host inside Revit cannot write over it
function Stop-FrozenRevit([Diagnostics.Process]$Process, [string]$Reason, [string]$LogPath, [string]$JournalFolder, [datetime]$Start, [int]$TailLines = 40) {
    $windows = @(Get-ProcessWindows -ProcessId $Process.Id)
    $journal = Get-JournalTail -Folder $JournalFolder -Start $Start -Lines $TailLines
    if (-not $Process.HasExited) {
        Stop-Process -Id $Process.Id -Force
        $Process.WaitForExit(60000) | Out-Null
    }
    $freeze = [pscustomobject]@{
        utc         = (Get-Date).ToUniversalTime().ToString('o')
        reason      = $Reason
        pid         = $Process.Id
        windows     = $windows
        journal     = $journal.journal
        journalTail = @($journal.tail)
    }
    $log = if ($LogPath -and (Test-Path -LiteralPath $LogPath)) {
        Get-Content -Raw -LiteralPath $LogPath | ConvertFrom-Json
    }
    else {
        [pscustomobject]@{
            tool  = 'Run-TestHost.ps1'
            steps = @()
        }
    }
    $open = @($log.steps | Where-Object { -not $_.endedUtc }) | Select-Object -First 1
    if ($open) {
        $open | Add-Member -Force -NotePropertyName result -NotePropertyValue 'failed'
        $open | Add-Member -Force -NotePropertyName detail -NotePropertyValue ('freeze guard: ' + $Reason)
        $open | Add-Member -Force -NotePropertyName endedUtc -NotePropertyValue $freeze.utc
    }
    $log | Add-Member -Force -NotePropertyName result -NotePropertyValue 'failed'
    $log | Add-Member -Force -NotePropertyName freeze -NotePropertyValue $freeze
    if ($LogPath) {
        $log | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $LogPath -Encoding utf8
    }
    [pscustomobject]@{
        reason      = $Reason
        pid         = $Process.Id
        step        = $(if ($open) { $open.name } else { $null })
        windows     = $windows
        journal     = $journal.journal
        journalTail = @($journal.tail)
    }
}
