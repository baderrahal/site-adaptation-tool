# R310. The memory of one window of the test host, which Run-TestHost.ps1 samples every testHost.sampleSeconds of config/project.json.
# Every byte count is a [long] and the peaks are found by comparing [long] values. [Math]::Max with an Int32 first argument binds to
# Max(Int32, Int32) in PowerShell 7 and throws once a value passes 2 GiB, and an empty catch turned that into a peak frozen under
# 2 GiB before (Round ADDIN Phase 1). Byte counts are divided by 1MB and 1GB of PowerShell, which are MiB and GiB, and named so.

# The processes a process started and the ones they started in turn, from one list of every process. A process counts only when it
# started after its parent, since Windows reuses the id of a process that has ended. A helper an earlier window started and this
# one reuses is no descendant of it and is never counted. The call ends with a throw past TimeoutSeconds
function Get-ChildProcesses {
    param([Parameter(Mandatory)][Diagnostics.Process]$Process, [Parameter(Mandatory)][ValidateRange(1, 60)][int]$TimeoutSeconds)
    $all = @(Get-CimInstance -ClassName Win32_Process -Property ProcessId, ParentProcessId, Name, PrivatePageCount, WorkingSetSize, CreationDate -OperationTimeoutSec $TimeoutSeconds -ErrorAction Stop)
    $started = @{}
    foreach ($item in $all) {
        $started[[int]$item.ProcessId] = $item.CreationDate
    }
    $started[$Process.Id] = $Process.StartTime
    $found = [Collections.Generic.List[object]]::new()
    $parents = @($Process.Id)
    while ($parents.Count) {
        $next = @()
        foreach ($item in $all) {
            $parent = [int]$item.ParentProcessId
            $id = [int]$item.ProcessId
            if ($parents -contains $parent -and $id -ne $Process.Id -and $null -ne $item.CreationDate -and $item.CreationDate -ge $started[$parent] -and -not ($found | Where-Object { $_.ProcessId -eq $id })) {
                $found.Add($item)
                $next += $id
            }
        }
        $parents = $next
    }
    $found
}

# One sample of a running process: private memory and working set now and the peak of each that Windows keeps (PeakPagefileUsage,
# which .NET names PeakPagedMemorySize64, and PeakWorkingSetSize), whether it responds, the free physical memory of the machine, and
# the count, names, private memory and working set of the processes it started. Each CIM call has TimeoutSeconds, so a WMI that
# hangs under memory pressure costs a sample at most twice that. A failed or timed out call leaves its own fields null, not read,
# with its reason in freeError or childError and the memory of the process stays. Throws when the process has ended
function Get-WindowSample {
    param([Parameter(Mandatory)][Diagnostics.Process]$Process, [Parameter(Mandatory)][ValidateRange(1, 60)][int]$TimeoutSeconds)
    $Process.Refresh()
    $sample = [ordered]@{
        utc                  = (Get-Date).ToUniversalTime().ToString('o')
        privateBytes         = [long]$Process.PrivateMemorySize64
        peakPrivateBytes     = [long]$Process.PeakPagedMemorySize64
        workingSetBytes      = [long]$Process.WorkingSet64
        peakWorkingSetBytes  = [long]$Process.PeakWorkingSet64
        responding           = [bool]$Process.Responding
        freeBytes            = $null
        childProcesses       = $null
        childNames           = $null
        childPrivateBytes    = $null
        childWorkingSetBytes = $null
    }
    try {
        $os = Get-CimInstance -ClassName Win32_OperatingSystem -Property FreePhysicalMemory -OperationTimeoutSec $TimeoutSeconds -ErrorAction Stop
        $sample.freeBytes = [long]$os.FreePhysicalMemory * 1024
    }
    catch {
        $sample.freeError = $_.Exception.Message
    }
    try {
        $children = @(Get-ChildProcesses -Process $Process -TimeoutSeconds $TimeoutSeconds)
        [long]$private = 0
        [long]$working = 0
        foreach ($child in $children) {
            $private += [long]$child.PrivatePageCount
            $working += [long]$child.WorkingSetSize
        }
        $sample.childProcesses = $children.Count
        $sample.childNames = (@($children | ForEach-Object { [string]$_.Name } | Sort-Object -Unique) -join ', ')
        $sample.childPrivateBytes = $private
        $sample.childWorkingSetBytes = $working
    }
    catch {
        $sample.childError = $_.Exception.Message
    }
    $sample
}

