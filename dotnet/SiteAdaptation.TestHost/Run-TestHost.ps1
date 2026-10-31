# One run of the Revit test host for one prototype (R103, R104), or with -DryStart one start of Revit with no job.
# It copies the site model to <name>_TEST-<code>.rvt by a file copy, writes the job file, starts Revit 2024 with the
# token of this run on Revit alone, runs the dialog watcher of R108 beside it and waits for Revit to exit.
# The original model is never opened. Nothing in the registry is read or written.
# With -Upgrade it saves one prototype as a Revit 2024 file in the upgraded prototype folder of config/project.json (R109).
# With -PrototypeSet it takes the source and target folders of that set of prototypeSets instead and the job code is the
# prefix of the set and -Code, as STH1 for the STR prototype of TH1 (R313).
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
    [string]$PrototypeSet,
    [string]$FloorSpec,
    [string]$RoundSpec,
    [int]$TimeoutMinutes = 30,
    [int]$NoLogMinutes = 8,
    # Lane B 5b. With -DryStart, one probe add-in of its own manifest loads beside the tool, its log and heartbeat named by the run
    [string]$ProbeManifest,
    # R104, Lane B 5b. The freeze guard: no first log step, no heartbeat, Revit not responding
    [int]$FirstStepMinutes = 5,
    [int]$HeartbeatMinutes = 10,
    [int]$NotRespondingMinutes = 5
)

