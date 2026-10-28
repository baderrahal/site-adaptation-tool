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
