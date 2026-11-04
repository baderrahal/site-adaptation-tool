# R448, developers only. Starts Revit 2024 with the listener of the test host on one slot and stays until that Revit ends, so the window
# lock it runs under holds its slot for as long as Revit is open. The slot comes from python\window_lock.py, the folder root from
# testHost.listener of config/project.json. The setting goes to this Revit alone, a Revit started any other way has no listener.
# Run it under the lock, the how-to is steps/revit-listener.md. Nothing in the registry is read or written.
param(
    [int]$ReadyMinutes = 10,
    # What the watcher of R108 looks for, a security box at the start of Revit. As long as Run-TestHost.ps1 gives it
    [int]$WatchMinutes = 10
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TestHostPaths.ps1')
. (Join-Path $PSScriptRoot 'FreezeGuard.ps1')
. (Join-Path $PSScriptRoot 'ListenerPaths.ps1')

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$config = Get-Content -Raw (Join-Path $repo 'config\project.json') | ConvertFrom-Json
$listener = $config.testHost.listener
$folders = Get-ListenerFolders -Root ([string]$listener.root) -Slot (Get-ListenerSlot)
# D19. The job folder never lies in an input root, and no model or config of a job may either
$inputRoots = @(@([string]$config.inputRoot) + @($listener.inputRoots | ForEach-Object { [string]$_ }) | Where-Object { $_ } | Sort-Object -Unique)
Assert-OutsideInputRoots -Folder $folders.root -InputRoots $inputRoots

if (Test-Path -LiteralPath $folders.session) {
    $earlier = Get-Content -Raw -LiteralPath $folders.session | ConvertFrom-Json
    if (Get-Process -Id $earlier.pid -ErrorAction SilentlyContinue) {
        throw "Slot $($folders.slot) has a listener already, process $($earlier.pid)"
    }
}

$addins = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024'
$watcherExe = Join-Path $addins 'SiteAdaptationTestHost\Watcher\SiteAdaptation.DialogWatcher.exe'
if (-not (Test-Path $watcherExe)) {
    throw "The dialog watcher is not installed at $watcherExe. Run Install-TestHost.ps1 first."
}
$revit = Get-Revit2024Exe
$stampFolder = Join-Path (Get-WorkRoot -AppData $env:APPDATA -MyDocuments ([Environment]::GetFolderPath('MyDocuments'))) 'dev-manifest-stamps'
$tool = Get-ToolAddIns -Repo $repo -Addins $addins -StampFolder $stampFolder
# SETUP-sign. The developer manifests are written for as long as this listener lives. Any error from here on takes them away before it
# leaves the script, and the finally below does it when the listener ends, unless a run holds a stamp of its own
trap {
    # A trap covers the whole script, the lines above it too, where the stamp folder is not set and nothing was written, so the real error comes through
    if ($stampFolder) {
        [void](Remove-DevManifestsWhenFree $addins $stampFolder $PID)
    }
    break
}

New-Item -ItemType Directory -Force $folders.claimed, $folders.out | Out-Null
$watchPath = Join-Path $folders.out 'watcher.json'
$pidPath = Join-Path $folders.out 'watcher_pid.json'
# What a listener that ended left, a job file waiting in the in folder stays and its token is refused with a result
foreach ($left in @($folders.session, $folders.ready, $folders.refused, $watchPath, $pidPath)) {
    Remove-Item -LiteralPath $left -ErrorAction SilentlyContinue
}

$token = [guid]::NewGuid().ToString('N')
$info = [Diagnostics.ProcessStartInfo]::new($revit)
$info.UseShellExecute = $false
$info.WorkingDirectory = Split-Path $revit
$info.Environment['SITEADAPTATION_TESTHOST_TOKEN'] = $token
$info.Environment['SITEADAPT_LISTENER_ROOT'] = $folders.root
$info.Environment['SITEADAPT_LISTENER_SLOT'] = $folders.slot
$info.Environment['SITEADAPT_LISTENER_INPUT_ROOTS'] = $inputRoots -join '|'
# L5. The floor of R200, looked at before each job, since this script takes the lock once for many jobs
$info.Environment['SITEADAPT_LISTENER_MIN_FREE_GB'] = ([double]$config.siteBuild.minFreeGB).ToString([Globalization.CultureInfo]::InvariantCulture)
# A job file names one job of a Revit of its own, this one takes its jobs from the folder
$info.Environment.Remove('SITEADAPT_JOB') | Out-Null

$process = $null
$watcher = $null
$readyAt = $null
try {
    # R108. The watcher is up before Revit, so no security box opens with nothing to answer it. The listener log holds no step, so it
    # looks for its whole time
    $listenerLog = Join-Path $folders.out 'listener_log.json'
    $watchArgs = @('--pid-file', $pidPath, '--out', $watchPath, '--stop-log', $listenerLog, '--minutes', [string]$WatchMinutes, '--ours', ($tool.ids -join ','), '--marks', ($tool.marks -join '|'), '--our-paths', ($tool.paths -join '|'))
    $watcher = Start-WatcherFirst -WatcherExe $watcherExe -Arguments $watchArgs -ReadyPath $watchPath
    $start = Get-Date
    $process = [Diagnostics.Process]::Start($info)
    Set-WatchedPid -Path $pidPath -ProcessId $process.Id
    $session = [ordered]@{
        pid      = $process.Id
        token    = $token
        slot     = $folders.slot
        startUtc = $start.ToUniversalTime().ToString('o')
    }
    Write-WholeFile -Path $folders.session -Text ($session | ConvertTo-Json)
    "Revit $($process.Id) started on slot $($folders.slot), the listener is waking"

    $until = $start.AddMinutes($ReadyMinutes)
    while (-not (Test-Path -LiteralPath $folders.ready)) {
        if ($process.HasExited) {
            throw "Revit ended before the listener was ready, exit code $($process.ExitCode)"
        }
        if (Test-Path -LiteralPath $folders.refused) {
            throw 'The listener did not start: ' + (Get-Content -Raw -LiteralPath $folders.refused | ConvertFrom-Json).reason
        }
        if ((Get-Date) -gt $until) {
            throw "The listener was not ready in $ReadyMinutes minutes"
        }
        Start-Sleep -Seconds 1
    }
    $readyAt = Get-Date
    "Listener ready on slot $($folders.slot), Revit $($process.Id), jobs in $($folders.in), results in $($folders.out)"
    # The lock of this slot is held by this script, so it stays as long as Revit does
    while (-not $process.WaitForExit(5000)) {
    }
    "Revit $($process.Id) exited, code $($process.ExitCode), after {0:0.0} minutes" -f ((Get-Date) - $start).TotalMinutes
}
finally {
    # Only a Revit that never became a listener is stopped here. A ready one is closed by hand or by the freeze guard of a send
    if ($process -and -not $readyAt -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit(60000) | Out-Null
    }
    if ($watcher -and -not $watcher.HasExited) {
        $watcher.WaitForExit(30000) | Out-Null
        if (-not $watcher.HasExited) {
            Stop-Process -Id $watcher.Id -Force
        }
    }
    if ($process) {
        Remove-ListenerFilesOf -Folders $folders -ProcessId $process.Id
    }
    "Developer add-in manifests after the listener: $(Remove-DevManifestsWhenFree $addins $stampFolder $PID)"
}
