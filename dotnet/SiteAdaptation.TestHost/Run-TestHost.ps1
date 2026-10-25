# One run of the Revit test host for one prototype (R103, R104), or with -DryStart one start of Revit with no job.
# It copies the site model to <name>_TEST-<code>.rvt by a file copy, writes the job file, starts Revit 2024 with the
# token of this run on Revit alone, runs the dialog watcher of R108 beside it and waits for Revit to exit.
# The original model is never opened. Nothing in the registry is read or written.
# With -Upgrade it saves one prototype as a Revit 2024 file in the upgraded prototype folder of config/project.json (R109).
# -NewUpgradeFolder makes that folder and stops when it is there already, so it goes on the first upgrade only.
# With -FloorSpec it runs a master, floors or linksAndFloors job of R117 on a copy python/scenario_files.py made beforehand.
# The spec names kind, code, model and the options, and this script never copies, moves or deletes a model.
# With -RoundSpec it runs a siteUpgrade, read, newSite, image or linksAndFloors job of R204 to R214 (a read of R245 may name a test copy of handCheck.folders) on the files of
# siteBuild in config/project.json, all in its work folder. It never copies, moves or deletes a model either.
param(
    [ValidatePattern('^[A-Z][A-Z0-9]{1,3}$')]
    [string]$Code,
    [switch]$DryStart,
    [switch]$Upgrade,
    [switch]$NewUpgradeFolder,
    [string]$FloorSpec,
    [string]$RoundSpec,
    [int]$TimeoutMinutes = 30,
    [int]$NoLogMinutes = 8
)

$ErrorActionPreference = 'Stop'
$spec = $null
if ($FloorSpec) {
    if ($Code -or $DryStart -or $Upgrade) {
        throw '-FloorSpec goes alone, its code is in the spec.'
    }
    $spec = Get-Content -Raw $FloorSpec | ConvertFrom-Json
    if (@('master', 'floors', 'linksAndFloors') -notcontains $spec.kind) {
        throw "The spec kind is not master, floors or linksAndFloors: $($spec.kind)"
    }
    if ($spec.code -notmatch '^(MST|S[1-9][0-9]{0,2})$') {
        throw "The spec code is not MST or S<n>: $($spec.code)"
    }
    # An empty list would reach the job as no list, which tries every plot
    foreach ($list in 'plots', 'deletePlots') {
        if ($spec.PSObject.Properties.Name -contains $list -and @($spec.$list).Count -eq 0) {
            throw "The spec $list is empty, leave it out to mean every plot"
        }
    }
    $Code = $spec.code
}
$round = $null
if ($RoundSpec) {
    if ($Code -or $DryStart -or $Upgrade -or $FloorSpec) {
        throw '-RoundSpec goes alone, its code is in the spec.'
    }
    $round = Get-Content -Raw $RoundSpec | ConvertFrom-Json
    if (@('siteUpgrade', 'read', 'newSite', 'image', 'linksAndFloors') -notcontains $round.kind) {
        throw "The round spec kind is not siteUpgrade, read, newSite, image or linksAndFloors: $($round.kind)"
    }
    if ($round.code -notmatch '^[A-Z][A-Z0-9]{1,3}$') {
        throw "The round spec code is not a code of 2 to 4 capitals and digits: $($round.code)"
    }
    $Code = $round.code
}
if (-not $DryStart -and -not $Code) {
    throw 'Give -Code, or -DryStart for a start with no job.'
}
if ($NewUpgradeFolder -and -not $Upgrade) {
    throw '-NewUpgradeFolder goes with -Upgrade only.'
}
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$config = Get-Content -Raw (Join-Path $repo 'config\project.json') | ConvertFrom-Json
$docs = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'SiteAdaptation'
$addins = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024'
$watcherExe = Join-Path $addins 'SiteAdaptationTestHost\Watcher\SiteAdaptation.DialogWatcher.exe'
$name = if ($DryStart) { 'dry_start' } elseif ($Upgrade -or ($round -and $round.kind -eq 'siteUpgrade')) { $Code + '_upgrade' } else { $Code }
# R110. One job file per window, its path goes to that Revit alone in SITEADAPT_JOB. Other Revit windows may run beside it
$jobPath = if ($DryStart) { $null } else { Join-Path $docs ('testhost_job_' + $name + '.json') }
$refused = if ($jobPath) { [IO.Path]::ChangeExtension($jobPath, '.refused.json') } else { $null }
$logPath = Join-Path $docs ('testhost_' + $name + '_log.json')
$watchPath = Join-Path $docs ('testhost_' + $name + '_watcher.json')
$runPath = Join-Path $docs ('testhost_' + $name + '_run.json')