# The helpers of testHost.helperProcesses already running, listed before a window starts its Revit: name, id, start time and
# private memory in MiB. A helper an earlier window started and this one reuses, such as the LocalDB of Steel Connections, is no
# child of this window, so the child figures leave it out and this list shows it. Get-Process asks no CIM, so it cannot hang on WMI
function Get-RunningHelpers {
    param([string[]]$Names)
    $wanted = @($Names | Where-Object { $_ })
    if (-not $wanted.Count) {
        return
    }
    foreach ($item in @(Get-Process -Name $wanted -ErrorAction SilentlyContinue | Sort-Object Name, Id)) {
        $started = $null
        try {
            $started = $item.StartTime.ToUniversalTime().ToString('o')
        }
        catch {
            # A process of another user may hide its start time, its name and memory still count
        }
        [ordered]@{
            name       = $item.Name
            id         = $item.Id
            startUtc   = $started
            privateMib = [long][Math]::Round([long]$item.PrivateMemorySize64 / 1MB)
        }
    }
}

# The peaks Windows keeps for a process over its whole life, read through its handle with K32GetProcessMemoryInfo, which still
# answers after the process has ended while the handle is open. The samples miss what comes after the last look, such as the
# close of Revit. Throws when Windows gives no counters
function Get-ExitPeaks {
    param([Parameter(Mandatory)][Diagnostics.Process]$Process)
    if (-not ('SiteAdaptation.WindowMemory' -as [type])) {
        # PROCESS_MEMORY_COUNTERS of 64 bit Windows as 9 words: cb and PageFaultCount, then PeakWorkingSetSize at 1 and
        # PeakPagefileUsage, the peak private memory, at 8. The C# statement ends in character 59, kept out of this file by R15
        $signature = '[DllImport("kernel32.dll", SetLastError = true)] public static extern bool K32GetProcessMemoryInfo(IntPtr process, [Out] ulong[] counters, uint size)' + [char]59
        Add-Type -Namespace SiteAdaptation -Name WindowMemory -MemberDefinition $signature
    }
    $counters = [ulong[]]::new(9)
    if (-not [SiteAdaptation.WindowMemory]::K32GetProcessMemoryInfo($Process.Handle, $counters, 72)) {
        throw "K32GetProcessMemoryInfo gave no counters, Win32 error $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
    }
    [ordered]@{
        utc                 = (Get-Date).ToUniversalTime().ToString('o')
        peakPrivateBytes    = [long]$counters[8]
        peakWorkingSetBytes = [long]$counters[1]
    }
}

