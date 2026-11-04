# The paths the run script takes from this machine. Dot-sourced by Run-TestHost.ps1, Start-RevitListener.ps1 and the tests. It reads the
# manifests. SETUP-sign: Get-ToolAddIns also writes the three developer manifests and a stamp of the run for the length of that run, and
# Remove-DevManifestsWhenFree takes them away when no run holds a stamp, so they never stay behind for a plain start of Revit.

# R108. The four add-ins of this tool, each by the AddInId of its repo manifest. The name and the DLL come from the manifest of
# that id installed in the add-ins root, as Revit reads it. The watcher clicks Load Once only on a box that names one of these
# DLLs, and stops Revit on a box that looks like the tool any other way
$ToolManifests = @(
    'dotnet\RevitPrototypeReader\SiteAdaptation.addin'
    'dotnet\SiteAdaptation.Placer\SiteAdaptationPlacer_Revit2024.addin'
    'dotnet\SiteAdaptation.TestHost\SiteAdaptationTestHost_Revit2024.addin'
    'dotnet\SiteAdaptation.AddIn\SiteAdaptationAddIn_Revit2024.addin'
)

# Each add-in of one manifest file, with the Assembly as Revit takes it, a relative one under the folder of the manifest
function Read-AddInManifest([string]$Path) {
    [xml]$xml = Get-Content -Raw -LiteralPath $Path
    foreach ($addIn in @($xml.RevitAddIns.AddIn)) {
        $assembly = [string]$addIn.Assembly
        if ($assembly -and -not [IO.Path]::IsPathRooted($assembly)) {
            $assembly = Join-Path (Split-Path $Path) $assembly
        }
        [pscustomobject]@{
            id       = ([string]$addIn.AddInId).Trim().ToLowerInvariant()
            name     = [string]$addIn.Name
            assembly = $assembly
            manifest = $Path
        }
    }
}

# SETUP-sign. Install-DevManifests and Remove-DevManifests, shared with the install script of the add-in
. (Join-Path $PSScriptRoot '..\SiteAdaptation.AddIn\InstallChecks.ps1')

# The manifests of the reader, the placer and the test host exist for a run only, so the run or the listener that starts Revit calls this with a
# stamp folder. It adds a stamp of its own process, writes the manifests that are not there from the DLLs the install put in their folders, and
# reads them. If it throws after that, the stamp and the manifests go before the error leaves. The caller removes them with
# Remove-DevManifestsWhenFree when it ends, and on any error after this call. With no stamp folder it writes nothing and only reads
function Get-ToolAddIns([string]$Repo, [string]$Addins, [string]$StampFolder) {
    if (-not $StampFolder) {
        return Read-ToolAddIns $Repo $Addins
    }
    Add-DevManifestStamp $StampFolder $PID
    try {
        [void](Install-DevManifests $Repo $Addins)
        return Read-ToolAddIns $Repo $Addins
    }
    catch {
        [void](Remove-DevManifestsWhenFree $Addins $StampFolder $PID)
        throw
    }
}

# The four add-ins of the tool as the add-ins root holds them, a throw when one is not installed once
function Read-ToolAddIns([string]$Repo, [string]$Addins) {
    $installed = @(Get-ChildItem -LiteralPath $Addins -Filter '*.addin' -File -ErrorAction SilentlyContinue | ForEach-Object { Read-AddInManifest $_.FullName })
    $ids = @()
    $marks = @()
    $paths = @()
    $files = @()
    foreach ($manifest in $ToolManifests) {
        $id = @(Read-AddInManifest (Join-Path $Repo $manifest))[0].id
        $there = @($installed | Where-Object id -eq $id)
        if ($there.Count -ne 1) {
            throw "The add-in $id of $manifest is not installed once in $Addins, found $($there.Count). Run the install scripts first."
        }
        $ids += $id
        $marks += $there[0].name
        $marks += Split-Path $there[0].assembly -Leaf
        $paths += $there[0].assembly
        $files += $there[0].manifest
    }
    [pscustomobject]@{
        manifests = $ToolManifests
        installed = $files
        ids       = $ids
        marks     = $marks
        paths     = $paths
    }
}

# Revit.exe of the Revit 2024 install folder, found by its own version and not guessed
function Get-Revit2024Exe {
    $found = @(Get-ChildItem (Join-Path $env:ProgramFiles 'Autodesk') -Directory | ForEach-Object { Join-Path $_.FullName 'Revit.exe' } |
        Where-Object { Test-Path $_ } | Where-Object { $v = (Get-Item $_).VersionInfo
            $v.ProductName -eq 'Autodesk Revit' -and $v.ProductMajorPart -eq 24 })
    if ($found.Count -ne 1) {
        throw "Found $($found.Count) Revit.exe files of version 24, one is needed: $($found -join ', ')"
    }
    $found[0]
}