if ($jobPath -and (Test-Path $jobPath)) {
    throw "A job file is already waiting at $jobPath. Look at it before you run again."
}
if (-not (Test-Path $watcherExe)) {
    throw "The dialog watcher is not installed at $watcherExe. Run Install-TestHost.ps1 first."
}

# Revit.exe of the Revit 2024 install folder, found by its own version and not guessed
$found = @(Get-ChildItem (Join-Path $env:ProgramFiles 'Autodesk') -Directory | ForEach-Object { Join-Path $_.FullName 'Revit.exe' } |
    Where-Object { Test-Path $_ } | Where-Object { $v = (Get-Item $_).VersionInfo
        $v.ProductName -eq 'Autodesk Revit' -and $v.ProductMajorPart -eq 24 })
if ($found.Count -ne 1) {
    throw "Found $($found.Count) Revit.exe files of version 24, one is needed: $($found -join ', ')"
}
$revit = $found[0]

# The three add-ins of this tool by their AddInId and name in the repo manifests. The watcher stops Revit if one of them asks
$manifests = @('dotnet\RevitPrototypeReader\SiteAdaptation.addin', 'dotnet\SiteAdaptation.Placer\SiteAdaptationPlacer_Revit2024.addin', 'dotnet\SiteAdaptation.TestHost\SiteAdaptationTestHost_Revit2024.addin')
$ourIds = @()
$ourMarks = @()
foreach ($manifest in $manifests) {
    [xml]$xml = Get-Content -Raw (Join-Path $repo $manifest)
    $ourIds += [string]$xml.RevitAddIns.AddIn.AddInId
    $ourMarks += [string]$xml.RevitAddIns.AddIn.Name
    $ourMarks += Split-Path ([string]$xml.RevitAddIns.AddIn.Assembly) -Leaf
}

# The three DLLs as Install-TestHost.ps1 puts them. Only a box that names one of these paths gets Load Once
$ourPaths = @(
    (Join-Path $addins 'SiteAdaptation\RevitPrototypeReader.dll')
    (Join-Path $addins 'SiteAdaptationPlacer\SiteAdaptation.Placer.dll')
    (Join-Path $addins 'SiteAdaptationTestHost\SiteAdaptation.TestHost.dll')
)

$original = $null
$copy = $null
$token = $null
$roundJob = $null

# R213. The new site file a newSite job makes and a linksAndFloors job places into. It is empty between rounds, never a kept file
function Get-SiteNewFile($build) {
    if (-not $build.newFile) {
        throw 'siteBuild.newFile of config/project.json is empty, set it to the new site file <name>-TEST-NEW.rvt of the round first'
    }
    return [string]$build.newFile
}

