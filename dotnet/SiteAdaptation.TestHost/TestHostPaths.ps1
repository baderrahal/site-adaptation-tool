# The paths the run script takes from this machine. Dot-sourced by Run-TestHost.ps1 and by the tests, it touches no file but the
# repo manifests it reads.

# R108. The four add-ins of this tool, each by its repo manifest and the DLL its install script puts under the add-ins root. The
# watcher clicks Load Once only on a box that names one of these DLLs, and stops Revit on a box that looks like the tool any other way
$ToolManifests = @(
    'dotnet\RevitPrototypeReader\SiteAdaptation.addin'
    'dotnet\SiteAdaptation.Placer\SiteAdaptationPlacer_Revit2024.addin'
    'dotnet\SiteAdaptation.TestHost\SiteAdaptationTestHost_Revit2024.addin'
    'dotnet\SiteAdaptation.AddIn\SiteAdaptationAddIn_Revit2024.addin'
)
# The DLL of each manifest above, in its order, under the add-ins root
$ToolDlls = @(
    'SiteAdaptation\RevitPrototypeReader.dll'
    'SiteAdaptationPlacer\SiteAdaptation.Placer.dll'
    'SiteAdaptationTestHost\SiteAdaptation.TestHost.dll'
    'SiteAdaptationAddIn\SiteAdaptation.AddIn.dll'
)

function Get-ToolAddIns([string]$Repo, [string]$Addins) {
    $ids = @()
    $marks = @()
    $paths = @()
    foreach ($at in 0..($ToolManifests.Count - 1)) {
        [xml]$xml = Get-Content -Raw (Join-Path $Repo $ToolManifests[$at])
        $ids += [string]$xml.RevitAddIns.AddIn.AddInId
        $marks += [string]$xml.RevitAddIns.AddIn.Name
        $marks += Split-Path ([string]$xml.RevitAddIns.AddIn.Assembly) -Leaf
        $paths += Join-Path $Addins $ToolDlls[$at]
    }
    [pscustomobject]@{
        manifests = $ToolManifests
        ids       = $ids
        marks     = $marks
        paths     = $paths
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
