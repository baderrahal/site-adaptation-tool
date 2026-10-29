# The paths the run script takes from this machine. Dot-sourced by Run-TestHost.ps1 and by the tests, it touches no file but the
# manifests it reads.

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

function Get-ToolAddIns([string]$Repo, [string]$Addins) {
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