if ($round) {
    # R204 to R214. Every file sits in the work folder of siteBuild, and the file named in $original must not change
    $build = $config.siteBuild
    $work = Join-Path $build.folder 'work'
    $roundJob = [ordered]@{
        kind  = $round.kind
        token = $null
        code  = $Code
    }
    $keyStem = [IO.Path]::GetFileNameWithoutExtension($build.answerKey.model)
    switch ($round.kind) {
        'siteUpgrade' {
            $original = Join-Path $work $build.answerKey.model
            $target = Join-Path $work ($keyStem + '_TEST-' + $Code + '.rvt')
            if (Test-Path $target) {
                throw "The upgraded copy is already there, it is never overwritten: $target"
            }
            $roundJob.source = $original
            $roundJob.target = $target
        }
        'read' {
            # R227. A read may name its own test copy in the work folder, such as a prototype, else it reads the key copy.
            # R245. It may also name a test copy <name>_TEST-<code>.rvt made beforehand in a folder of handCheck.folders
            $copy = if ($round.model) { [string]$round.model } else { Join-Path $work ($keyStem + '_TEST-' + $Code + '.rvt') }
            $handCheck = @($config.handCheck.folders) -contains (Split-Path $copy)
            if ((Split-Path $copy) -ne $work -and -not $handCheck) {
                throw "The read model is not in the work folder $work or a folder of handCheck.folders in config/project.json: $copy"
            }
            if ($handCheck -and [IO.Path]::GetFileName($copy) -notmatch ('_TEST-' + $Code + '\.rvt$')) {
                throw "The read model is not named <name>_TEST-$Code.rvt: $copy"
            }
            $original = $copy
            $roundJob.model = $copy
        }
        'image' {
            $copy = [string]$round.model
            if ((Split-Path $copy) -ne $work) {
                throw "The image model is not in the work folder $work"
            }
            $original = $copy
            $roundJob.model = $copy
            $roundJob.imageFolder = Join-Path $build.folder 'images'
            $roundJob.imageLevel = [string]$round.imageLevel
            $roundJob.area = [double[]]@($round.area)
            # R302. The image step runs at most testHost.imageStepMinutes of config/project.json. The job may run that plus
            # testHost.imageMarginMinutes for the open and the close, and the window 3 minutes more for Revit to exit
            $imageMinutes = [double]$config.testHost.imageStepMinutes
            $imageMargin = [double]$config.testHost.imageMarginMinutes
            if (-not ($imageMinutes -gt 0) -or -not ($imageMargin -gt 0)) {
                throw 'config/project.json has no testHost.imageStepMinutes and imageMarginMinutes above 0'
            }
            $imageTotal = $imageMinutes + $imageMargin
            if (-not $PSBoundParameters.ContainsKey('TimeoutMinutes')) {
                $TimeoutMinutes = [int][Math]::Ceiling($imageTotal + 3)
            }
            elseif ($TimeoutMinutes -lt $imageTotal + 3) {
                throw "An image job of $imageMinutes minutes with a margin of $imageMargin needs -TimeoutMinutes $($imageTotal + 3) or more, or none"
            }
            $roundJob.imageMinutes = $imageMinutes
            $roundJob.imageMarginMinutes = $imageMargin
            # R234. The links load for the image, from linkFolder by file name when their saved path is not there
            if ($round.loadLinks) {
                $roundJob.loadLinks = $true
                if ($round.linkFolder) {
                    $roundJob.linkFolder = [string]$round.linkFolder
                }
            }
        }
        'newSite' {
            $copy = Join-Path $work (Get-SiteNewFile $build)
            if (-not ([string]$round.site).StartsWith($build.folder + [IO.Path]::DirectorySeparatorChar)) {
                throw "The site file is not in $($build.folder)"
            }
            $original = $build.template
            $roundJob.model = $copy
            $roundJob.template = $build.template
            $roundJob.site = [string]$round.site
        }
        'linksAndFloors' {
            $copy = Join-Path $work (Get-SiteNewFile $build)
            $planPath = [string]$round.plan
            $plan = Get-Content -Raw $planPath | ConvertFrom-Json
            $files = @($plan.plots | ForEach-Object { $_.prototypeFile.value } | Where-Object { $_ -match ('-' + $round.prototypeCode + '-ZZ-MOD-') } | Sort-Object -Unique)
            if ($files.Count -ne 1) {
                throw "The plan names $($files.Count) prototype files for $($round.prototypeCode), one is needed: $($files -join ', ')"
            }
            $original = $planPath
            $roundJob.model = $copy
            $roundJob.prototypeFile = $files[0]
            $roundJob.plan = $planPath
            # R231. One run per prototype code places the links and the floors of the plots of that code only
            $roundJob.plots = [string[]]@($plan.plots | Where-Object { $_.prototypeFile.value -eq $files[0] } | ForEach-Object { [string]$_.plot })
        }
    }
    $before = Get-Item $original
    $hashBefore = (Get-FileHash $original).Hash
}
if (-not $DryStart -and -not $round) {
    $root = if ($env:SITE_ADAPTATION_INPUT) { $env:SITE_ADAPTATION_INPUT } else { $config.inputRoot }
    $original = Join-Path $root $config.testHost.model
    $planPath = if ($spec -and $spec.plan) { $spec.plan } else { $config.pastJob.output }

    # The prototype file of the code comes from the plan, never from the code alone. A floor spec names its prototype code, if any
    $plan = Get-Content -Raw $planPath | ConvertFrom-Json
    $prototypeCode = if ($spec) { $spec.prototypeCode } else { $Code }
    $prototype = $null
    $plots = @($plan.plots).Count
    if ($prototypeCode) {
        $files = @($plan.plots | ForEach-Object { $_.prototypeFile.value } | Where-Object { $_ -match ('-' + $prototypeCode + '-ZZ-MOD-') } | Sort-Object -Unique)
        if ($files.Count -ne 1) {
            throw "The plan names $($files.Count) prototype files for $prototypeCode, one is needed: $($files -join ', ')"
        }
        $prototype = $files[0]
        $plots = @($plan.plots | Where-Object { $_.prototypeFile.value -eq $prototype }).Count
    }

    $master = $null
    if ($spec) {
        # R117. The copy is the master or a work file next to the site model, made beforehand. Its name must fit the kind and the code
        $folder = Split-Path $original
        $base = [IO.Path]::GetFileNameWithoutExtension($original)
        $master = Join-Path $folder ($base + '_TEST-MASTER.rvt')
        $wanted = if ($spec.kind -eq 'master') { $master } else { Join-Path $folder ($base + '_TEST-WORK-' + $Code + '.rvt') }
        if ($spec.model -ne $wanted) {
            throw "The spec model is not $wanted"
        }
        if (-not (Test-Path $wanted)) {
            throw "The copy is not there, make it with python/scenario_files.py first: $wanted"
        }
        $copy = $wanted
    }
    elseif ($Upgrade) {
        # The source is the past prototype folder, kept in the config. It is only read, the run checks it is unchanged
        $source = Join-Path (Join-Path $root $config.pastJob.prototypeFolder) $prototype
        $targetFolder = Join-Path $root $config.pastJob.upgradedPrototypeFolder
        $target = Join-Path $targetFolder $prototype
        if ($NewUpgradeFolder) {
            if (Test-Path $targetFolder) {
                throw "The upgraded prototype folder is already there, nothing was done: $targetFolder"
            }
            New-Item -ItemType Directory $targetFolder | Out-Null
            "Made the folder $targetFolder"
        }
        elseif (-not (Test-Path $targetFolder)) {
            throw "The upgraded prototype folder is not there, the first upgrade takes -NewUpgradeFolder: $targetFolder"
        }
        if (Test-Path $target) {
            throw "The upgraded file is already there, it is never overwritten: $target"
        }
        $original = $source
    }
    else {
        # A fresh copy every run. An earlier copy of the same code is kept under the next free run number
        $folder = Split-Path $original
        $base = [IO.Path]::GetFileNameWithoutExtension($original)
        $copy = Join-Path $folder ($base + '_TEST-' + $Code + '.rvt')
        if (Test-Path $copy) {
            $n = 1
            while (Test-Path (Join-Path $folder ($base + '_TEST-' + $Code + '_run' + $n + '.rvt'))) {
                $n++
            }
            Rename-Item $copy ($base + '_TEST-' + $Code + '_run' + $n + '.rvt')
        }
    }
    $before = Get-Item $original
    $hashBefore = (Get-FileHash $original).Hash
    $masterBefore = $null
    if ($spec -and $spec.kind -ne 'master') {
        $masterBefore = (Get-FileHash $master).Hash
    }
    if (-not $Upgrade -and -not $spec) {
        Copy-Item $original $copy
    }
}