$ErrorActionPreference = 'Stop'
# R310. The memory samples of the window
. (Join-Path $PSScriptRoot 'WindowSamples.ps1')
# R313. The prototype sets an upgrade may take
. (Join-Path $PSScriptRoot 'PrototypeSets.ps1')
# R108. The add-ins of this tool the watcher answers Load Once
. (Join-Path $PSScriptRoot 'TestHostPaths.ps1')
# R108 the watcher before Revit, R104 the freeze guard
. (Join-Path $PSScriptRoot 'FreezeGuard.ps1')
$probe = $null
if ($ProbeManifest) {
    if (-not $DryStart) {
        throw '-ProbeManifest goes with -DryStart, a probe has no job of the test host.'
    }
    $probe = Get-ProbeAddIn -Manifest $ProbeManifest
    if ($probe.code -notmatch '^[A-Za-z0-9_-]{1,40}$') {
        throw "The probe manifest name is not 1 to 40 letters, digits, - or _: $($probe.code)"
    }
}
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
    if (@('siteUpgrade', 'read', 'newSite', 'image', 'linksAndFloors', 'addinRun') -notcontains $round.kind) {
        throw "The round spec kind is not siteUpgrade, read, newSite, image, linksAndFloors or addinRun: $($round.kind)"
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
if ($PrototypeSet -and -not $Upgrade) {
    throw '-PrototypeSet goes with -Upgrade only.'
}
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$config = Get-Content -Raw (Join-Path $repo 'config\project.json') | ConvertFrom-Json
# R313. The parts of a prototype file name, written into every job that names a prototype file for the guard of the test host
$names = Get-PrototypeNames -Config $config
# R313. The code names the prototype, the job code of a set is its prefix and the code, so the outputs of a set have names of their own
$set = $null
$prototypeCode = $Code
if ($PrototypeSet) {
    $set = Get-PrototypeSet -Config $config -Name $PrototypeSet
    $Code = Get-SetJobCode -Set $set -Code $prototypeCode
}
# R372, QL2. The job, log and sample files sit in the work root of %APPDATA%\SiteAdaptation\machine.json, as the test host reads it
$docs = Get-WorkRoot -AppData $env:APPDATA -MyDocuments ([Environment]::GetFolderPath('MyDocuments'))
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$addins = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024'
$watcherExe = Join-Path $addins 'SiteAdaptationTestHost\Watcher\SiteAdaptation.DialogWatcher.exe'
$name = if ($DryStart) { 'dry_start' } elseif ($Upgrade -or ($round -and $round.kind -eq 'siteUpgrade')) { $Code + '_upgrade' } else { $Code }
# Lane B 5b. A probe names its own log, heartbeat and watcher files
if ($probe) {
    $name = 'probe_' + $probe.code
}
# R110. One job file per window, its path goes to that Revit alone in SITEADAPT_JOB. Other Revit windows may run beside it
$jobPath = if ($DryStart) { $null } else { Join-Path $docs ('testhost_job_' + $name + '.json') }
$refused = if ($jobPath) { [IO.Path]::ChangeExtension($jobPath, '.refused.json') } else { $null }
$logPath = Join-Path $docs ('testhost_' + $name + '_log.json')
$watchPath = Join-Path $docs ('testhost_' + $name + '_watcher.json')
$runPath = Join-Path $docs ('testhost_' + $name + '_run.json')
$samplesPath = Join-Path $docs ('testhost_' + $name + '_samples.jsonl')
# R104. The heartbeat the test host or the probe writes beside its log, and the pid file the watcher waits for (R108)
$beatPath = Join-Path $docs ('testhost_' + $name + '_heartbeat.json')
$pidPath = Join-Path $docs ('testhost_' + $name + '_watcher_pid.json')
$journals = Join-Path $env:LOCALAPPDATA 'Autodesk\Revit\Autodesk Revit 2024\Journals'
$sampleSeconds = [int]$config.testHost.sampleSeconds
if ($sampleSeconds -lt 1) {
    throw 'config/project.json has no testHost.sampleSeconds of 1 or more'
}
# R310. The watcher, refusal, log and start screen checks look every 5 s as before the samples, whatever sampleSeconds is
$checkSeconds = 5
# R310. Each CIM call of a sample ends at testHost.cimTimeoutSeconds, and two calls at that limit fit in the 5 s of the checks
$cimSeconds = [int]$config.testHost.cimTimeoutSeconds
if ($cimSeconds -lt 1 -or 2 * $cimSeconds -ge $checkSeconds) {
    throw "config/project.json has no testHost.cimTimeoutSeconds of 1 or more whose two calls fit in the $checkSeconds s of the checks"
}

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

# The four add-ins of this tool by their AddInId and name in the repo manifests and the DLLs as the install scripts put them.
# Only a box that names one of these DLLs gets Load Once, the watcher stops Revit on any other box of the tool
$tool = Get-ToolAddIns -Repo $repo -Addins $addins
$ourIds = $tool.ids
$ourMarks = $tool.marks
$ourPaths = $tool.paths
# Lane B 5b. The probe gets Load Once as the tool does, by the id, name and DLL of its own manifest, and is installed for this run only
$probeInstalled = $null
if ($probe) {
    $ourIds += $probe.id
    $ourMarks += $probe.name
    $ourMarks += Split-Path $probe.path -Leaf
    $ourPaths += $probe.path
    $probeInstalled = Join-Path $addins ('SiteAdaptationProbe_' + $probe.code + '.addin')
    if (Test-Path -LiteralPath $probeInstalled) {
        throw "A probe manifest is installed already at $probeInstalled. Look at it before you run again."
    }
}

$original = $null
$copy = $null
$token = $null
$roundJob = $null
$setUpSource = $null
$buttonsInputs = $null
$buttonsBefore = $null

# Lane B 5c (R384). The real files a buttons run reads in place: each input of the pane settings, every file of a folder input, the
# template and the key copy. Each is held by its size, write time and SHA-256 before and after the run (D19)
function Get-ButtonsInputs($paths) {
    $files = [Collections.Generic.List[string]]::new()
    foreach ($path in @($paths | Where-Object { $_ })) {
        if (Test-Path -LiteralPath $path -PathType Container) {
            Get-ChildItem -LiteralPath $path -File -Recurse | ForEach-Object { $files.Add($_.FullName) }
        }
        elseif (Test-Path -LiteralPath $path -PathType Leaf) {
            $files.Add((Get-Item -LiteralPath $path).FullName)
        }
        else {
            throw "A real input of the buttons run is not there: $path"
        }
    }
    return @($files | Sort-Object -Unique)
}

function Get-InputHashes($files) {
    $hashes = [ordered]@{}
    foreach ($file in $files) {
        $item = Get-Item -LiteralPath $file
        $hashes[$file] = '{0}|{1}|{2}' -f $item.Length, $item.LastWriteTimeUtc.Ticks, (Get-FileHash -LiteralPath $file).Hash
    }
    return $hashes
}

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
            # R204. The source is the copy of the answer key in the work folder, or another copy there that the spec names, such
            # as one of the 7 Parsons site models of Round ADDIN. The target takes the name of the source
            $original = if ($round.source) { [string]$round.source } else { Join-Path $work $build.answerKey.model }
            if ((Split-Path $original) -ne $work) {
                throw "The site upgrade source is not in the work folder $work`: $original"
            }
            $target = Join-Path $work ([IO.Path]::GetFileNameWithoutExtension($original) + '_TEST-' + $Code + '.rvt')
            if (Test-Path $target) {
                throw "The upgraded copy is already there, it is never overwritten: $target"
            }
            $roundJob.source = $original
            $roundJob.target = $target
            $roundJob.workFolder = $work
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
        'addinRun' {
            # Lane B 5a, docs/design/09-risks.md open items 1 and 2. The new projects of the job go into a run folder of their own under
            # the work root, made here and empty, never a file of the inputs. The template is the one file the job reads
            $run = New-AddinRunFolder -Docs $docs -Code $Code -Stamp $stamp
            $runFolder = $run.folder
            $copy = $run.model
            $original = $build.template
            $roundJob.model = $copy
            $roundJob.template = $build.template
            # Lane B 5b. With setUp the job runs button 1 on the four copies of 3.1. The answer key copy is taken into the run folder by
            # a file copy, its source hashed before and after (D19). The settings are config with the position file of this machine
            # (J5 joins the paths at 5c) and the rules profile beside it, as the settings of a project are read
            if ($round.setUp) {
                $setUpSource = [string]$round.keyCopy
                if (-not (Test-Path -LiteralPath $setUpSource -PathType Leaf)) {
                    throw "The key copy of the setUp run is not there: $setUpSource"
                }
                $setUpPosition = [string]$round.positionFile
                if (-not (Test-Path -LiteralPath $setUpPosition -PathType Leaf) -or -not $setUpPosition.StartsWith($docs + '\')) {
                    throw "The position file of the setUp run is not a file under the work root $docs : $setUpPosition"
                }
                $setUpBefore = Get-Item -LiteralPath $setUpSource
                $setUpHash = (Get-FileHash -LiteralPath $setUpSource).Hash
                Copy-Item -LiteralPath $setUpSource -Destination (Join-Path $runFolder ($Code + '-SETUP-IMD.rvt'))
                $settingsCopy = Get-Content -Raw (Join-Path $repo 'config\project.json') | ConvertFrom-Json
                $settingsCopy.addin.position.file = $setUpPosition
                New-Item -ItemType Directory (Join-Path $runFolder 'config') | Out-Null
                $settingsCopy | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath (Join-Path $runFolder 'config\project.json') -Encoding utf8
                $profileCopy = Join-Path $runFolder ([string]$config.profile)
                New-Item -ItemType Directory -Force (Split-Path $profileCopy) | Out-Null
                Copy-Item -LiteralPath (Join-Path $repo ([string]$config.profile)) -Destination $profileCopy
                $roundJob.setUp = $true
            }
            # QL20. The whole run, the exit of Revit included, ends by 15 minutes. CHOSEN: the job gets 13 and the window 15, so the
            # 2 minutes between leave Revit time to exit after the job posts it, before the window stops Revit. Tool timings
            $addinRunWindowMinutes = 15
            $addinRunJobMinutes = 13
            # Lane B 5c (R384). With buttons the job runs them on one NBH of one copy: the key copy of the spec taken into the run folder
            # by a file copy, else the new project the test host makes from the template. The settings are config with the position
            # file of this machine and the rules profile beside it, as setUp writes them, and the pane settings of R367 name the real
            # inputs of the spec in place. Every real input is hashed before and after (D19)
            if ($round.buttons) {
                $buttonsPosition = [string]$round.positionFile
                if (-not (Test-Path -LiteralPath $buttonsPosition -PathType Leaf) -or -not $buttonsPosition.StartsWith($docs + '\')) {
                    throw "The position file of the buttons run is not a file under the work root $docs : $buttonsPosition"
                }
                $settingsCopy = Get-Content -Raw (Join-Path $repo 'config\project.json') | ConvertFrom-Json
                $settingsCopy.addin.position.file = $buttonsPosition
                # R375. A data book folder of config with no full path lies in the work root, not in the work folder of the run, so the
                # copy names it in full and its files are real inputs read in place
                $bookFolder = [string]$settingsCopy.addin.readData.dataBookFolder
                if (-not [IO.Path]::IsPathRooted($bookFolder)) {
                    $bookFolder = Join-Path $docs $bookFolder
                    $settingsCopy.addin.readData.dataBookFolder = $bookFolder
                }
                $buttonsSources = @($build.template)
                if ($round.keyCopy) {
                    $buttonsSources += [string]$round.keyCopy
                }
                foreach ($entry in @($round.inputs.PSObject.Properties)) {
                    $buttonsSources += [string]$entry.Value
                }
                foreach ($bookFile in @($settingsCopy.addin.readData.dataBookFiles.PSObject.Properties)) {
                    $buttonsSources += Join-Path $bookFolder ([string]$bookFile.Value)
                }
                $buttonsInputs = Get-ButtonsInputs $buttonsSources
                $buttonsBefore = Get-InputHashes $buttonsInputs
                if ($round.keyCopy) {
                    Copy-Item -LiteralPath ([string]$round.keyCopy) -Destination $copy
                }
                $settingsPath = Join-Path $runFolder 'config\project.json'
                New-Item -ItemType Directory (Split-Path $settingsPath) | Out-Null
                $settingsCopy | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $settingsPath -Encoding utf8
                # R375. Button 2 reads the layer list beside the config it is given, so the copy of config takes it along
                $layersName = [string]$settingsCopy.addin.readData.cadLayers
                Copy-Item -LiteralPath (Join-Path $repo (Join-Path 'config' $layersName)) -Destination (Join-Path (Split-Path $settingsPath) $layersName)
                # R376. 4 Check reads the outline and offset files beside the config and, with no addin.overrideFile, the override list in
                # overrides beside it, which Save overrides writes. The run takes copies, so a Save writes the run folder and never the repo
                Get-ChildItem -LiteralPath (Join-Path $repo 'config') -File -Filter '*.json' | Where-Object { $_.Name -ne 'project.json' -and $_.Name -ne $layersName } | Copy-Item -Destination (Split-Path $settingsPath)
                Copy-Item -LiteralPath (Join-Path $repo 'config\overrides') -Destination (Split-Path $settingsPath) -Recurse
                $profileCopy = Join-Path $runFolder ([string]$config.profile)
                New-Item -ItemType Directory -Force (Split-Path $profileCopy) | Out-Null
                Copy-Item -LiteralPath (Join-Path $repo ([string]$config.profile)) -Destination $profileCopy
                $levelProfile = if ($round.levelProfile) { [string]$round.levelProfile } else { [string](Get-Content -Raw $profileCopy | ConvertFrom-Json).levelProfiles.default }
                $paneInputs = [ordered]@{}
                foreach ($entry in @($round.inputs.PSObject.Properties)) {
                    $paneInputs[$entry.Name] = [ordered]@{ path = [string]$entry.Value }
                }
                $paneSettings = [ordered]@{
                    formatVersion = 1
                    configFile    = $settingsPath
                    rulesFile     = $profileCopy
                    levelProfile  = $levelProfile
                    inputs        = $paneInputs
                    scope         = [ordered]@{
                        kind = 'nbh'
                        nbh  = [string]$round.nbh
                    }
                    dryRunFirst   = ($round.dryRunFirst -ne $false)
                }
                $paneSettingsPath = Join-Path $runFolder 'pane_settings.json'
                $paneSettings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $paneSettingsPath -Encoding utf8
                $roundJob.buttons = [int[]]@($round.buttons)
                $roundJob.nbh = [string]$round.nbh
                $roundJob.villaMode = [string]$round.villaMode
                $roundJob.linksAfterMasses = [bool]$round.linksAfterMasses
                $roundJob.dryRunFirst = ($round.dryRunFirst -ne $false)
                $roundJob.paneSettings = $paneSettingsPath
                # Two worker reads, the build of 4 Check and two runs of 7 take more than the 13 minutes of 5a. CHOSEN, tool timings
                $addinRunWindowMinutes = 31
                $addinRunJobMinutes = 29
            }
            if (-not $PSBoundParameters.ContainsKey('TimeoutMinutes')) {
                $TimeoutMinutes = $addinRunWindowMinutes
            }
            elseif ($TimeoutMinutes -gt $addinRunWindowMinutes) {
                throw "An addinRun job ends by $addinRunWindowMinutes minutes, -TimeoutMinutes $TimeoutMinutes is more"
            }
        }
        'linksAndFloors' {
            $copy = Join-Path $work (Get-SiteNewFile $build)
            $planPath = [string]$round.plan
            $plan = Get-Content -Raw $planPath | ConvertFrom-Json
            $files = @($plan.plots | ForEach-Object { $_.prototypeFile.value } | Where-Object { $_ -match ('-' + $round.prototypeCode + [regex]::Escape($names.suffix)) } | Sort-Object -Unique)
            if ($files.Count -ne 1) {
                throw "The plan names $($files.Count) prototype files for $($round.prototypeCode), one is needed: $($files -join ', ')"
            }
            $original = $planPath
            $roundJob.model = $copy
            $roundJob.prototypeFile = $files[0]
            $roundJob.prototypeNames = $names
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
    $sourceFolder = if ($set) { Join-Path $root $set.source } else { Join-Path $root $config.pastJob.prototypeFolder }

    if ($set) {
        # R313. A set takes the one file of the code from its own source folder, the plan names the ARC prototypes only
        $prototype = Get-SetPrototype -Folder $sourceFolder -Names $names -Discipline $set.discipline -Code $prototypeCode
        $plots = 0
    }
    else {
        # The prototype file of the code comes from the plan, never from the code alone. A floor spec names its prototype code, if any
        $plan = Get-Content -Raw $planPath | ConvertFrom-Json
        $prototypeCode = if ($spec) { $spec.prototypeCode } else { $Code }
        $prototype = $null
        $plots = @($plan.plots).Count
        if ($prototypeCode) {
            $files = @($plan.plots | ForEach-Object { $_.prototypeFile.value } | Where-Object { $_ -match ('-' + $prototypeCode + [regex]::Escape($names.suffix)) } | Sort-Object -Unique)
            if ($files.Count -ne 1) {
                throw "The plan names $($files.Count) prototype files for $prototypeCode, one is needed: $($files -join ', ')"
            }
            $prototype = $files[0]
            $plots = @($plan.plots | Where-Object { $_.prototypeFile.value -eq $prototype }).Count
        }
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
        # The source is the past prototype folder, kept in the config, or the source folder of a set (R313). It is only read, the
        # run checks it is unchanged
        $source = Join-Path $sourceFolder $prototype
        $targetFolder = if ($set) { Join-Path $root $set.target } else { Join-Path $root $config.pastJob.upgradedPrototypeFolder }
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
# A place run leaves the upgrade outputs of its code alone. A run named <code>_upgrade, an upgrade or a site upgrade, moves its own
$old = @(Get-ChildItem $docs -Filter ('testhost_' + $name + '_*.json*') | Where-Object { $name -ne $Code -or $_.Name -notlike ('testhost_' + $Code + '_upgrade_*') })
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
$samples = [Collections.Generic.List[object]]::new()
$sampleFailures = [Collections.Generic.List[string]]::new()
$helpersAtStart = @()
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
            elseif ($round.kind -eq 'addinRun') {
                $job.totalMinutes = [Math]::Min($addinRunJobMinutes, $TimeoutMinutes - 2)
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
            if ($prototype) {
                $job.prototypeNames = $names
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
            $job.prototypeNames = $names
            if ($set) {
                # R313. The guard of the test host checks the file name by the discipline and the job code by the prefix of the set
                $job.discipline = [string]$set.discipline
                $job.codePrefix = [string]$set.codePrefix
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
            $job.prototypeNames = $names
        }
        $job | ConvertTo-Json -Depth 5 | Set-Content -Path $jobPath -Encoding utf8
        # The token goes to the Revit process alone, this console never holds it
        $info.Environment['SITEADAPTATION_TESTHOST_TOKEN'] = $token
        $info.Environment['SITEADAPT_JOB'] = $jobPath
    }
    # R310. The helpers an earlier window left running, which the child figures of this window never count
    try {
        $helpersAtStart = @(Get-RunningHelpers -Names @($config.testHost.helperProcesses))
    }
    catch {
        $sampleFailures.Add((Get-Date).ToUniversalTime().ToString('o') + ' helpers at the start: ' + $_.Exception.Message)
    }
    # R108, Lane B 5b. The watcher is up and waiting before Revit starts, so no security box opens with nothing to answer it
    Remove-Item -LiteralPath $pidPath -ErrorAction SilentlyContinue
    $watchArgs = @('--pid-file', $pidPath, '--out', $watchPath, '--stop-log', $logPath, '--minutes', '10', '--ours', ($ourIds -join ','), '--marks', ($ourMarks -join '|'), '--our-paths', ($ourPaths -join '|'))
    $watcher = Start-WatcherFirst -WatcherExe $watcherExe -Arguments $watchArgs -ReadyPath $watchPath
    if ($probe) {
        Copy-Item -LiteralPath $probe.manifest -Destination $probeInstalled
        $info.Environment['SITEADAPT_PROBE_LOG'] = $logPath
        $info.Environment['SITEADAPT_PROBE_HEARTBEAT'] = $beatPath
    }
    $process = [Diagnostics.Process]::Start($info)
    Set-WatchedPid -Path $pidPath -ProcessId $process.Id
    if ($probe) {
        "Revit $($process.Id) started at $($start.ToString('HH:mm:ss')) with the probe $($probe.name) of $($probe.manifest), the watcher answers the security dialogs"
    }
    elseif ($DryStart) {
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
    # R104, Lane B 5b. A job or a probe writes steps and a heartbeat, a plain dry start only answers or not
    $hasJob = (-not $DryStart) -or [bool]$probe
    $firstStepAt = $null
    $notRespondingSince = $null
    # Q154, the recommended reading until Bader answers: an image step may hold Revit busy for its whole limit (Q063)
    if ($round -and $round.kind -eq 'image' -and -not $PSBoundParameters.ContainsKey('NotRespondingMinutes')) {
        $NotRespondingMinutes = [int][Math]::Ceiling($imageMinutes)
        "Freeze guard: an image job, Revit may not respond for up to $NotRespondingMinutes minutes (Q154)"
    }
    $clock = New-LookClock -Start $start -CheckSeconds $checkSeconds -SampleSeconds $sampleSeconds
    while ($true) {
        if ($process.WaitForExit((Get-LookWait -Clock $clock -Now (Get-Date)))) {
            $exited = $true
            break
        }
        $now = Get-Date
        # R310. The deadline comes first in every pass, so a slow sample never holds back the stop of Revit
        if ($now -gt $deadline) {
            $stopReason = "Revit did not exit in $TimeoutMinutes minutes"
            break
        }
        $due = Get-DueLooks -Clock $clock -Now $now
        if ($due.sample) {
            # R310. One sample per look, written at once so a window that is stopped keeps its samples. A CIM call that times out
            # leaves its fields null with the reason and the sample is still written
            try {
                $sample = Get-WindowSample -Process $process -TimeoutSeconds $cimSeconds
                $samples.Add($sample)
                $sample | ConvertTo-Json -Compress | Add-Content -Path $samplesPath -Encoding utf8
            }
            catch {
                # Revit ended between two looks, which the next WaitForExit sees, or a sample failed. Each is kept with its reason
                $sampleFailures.Add((Get-Date).ToUniversalTime().ToString('o') + ' ' + $_.Exception.Message)
            }
        }
        if (-not $due.check) {
            continue
        }
        if ($watcher.HasExited -and $watcher.ExitCode -eq 3) {
            $stopReason = 'the watcher stopped Revit on a dialog it may not answer'
            break
        }
        if ($refused -and (Test-Path $refused) -and (Get-Item $refused).LastWriteTime -gt $start) {
            $stopReason = 'the test host refused the job: ' + (Get-Content -Raw $refused | ConvertFrom-Json).reason
            break
        }
        if (-not $DryStart -and -not ((Test-Path $logPath) -and (Get-Item $logPath).LastWriteTime -gt $start) -and $now -gt $start.AddMinutes($NoLogMinutes)) {
            $stopReason = "the test host wrote no log in $NoLogMinutes minutes"
            break
        }
        # R104, Lane B 5b. The freeze guard looks in every check. It logs the windows and the journal tail, ends this Revit only and
        # fails the open step with the reason
        $process.Refresh()
        if ($process.Responding) {
            $notRespondingSince = $null
        }
        elseif (-not $notRespondingSince) {
            $notRespondingSince = $now
        }
        $freezeClock = Read-FreezeClock -LogPath $logPath -BeatPath $beatPath -Start $start
        if ($freezeClock.firstStepAt -and -not $firstStepAt) {
            $firstStepAt = $freezeClock.firstStepAt
        }
        $freeze = Get-FreezeReason -Start $start -Now $now -FirstStepAt $firstStepAt -LastBeatAt $freezeClock.lastBeatAt -NotRespondingSince $notRespondingSince -HasJob $hasJob -FirstStepMinutes $FirstStepMinutes -HeartbeatMinutes $HeartbeatMinutes -NotRespondingMinutes $NotRespondingMinutes
        if ($freeze) {
            $frozen = Stop-FrozenRevit -Process $process -Reason $freeze -LogPath $logPath -JournalFolder $journals -Start $start
            $stopReason = 'freeze guard: ' + $freeze
            "Freeze guard: $freeze. Revit $($frozen.pid) ended, open step: $($frozen.step)"
            'Windows of Revit at the stop:'
            @($frozen.windows) | Select-Object handle, class, title, visible | Format-Table -AutoSize | Out-String -Width 250
            "Journal tail of $($frozen.journal):"
            @($frozen.journalTail)
            break
        }
        if ($probe) {
            # The probe ends its run by a result in its log, then Revit is closed as from the start screen
            $probeResult = $null
            try {
                $probeResult = (Get-Content -Raw -LiteralPath $logPath -ErrorAction Stop | ConvertFrom-Json).result
            }
            catch {
                # No log yet, or the probe is writing it right now
            }
            if ($probeResult) {
                "The probe wrote its result $probeResult. Closing Revit."
                $process.CloseMainWindow() | Out-Null
                $exited = $process.WaitForExit(180000)
                if (-not $exited) {
                    $stopReason = 'Revit did not close after the probe in 3 minutes'
                }
                break
            }
        }
        elseif ($DryStart) {
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
    # The probe manifest was installed for this run only, and Revit reads the add-ins root at its next start
    if ($probeInstalled -and (Test-Path -LiteralPath $probeInstalled)) {
        Remove-Item -LiteralPath $probeInstalled
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

# R310. The peaks Windows kept to the exit of Revit, which the samples miss after their last look. They count also with no sample
$exitPeaks = $null
if ($process -and $process.HasExited) {
    try {
        $exitPeaks = Get-ExitPeaks -Process $process
    }
    catch {
        $sampleFailures.Add((Get-Date).ToUniversalTime().ToString('o') + ' exit peaks: ' + $_.Exception.Message)
    }
}
$peaks = Get-WindowPeaks -Samples $samples -FloorGB ([double]$config.siteBuild.minFreeGB) -Exit $exitPeaks
$peaks.sampleFailures = @($sampleFailures)
$peaks.helpersAtStart = @($helpersAtStart)
"Run time {0:0.0} minutes, exit code {1}, Revit process {2}" -f ($end - $start).TotalMinutes, $(if ($exited) { $process.ExitCode } else { 'stopped' }), $process.Id
"Memory of {0} samples every {1} s: peak private {2} MiB and peak working set {3} MiB with the peaks Windows keeps{4}, sampled {5} and {6} MiB, least free {7} GiB, under the floor of {8} GiB in {9}, not responding in {10}, samples failed {11}" -f $peaks.samples, $sampleSeconds, $peaks.peakPrivateMib, $peaks.peakWorkingSetMib, $(if ($peaks.throughExit) { ' to the exit' } else { ' to the last sample' }), $peaks.sampledPrivateMib, $peaks.sampledWorkingSetMib, $peaks.minFreeGib, $peaks.floorGib, $peaks.samplesUnderFloor, $peaks.notResponding, $sampleFailures.Count
"Processes Revit started: at most {0} in a sample ({1}), sampled private {2} MiB and working set {3} MiB, Revit and them together {4} MiB private, samples with no free memory {5} and no child list {6}" -f $peaks.childProcessesMax, (@($samples | ForEach-Object { $_.childNames } | Where-Object { $_ } | ForEach-Object { $_ -split ', ' } | Sort-Object -Unique) -join ', '), $peaks.sampledChildPrivateMib, $peaks.sampledChildWorkingSetMib, $peaks.sampledTotalPrivateMib, $peaks.freeMissing, $peaks.childMissing
"Helpers running before Revit started, not counted in the figures of this window: {0}" -f $(if ($helpersAtStart.Count) { (@($helpersAtStart | ForEach-Object { '{0} {1} ({2} MiB private)' -f $_.name, $_.id, $_.privateMib }) -join ', ') } else { 'none' })
# R110. One line per job for the parallel report
$logResult = if (Test-Path $logPath) { (Get-Content -Raw $logPath | ConvertFrom-Json).result } else { $null }
[ordered]@{
    name       = $name
    pid        = $process.Id
    startUtc   = $start.ToUniversalTime().ToString('o')
    endUtc     = $end.ToUniversalTime().ToString('o')
    minutes    = [Math]::Round(($end - $start).TotalMinutes, 2)
    peakMb     = $peaks.peakWorkingSetMib
    memory     = $peaks
    exitCode   = $(if ($exited) { $process.ExitCode } else { $null })
    stopReason = $stopReason
    logResult  = $logResult
} | ConvertTo-Json -Depth 5 | Set-Content -Path $runPath -Encoding utf8
if (-not $DryStart) {
    $after = Get-Item $original
    $same = ((Get-FileHash $original).Hash -eq $hashBefore) -and ($after.LastWriteTimeUtc -eq $before.LastWriteTimeUtc) -and ($after.Length -eq $before.Length)
    if ($round) {
        "Unchanged, $(Split-Path $original -Leaf): $same"
        if ($setUpSource) {
            $setUpAfter = Get-Item -LiteralPath $setUpSource
            $setUpSame = ((Get-FileHash -LiteralPath $setUpSource).Hash -eq $setUpHash) -and ($setUpAfter.LastWriteTimeUtc -eq $setUpBefore.LastWriteTimeUtc) -and ($setUpAfter.Length -eq $setUpBefore.Length)
            "Unchanged, $(Split-Path $setUpSource -Leaf): $setUpSame, $($setUpBefore.Length) bytes, SHA-256 $setUpHash"
            if (-not $setUpSame) {
                $stopReason = 'the key copy of the setUp run changed (D19)'
            }
        }
        if ($buttonsInputs) {
            $buttonsAfter = Get-InputHashes $buttonsInputs
            $buttonsChanged = @($buttonsInputs | Where-Object { $buttonsAfter[$_] -ne $buttonsBefore[$_] } | ForEach-Object { Split-Path $_ -Leaf })
            "Real inputs of the buttons run: $($buttonsInputs.Count), unchanged $($buttonsInputs.Count - $buttonsChanged.Count)"
            if ($buttonsChanged.Count -gt 0) {
                $stopReason = 'a real input of the buttons run changed (D19): ' + ($buttonsChanged -join ', ')
            }
        }
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
elseif ($round -and $round.kind -eq 'addinRun') {
    $result = Join-Path $docs ('testhost_' + $Code + '_addinrun.json')
    if (Test-Path $result) {
        $run = Get-Content -Raw $result | ConvertFrom-Json
        "Add-in run: held $($run.held), not held $($run.notHeld), not measured $($run.notMeasured), result $result"
        @($run.checks) | Select-Object item, name, held, detail | Format-Table -Wrap -AutoSize | Out-String -Width 250
    }
    else {
        "The add-in run wrote no result at $result"
    }
    'Files of the run folder:'
    Get-ChildItem $runFolder | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 200
}
elseif ($round) {
    'Files of the work folder:'
    Get-ChildItem $work | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 200
}
elseif (-not $DryStart) {
    'TEST copies next to the model:'
    Get-ChildItem (Split-Path $original) | Where-Object { $_.Name -like ($base + '_TEST-*') } | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 200
}

# R200. The lock wrapper hands this exit code on, so a job whose log result is not ok, or a window that was stopped, ends with 1
if (-not $DryStart -and ($stopReason -or $logResult -ne 'ok')) {
    exit 1
}
# Lane B 5b. A probe ends as a job does, and a dry start that froze ends with 1 too
if (($probe -and ($stopReason -or $logResult -ne 'ok')) -or ($stopReason -like 'freeze guard: *')) {
    exit 1
}