# The peaks of one window in MiB: the true peak of private memory and of working set, the largest of the samples, the peaks Windows
# keeps between them and, when Exit is given, the peaks to the exit of the process, also with no sample, with the largest sampled
# values apart. The processes Revit started have no exit peak, so theirs are the largest sampled values, alone and added to the
# private memory of Revit in the same sample. Then the least free memory in GiB, how many samples had less free memory than the
# floor of R200, how many found Revit not responding and how many lack the free memory or the child processes since a CIM call
# failed. With no sample and no Exit every peak is null
function Get-WindowPeaks {
    param([object[]]$Samples, [double]$FloorGB, [object]$Exit)
    [long]$private = -1
    [long]$working = -1
    [long]$truePrivate = -1
    [long]$trueWorking = -1
    [long]$free = -1
    [long]$childPrivate = -1
    [long]$childWorking = -1
    [long]$totalPrivate = -1
    $childMost = $null
    $under = 0
    $hung = 0
    $count = 0
    $freeMissing = 0
    $childMissing = 0
    foreach ($sample in @($Samples | Where-Object { $null -ne $_ })) {
        $count++
        if ([long]$sample.privateBytes -gt $private) { $private = [long]$sample.privateBytes }
        if ([long]$sample.workingSetBytes -gt $working) { $working = [long]$sample.workingSetBytes }
        foreach ($value in [long]$sample.privateBytes, [long]$sample.peakPrivateBytes) {
            if ($value -gt $truePrivate) { $truePrivate = $value }
        }
        foreach ($value in [long]$sample.workingSetBytes, [long]$sample.peakWorkingSetBytes) {
            if ($value -gt $trueWorking) { $trueWorking = $value }
        }
        if ($null -eq $sample.freeBytes) {
            $freeMissing++
        }
        else {
            if ($free -lt 0 -or [long]$sample.freeBytes -lt $free) { $free = [long]$sample.freeBytes }
            if ([long]$sample.freeBytes -lt $FloorGB * 1GB) { $under++ }
        }
        if ($null -eq $sample.childPrivateBytes) {
            $childMissing++
        }
        else {
            if ([long]$sample.childPrivateBytes -gt $childPrivate) { $childPrivate = [long]$sample.childPrivateBytes }
            if ([long]$sample.childWorkingSetBytes -gt $childWorking) { $childWorking = [long]$sample.childWorkingSetBytes }
            [long]$total = [long]$sample.privateBytes + [long]$sample.childPrivateBytes
            if ($total -gt $totalPrivate) { $totalPrivate = $total }
            if ($null -eq $childMost -or [int]$sample.childProcesses -gt $childMost) { $childMost = [int]$sample.childProcesses }
        }
        if (-not $sample.responding) { $hung++ }
    }
    if ($null -ne $Exit) {
        if ([long]$Exit.peakPrivateBytes -gt $truePrivate) { $truePrivate = [long]$Exit.peakPrivateBytes }
        if ([long]$Exit.peakWorkingSetBytes -gt $trueWorking) { $trueWorking = [long]$Exit.peakWorkingSetBytes }
    }
    $any = $count -or $null -ne $Exit
    $children = $count -gt $childMissing
    [ordered]@{
        samples                   = $count
        peakPrivateMib            = $(if ($any) { [long][Math]::Round($truePrivate / 1MB) } else { $null })
        peakWorkingSetMib         = $(if ($any) { [long][Math]::Round($trueWorking / 1MB) } else { $null })
        sampledPrivateMib         = $(if ($count) { [long][Math]::Round($private / 1MB) } else { $null })
        sampledWorkingSetMib      = $(if ($count) { [long][Math]::Round($working / 1MB) } else { $null })
        childProcessesMax         = $childMost
        sampledChildPrivateMib    = $(if ($children) { [long][Math]::Round($childPrivate / 1MB) } else { $null })
        sampledChildWorkingSetMib = $(if ($children) { [long][Math]::Round($childWorking / 1MB) } else { $null })
        sampledTotalPrivateMib    = $(if ($children) { [long][Math]::Round($totalPrivate / 1MB) } else { $null })
        minFreeGib                = $(if ($free -ge 0) { [Math]::Round($free / 1GB, 2) } else { $null })
        floorGib                  = $FloorGB
        samplesUnderFloor         = $under
        notResponding             = $hung
        freeMissing               = $freeMissing
        childMissing              = $childMissing
        throughExit               = [bool]($null -ne $Exit)
    }
}

# The two clocks of the wait loop of Run-TestHost.ps1, each on its own: the checks of the run (deadline, watcher, refusal, log and
# start screen) every CheckSeconds, fixed in the script, and a memory sample every SampleSeconds of the config. New-LookClock starts
# both at Start, Get-LookWait gives the milliseconds to the next look that is due, and Get-DueLooks says which looks are due at Now
# and sets each due one to its next time from Now
function New-LookClock {
    param([datetime]$Start, [int]$CheckSeconds, [int]$SampleSeconds)
    [pscustomobject]@{
        checkSeconds  = $CheckSeconds
        sampleSeconds = $SampleSeconds
        nextCheck     = $Start.AddSeconds($CheckSeconds)
        nextSample    = $Start.AddSeconds($SampleSeconds)
    }
}

function Get-LookWait {
    param([Parameter(Mandatory)][object]$Clock, [datetime]$Now)
    $next = if ($Clock.nextCheck -lt $Clock.nextSample) { $Clock.nextCheck } else { $Clock.nextSample }
    [int][Math]::Max(0, [Math]::Ceiling(($next - $Now).TotalMilliseconds))
}

function Get-DueLooks {
    param([Parameter(Mandatory)][object]$Clock, [datetime]$Now)
    $check = $Now -ge $Clock.nextCheck
    $sample = $Now -ge $Clock.nextSample
    if ($check) { $Clock.nextCheck = $Now.AddSeconds($Clock.checkSeconds) }
    if ($sample) { $Clock.nextSample = $Now.AddSeconds($Clock.sampleSeconds) }
    [pscustomobject]@{
        check  = $check
        sample = $sample
    }
}