# Outputs of an earlier run of this name are moved aside, so a run that writes nothing cannot pass on old files
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$old = @(Get-ChildItem $docs -Filter ('testhost_' + $name + '_*.json') | Where-Object { $Upgrade -or $_.Name -notlike ('testhost_' + $Code + '_upgrade_*') })
if ($old.Count -gt 0) {
    $aside = Join-Path $docs ('testhost_old\' + $name + '_' + $stamp)
    New-Item -ItemType Directory -Force $aside | Out-Null
    $old | Move-Item -Destination $aside
}

$start = Get-Date
$end = $null
$exited = $false
$stopReason = $null
$process = $null
$watcher = $null
try {
    $info = [Diagnostics.ProcessStartInfo]::new($revit)
    $info.UseShellExecute = $false
    $info.WorkingDirectory = Split-Path $revit
    if (-not $DryStart) {
        $token = [guid]::NewGuid().ToString('N')
        if ($round) {
            $job = $roundJob
            $job.token = $token
            $job.createdUtc = (Get-Date).ToUniversalTime().ToString('o')
            $job.totalMinutes = [Math]::Min(27, $TimeoutMinutes - 2)
            if ($round.kind -eq 'image') {
                $job.totalMinutes = [Math]::Min($imageTotal, $TimeoutMinutes - 2)
            }
        }
        elseif ($spec) {
            # Assigned outside $() so a list of one plot stays a list and is written as a JSON array
            $deletePlots = $null
            if ($spec.deletePlots) {
                $deletePlots = [string[]]@($spec.deletePlots | ForEach-Object { [string]$_ })
            }
            $planPlots = $null
            if ($spec.plots) {
                $planPlots = [string[]]@($spec.plots | ForEach-Object { [string]$_ })
            }
            $job = [ordered]@{
                kind          = $spec.kind
                token         = $token
                code          = $Code
                model         = $copy
                prototypeFile = $prototype
                plan          = $planPath
                deleteFloors  = $spec.deleteFloors
                deletePlots   = $deletePlots
                plots         = $planPlots
                dryRunOnly    = $spec.dryRunOnly
                runTwice      = $spec.runTwice
                expect        = $spec.expect
                createdUtc    = (Get-Date).ToUniversalTime().ToString('o')
                totalMinutes  = [Math]::Min(29, $TimeoutMinutes - 1)
            }
        }
        elseif ($Upgrade) {
            $job = [ordered]@{
                kind          = 'upgrade'
                token         = $token
                code          = $Code
                prototypeFile = $prototype
                source        = $source
                target        = $target
                createdUtc    = (Get-Date).ToUniversalTime().ToString('o')
                totalMinutes  = [Math]::Min(27, $TimeoutMinutes - 2)
            }
        }
        else {
            $job = [ordered]@{
                token         = $token
                code          = $Code
                model         = $copy
                prototypeFile = $prototype
                plan          = $planPath
                createdUtc    = (Get-Date).ToUniversalTime().ToString('o')
                totalMinutes  = [Math]::Min(27, $TimeoutMinutes - 2)
            }
        }
        $job | ConvertTo-Json -Depth 5 | Set-Content -Path $jobPath -Encoding utf8
        # The token goes to the Revit process alone, this console never holds it
        $info.Environment['SITEADAPTATION_TESTHOST_TOKEN'] = $token
        $info.Environment['SITEADAPT_JOB'] = $jobPath
    }
    $process = [Diagnostics.Process]::Start($info)
    $peak = 0
    $watchArgs = @('--pid', $process.Id, '--out', $watchPath, '--stop-log', $logPath, '--minutes', '10', '--ours', ($ourIds -join ','), '--marks', ($ourMarks -join '|'), '--our-paths', ($ourPaths -join '|'))
    $watcher = Start-Process -FilePath $watcherExe -ArgumentList ($watchArgs | ForEach-Object { '"' + $_ + '"' }) -PassThru -WindowStyle Hidden
    if ($DryStart) {
        "Revit $($process.Id) started at $($start.ToString('HH:mm:ss')) with no job, the watcher answers the security dialogs"
    }
    elseif ($Upgrade) {
        "Revit $($process.Id) started at $($start.ToString('HH:mm:ss')) to upgrade $prototype into $targetFolder"
    }
    else {
        "Revit $($process.Id) started at $($start.ToString('HH:mm:ss')) for $Code, $prototype, $plots plots in the plan, copy $copy"
    }

    $deadline = $start.AddMinutes($TimeoutMinutes)
    $quietSince = $null
    while ($true) {
        if ($process.WaitForExit(5000)) {
            $exited = $true
            break
        }
        $now = Get-Date
        if ($now -gt $deadline) {
            $stopReason = "Revit did not exit in $TimeoutMinutes minutes"
            break
        }
        if ($watcher.HasExited -and $watcher.ExitCode -eq 3) {
            $stopReason = 'the watcher stopped Revit on a dialog it may not answer'
            break
        }
        try {
            $process.Refresh()
            $peak = [Math]::Max($peak, $process.PeakWorkingSet64)
        }
        catch {
            # The process ended between two looks, the next WaitForExit sees it
        }
        if ($refused -and (Test-Path $refused) -and (Get-Item $refused).LastWriteTime -gt $start) {
            $stopReason = 'the test host refused the job: ' + (Get-Content -Raw $refused | ConvertFrom-Json).reason
            break
        }
        if (-not $DryStart -and -not ((Test-Path $logPath) -and (Get-Item $logPath).LastWriteTime -gt $start) -and $now -gt $start.AddMinutes($NoLogMinutes)) {
            $stopReason = "the test host wrote no log in $NoLogMinutes minutes"
            break
        }
        if ($DryStart) {
            # The start screen is reached when the main window has its title, Revit answers and the watcher has finished its dialogs for a minute
            $process.Refresh()
            $ready = $process.MainWindowTitle -and $process.Responding
            if ($ready -and -not $quietSince) {
                $quietSince = $now
            }
            if (-not $ready) {
                $quietSince = $null
            }
            if ($quietSince -and $now -gt $quietSince.AddSeconds(60)) {
                "Start screen reached: $($process.MainWindowTitle). Closing Revit."
                $process.CloseMainWindow() | Out-Null
                $exited = $process.WaitForExit(180000)
                if (-not $exited) {
                    $stopReason = 'Revit did not close from the start screen in 3 minutes'
                }
                break
            }
        }
    }
    if (-not $exited) {
        $process.Refresh()
        "Stopping Revit: $stopReason. Window title: $($process.MainWindowTitle)"
        if (Test-Path $logPath) {
            $stuck = (Get-Content -Raw $logPath | ConvertFrom-Json).steps | Where-Object { -not $_.endedUtc } | Select-Object -First 1
            if ($stuck) {
                "Stuck in step: $($stuck.name)"
            }
        }
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit(60000) | Out-Null
        }
    }
    elseif ($stopReason) {
        "Revit ended: $stopReason"
    }
}
finally {
    $end = Get-Date
    if ($watcher -and -not $watcher.HasExited) {
        $watcher.WaitForExit(30000) | Out-Null
        if (-not $watcher.HasExited) {
            Stop-Process -Id $watcher.Id -Force
        }
    }
    if ($token -and (Test-Path $jobPath)) {
        $left = Get-Content -Raw $jobPath | ConvertFrom-Json
        if ($left.token -eq $token) {
            Remove-Item $jobPath
            'The job file was still there, the test host did not claim it. It is removed now.'
        }
    }
}

