# R310. The memory of one window of the test host, which Run-TestHost.ps1 samples every testHost.sampleSeconds of config/project.json.
# Every byte count is a [long] and the peaks are found by comparing [long] values. [Math]::Max with an Int32 first argument binds to
# Max(Int32, Int32) in PowerShell 7 and throws once a value passes 2 GiB, and an empty catch turned that into a peak frozen under
# 2 GB before (Round ADDIN Phase 1).

# One sample of a running process: private memory and working set now and the peak of each that Windows keeps (PeakPagefileUsage,
# which .NET names PeakPagedMemorySize64, and PeakWorkingSetSize), whether it responds, and the free physical memory of the machine.
# Throws when the process has ended
function Get-WindowSample {
    param([Parameter(Mandatory)][Diagnostics.Process]$Process)
    $Process.Refresh()
    $os = Get-CimInstance Win32_OperatingSystem
    [ordered]@{
        utc                 = (Get-Date).ToUniversalTime().ToString('o')
        privateBytes        = [long]$Process.PrivateMemorySize64
        peakPrivateBytes    = [long]$Process.PeakPagedMemorySize64
        workingSetBytes     = [long]$Process.WorkingSet64
        peakWorkingSetBytes = [long]$Process.PeakWorkingSet64
        responding          = [bool]$Process.Responding
        freeBytes           = [long]$os.FreePhysicalMemory * 1024
    }
}

# The peaks of one window in MB: the true peak of private memory and of working set, the larger of the samples and the peak Windows
# keeps between them, and the largest sampled values apart. Then the least free memory in GB, how many samples had less free memory
# than the floor of R200 and how many found Revit not responding. With no sample every peak is null
function Get-WindowPeaks {
    param([object[]]$Samples, [double]$FloorGB)
    [long]$private = -1
    [long]$working = -1
    [long]$truePrivate = -1
    [long]$trueWorking = -1
    [long]$free = -1
    $under = 0
    $hung = 0
    $count = 0
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
        if ($free -lt 0 -or [long]$sample.freeBytes -lt $free) { $free = [long]$sample.freeBytes }
        if ([long]$sample.freeBytes -lt $FloorGB * 1GB) { $under++ }
        if (-not $sample.responding) { $hung++ }
    }
    [ordered]@{
        samples             = $count
        peakPrivateMb       = $(if ($count) { [long][Math]::Round($truePrivate / 1MB) } else { $null })
        peakWorkingSetMb    = $(if ($count) { [long][Math]::Round($trueWorking / 1MB) } else { $null })
        sampledPrivateMb    = $(if ($count) { [long][Math]::Round($private / 1MB) } else { $null })
        sampledWorkingSetMb = $(if ($count) { [long][Math]::Round($working / 1MB) } else { $null })
        minFreeGb           = $(if ($count) { [Math]::Round($free / 1GB, 2) } else { $null })
        floorGb             = $FloorGB
        samplesUnderFloor   = $under
        notResponding       = $hung
    }
}