# Lane B 5b. A probe of one Revit fact runs through the test host as every Revit start does, so the watcher is up before Revit.
# Its manifest is given from the folder the run starts in and is read here, the code is the file name of the manifest
function Get-ProbeAddIn([string]$Manifest) {
    $full = (Resolve-Path -LiteralPath $Manifest).Path
    $addIn = @(Read-AddInManifest $full)
    if ($addIn.Count -ne 1 -or -not $addIn[0].id -or -not $addIn[0].assembly) {
        throw "The probe manifest $full holds no single add-in with an AddInId and an Assembly"
    }
    [pscustomobject]@{
        id       = $addIn[0].id
        name     = $addIn[0].name
        path     = $addIn[0].assembly
        manifest = $full
        code     = [IO.Path]::GetFileNameWithoutExtension($full)
    }
}

# R372, QL2. The work root of this machine as WorkRoot of Common reads it for the test host: workRoot of
# %APPDATA%\SiteAdaptation\machine.json, else SiteAdaptation in the Documents folder. A machine file that gives no plain full path
# is refused, and so is a root not named SiteAdaptation, the folder name the test host keeps its files in
function Get-WorkRoot([string]$AppData, [string]$MyDocuments) {
    $machine = Join-Path $AppData 'SiteAdaptation\machine.json'
    if (-not (Test-Path -LiteralPath $machine -PathType Leaf)) {
        return Join-Path $MyDocuments 'SiteAdaptation'
    }
    try {
        $file = Get-Content -Raw -LiteralPath $machine | ConvertFrom-Json -NoEnumerate
    }
    catch {
        throw "$machine is no JSON: $($_.Exception.Message)"
    }
    $root = if ($file -is [pscustomobject]) { $file.workRoot } else { $null }
    if (-not ($root -is [string]) -or [string]::IsNullOrWhiteSpace($root)) {
        throw "$machine gives no workRoot"
    }
    # TestHostGuard.IsPlainFullPath: a drive path or a share, no parent step, no colon after the drive, no slash, no \\?\ or \\.\
    # prefix, and the path Windows would open is the path given
    $plain = ($root -match '^[A-Za-z]:\\' -or ($root.StartsWith('\\') -and -not $root.StartsWith('\\?\') -and -not $root.StartsWith('\\.\'))) -and
        @($root -split '\\') -notcontains '..' -and $root.IndexOf(':', 2) -lt 0 -and $root.IndexOf('/') -lt 0 -and [IO.Path]::GetFullPath($root) -ceq $root
    if (-not $plain) {
        throw "$machine gives a workRoot that is not a plain full path: $root"
    }
    if ((Split-Path $root -Leaf) -cne 'SiteAdaptation' -or $root.EndsWith('\')) {
        throw "the work root of the test host is a plain full path to a folder named SiteAdaptation: $root"
    }
    return $root
}

# Lane B 5a, R213. The run folder of an addinRun job, made new under the work root, and the first file the job makes in it. A folder
# that is already there stops the run, so no file of an earlier run is ever overwritten
function New-AddinRunFolder([string]$Docs, [string]$Code, [string]$Stamp) {
    $folder = Join-Path $Docs ('addin_run\' + $Code + '_' + $Stamp)
    if (Test-Path -LiteralPath $folder) {
        throw "The run folder is already there: $folder"
    }
    New-Item -ItemType Directory $folder | Out-Null
    [pscustomobject]@{
        folder = $folder
        model  = Join-Path $folder ($Code + '-TEST-NEW.rvt')
    }
}

# Q215, D19. A run writes only under the work root, so a folder that is an input root or lies inside one stops the run. The full paths are
# compared in any case, as Windows matches names, and a folder that only starts with the same letters is a folder of its own
function Assert-OutsideInputRoots([string]$Folder, [string[]]$InputRoots) {
    $full = [IO.Path]::GetFullPath($Folder).TrimEnd('\')
    foreach ($root in @($InputRoots | Where-Object { $_ })) {
        $inputRoot = [IO.Path]::GetFullPath($root).TrimEnd('\')
        if ($full -ieq $inputRoot -or $full.StartsWith($inputRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "The folder lies under the input root $inputRoot, a run writes only under the work root (D19): $Folder"
        }
    }
}

# Q215 (Bader, 2026-10-31). The ARC prototypes are saved as Revit 2024 files once, into this folder under the work root, and the pane
# Prototypes row of a run names it. -NewUpgradeFolder makes it on the first upgrade
function Get-UpgradeFolder([string]$Docs, [string[]]$InputRoots) {
    $folder = Join-Path $Docs 'prototypes_2024'
    Assert-OutsideInputRoots -Folder $folder -InputRoots $InputRoots
    return $folder
}

# Q127, J5. A path of config under the work root starts with the mark {workRoot}, taken as WorkRoot.Resolve of Common takes it, so the
# run script reads the same file as the add-in. A path with no mark stays as it is
function Resolve-WorkRootPath([string]$Path, [string]$Docs) {
    $mark = '{workRoot}'
    if (-not $Path -or -not $Path.StartsWith($mark, [StringComparison]::Ordinal)) {
        return $Path
    }
    $at = $Docs
    foreach ($part in @($Path.Substring($mark.Length).Replace('\', '/').Split('/') | Where-Object { $_ })) {
        $at = Join-Path $at $part
    }
    return $at
}

# R497, as R458 for the step hash. Revit writes <model>_backup\<model>.slog when it opens a workshared prototype to make a mass, so the
# check of a run counts it only under an input root, where any write is still a change (D19)
function Test-RevitWorksharingLog([string]$Path, [string[]]$InputRoots) {
    $full = [IO.Path]::GetFullPath($Path)
    if ([IO.Path]::GetExtension($full) -ine '.slog' -or -not ([IO.Path]::GetFileName([IO.Path]::GetDirectoryName($full)) -ilike '*_backup')) {
        return $false
    }
    return -not (Test-UnderInputRoot -Path $full -InputRoots $InputRoots)
}

function Test-UnderInputRoot([string]$Path, [string[]]$InputRoots) {
    $full = [IO.Path]::GetFullPath($Path)
    foreach ($root in @($InputRoots | Where-Object { $_ })) {
        if ($full.StartsWith([IO.Path]::GetFullPath($root).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

function Get-RunInputRoots($Config) {
    return @(@($env:SITE_ADAPTATION_INPUT, [string]$Config.inputRoot) + @($Config.testHost.listener.inputRoots | ForEach-Object { [string]$_ }) | Where-Object { $_ })
}

# R497. The list of real inputs is made before the run, so the files a run added under an input root are looked for after it (D19).
# A run may add files outside one, as Revit does in the prototype folder of the work root
function Get-NewInputFiles([string[]]$Sources, [string[]]$Before, [string[]]$InputRoots) {
    $known = [Collections.Generic.HashSet[string]]::new([string[]]@($Before), [StringComparer]::OrdinalIgnoreCase)
    $new = foreach ($source in @($Sources | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Container) })) {
        Get-ChildItem -LiteralPath $source -File -Recurse | Where-Object { -not $known.Contains($_.FullName) -and (Test-UnderInputRoot -Path $_.FullName -InputRoots $InputRoots) } | ForEach-Object { $_.FullName }
    }
    return @($new | Sort-Object -Unique)
}

# Lane B 5c (R384). The real files a buttons run reads in place: each input of the pane settings, every file of a folder input, the
# template and the key copy. Each is held by its size, write time and SHA-256 before and after the run (D19)
function Get-ButtonsInputs($paths, $inputRoots) {
    $files = [Collections.Generic.List[string]]::new()
    foreach ($path in @($paths | Where-Object { $_ })) {
        if (Test-Path -LiteralPath $path -PathType Container) {
            Get-ChildItem -LiteralPath $path -File -Recurse | Where-Object { -not (Test-RevitWorksharingLog -Path $_.FullName -InputRoots $inputRoots) } | ForEach-Object { $files.Add($_.FullName) }
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

# SETUP-sign. A stamp per run says the developer manifests are in use. It is a file in the stamp folder under the work root named by the process
# id of the run, holding that id and the start time of the process, so the id of a process that ended and was given to another one holds nothing
function Add-DevManifestStamp([string]$StampFolder, [int]$ProcessId) {
    New-Item -ItemType Directory -Force $StampFolder | Out-Null
    $started = (Get-Process -Id $ProcessId).StartTime.ToUniversalTime().Ticks
    Set-Content -LiteralPath (Join-Path $StampFolder "$ProcessId.stamp") -Value "$ProcessId $started"
}

# True when the stamp names a process that still runs and is the one that wrote it
function Test-DevManifestStampLive([string]$Path) {
    $parts = @((Get-Content -Raw -LiteralPath $Path -ErrorAction SilentlyContinue) -split '\s+' | Where-Object { $_ })
    if ($parts.Count -ne 2) {
        return $false
    }
    $process = Get-Process -Id ([int]$parts[0]) -ErrorAction SilentlyContinue
    return [bool]($process -and $process.StartTime.ToUniversalTime().Ticks -eq [int64]$parts[1])
}

# Removes the stamp of this run, deletes the stamps of processes that are gone, and removes the developer manifests when no stamp of a live
# process is left. Revit reads the add-ins root once, at its start, so a Revit somebody opened by hand does not hold them, and a manifest
# taken away while it runs harms nothing. Returns what it did, as text
function Remove-DevManifestsWhenFree([string]$Addins, [string]$StampFolder, [int]$ProcessId) {
    Remove-Item -LiteralPath (Join-Path $StampFolder "$ProcessId.stamp") -ErrorAction SilentlyContinue
    $live = 0
    foreach ($stamp in @(Get-ChildItem -LiteralPath $StampFolder -Filter '*.stamp' -File -ErrorAction SilentlyContinue)) {
        if (Test-DevManifestStampLive $stamp.FullName) {
            $live++
        }
        else {
            Remove-Item -LiteralPath $stamp.FullName -ErrorAction SilentlyContinue
        }
    }
    if ($live -gt 0) {
        return "kept, $live other run(s) hold them"
    }
    $removed = @(Remove-DevManifests $Addins)
    if ($removed.Count -gt 0) {
        return 'removed ' + ($removed -join ', ')
    }
    return 'none were there'
}