# The dialogs the watcher answered go into the log of the run, with time, add-in name, vendor, GUID and button
if (Test-Path $watchPath) {
    $watch = Get-Content -Raw $watchPath | ConvertFrom-Json
    $log = if (Test-Path $logPath) {
        Get-Content -Raw $logPath | ConvertFrom-Json
    }
    else {
        [pscustomobject]@{
            tool     = 'Run-TestHost.ps1'
            dryStart = [bool]$DryStart
        }
    }
    $log | Add-Member -Force -NotePropertyName securityDialogs -NotePropertyValue @($watch.dialogs)
    $summary = [pscustomobject]@{
        result  = $watch.result
        windows = @($watch.windows)
        notes   = @($watch.notes)
    }
    $log | Add-Member -Force -NotePropertyName watcher -NotePropertyValue $summary
    $log | ConvertTo-Json -Depth 20 | Set-Content -Path $logPath -Encoding utf8
    "Security dialogs: $(@($watch.dialogs).Count), watcher: $($watch.result)"
    @($watch.dialogs) | Select-Object utc, addInName, vendor, addInId, button, action | Format-Table -AutoSize | Out-String -Width 250
    foreach ($stopped in @($watch.dialogs | Where-Object { $_.action -ne 'DoNotLoad' -and $_.action -ne 'LoadOnceOurs' })) {
        "Dialog the watcher stopped on, full text:"
        $stopped.title
        $stopped.text
        'Buttons: ' + ($stopped.buttons -join ' | ')
    }
}
else {
    'The watcher wrote no report.'
}

"Run time {0:0.0} minutes, exit code {1}, Revit process {2}, peak memory {3:0} MB" -f ($end - $start).TotalMinutes, $(if ($exited) { $process.ExitCode } else { 'stopped' }), $process.Id, ($peak / 1MB)
# R110. One line per job for the parallel report
$logResult = if (Test-Path $logPath) { (Get-Content -Raw $logPath | ConvertFrom-Json).result } else { $null }
[ordered]@{
    name       = $name
    pid        = $process.Id
    startUtc   = $start.ToUniversalTime().ToString('o')
    endUtc     = $end.ToUniversalTime().ToString('o')
    minutes    = [Math]::Round(($end - $start).TotalMinutes, 2)
    peakMb     = [Math]::Round($peak / 1MB)
    exitCode   = $(if ($exited) { $process.ExitCode } else { $null })
    stopReason = $stopReason
    logResult  = $logResult
} | ConvertTo-Json | Set-Content -Path $runPath -Encoding utf8
if (-not $DryStart) {
    $after = Get-Item $original
    $same = ((Get-FileHash $original).Hash -eq $hashBefore) -and ($after.LastWriteTimeUtc -eq $before.LastWriteTimeUtc) -and ($after.Length -eq $before.Length)
    if ($round) {
        "Unchanged, $(Split-Path $original -Leaf): $same"
    }
    elseif ($Upgrade) {
        "Source prototype unchanged: $same"
    }
    else {
        "Original model unchanged: $same"
    }
    if ($masterBefore) {
        "Master unchanged: $((Get-FileHash $master).Hash -eq $masterBefore)"
    }
}

if (Test-Path $logPath) {
    $log = Get-Content -Raw $logPath | ConvertFrom-Json
    if ($log.steps) {
        "Log result: $($log.result)"
        $log.steps | Select-Object name, result, seconds, detail | Format-Table -Wrap -AutoSize | Out-String -Width 250
        if ($log.linkOpenings) {
            'Link openings:'
            $log.linkOpenings | Select-Object @{
                n = 'file'
                e = { Split-Path $_.path -Leaf }
            }, step, seconds | Format-Table -AutoSize | Out-String -Width 200
        }
        "Dialogs in Revit $(@($log.dialogs).Count), failure lines $(@($log.failures).Count)"
        foreach ($step in @($log.steps | Where-Object { $_.exception })) {
            "Exception in $($step.name):"
            $step.exception
        }
    }
}
if ($Upgrade) {
    'The upgraded prototype folder:'
    Get-ChildItem $targetFolder | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 200
}
elseif ($round) {
    'Files of the work folder:'
    Get-ChildItem $work | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 200
}
elseif (-not $DryStart) {
    'TEST copies next to the model:'
    Get-ChildItem (Split-Path $original) | Where-Object { $_.Name -like ($base + '_TEST-*') } | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 200
}
