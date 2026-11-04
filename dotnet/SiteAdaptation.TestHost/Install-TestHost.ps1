# Builds the solution and installs the reader, the placer and the test host from that one build (R103).
# Each add-in sits in its own folder under the add-ins root. SETUP-sign: no manifest is written here. Revit asks once for each manifest whose
# DLL is unsigned, so the three manifests exist for a run only, Get-ToolAddIns writes them and Run-TestHost.ps1 removes them after the run.
# What an older install left of them is removed here. Revit must be closed, it holds the DLLs open.
# -SkipSigningReport is kept so the callers that pass it still work, the script reads no registry key and has nothing to report.
param(
    [switch]$NoBuild,
    [switch]$SkipSigningReport
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$addins = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024'

if (Get-Process -Name Revit -ErrorAction SilentlyContinue) {
    throw 'Revit is running. Close it before the install.'
}
if (-not $NoBuild) {
    & dotnet build (Join-Path $repo 'dotnet\SiteAdaptation.sln') -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'The build failed, nothing was installed.'
    }
}

function Install-Folder($source, $target, $files) {
    New-Item -ItemType Directory -Force $target | Out-Null
    foreach ($name in $files) {
        Copy-Item -Path (Join-Path $source $name) -Destination $target -Force
    }
}

$reader = Join-Path $repo 'dotnet\RevitPrototypeReader\bin\Release\net48'
$placer = Join-Path $repo 'dotnet\SiteAdaptation.Placer\bin\Release\net48'
$host_ = Join-Path $repo 'dotnet\SiteAdaptation.TestHost\bin\Release\net48'
$readerFolder = Join-Path $addins 'SiteAdaptation'
$placerFolder = Join-Path $addins 'SiteAdaptationPlacer'
$hostFolder = Join-Path $addins 'SiteAdaptationTestHost'

Install-Folder $reader $readerFolder @('RevitPrototypeReader.dll', 'RevitPrototypeReader.pdb', 'SiteAdaptation.Common.dll', 'SiteAdaptation.Common.pdb', 'Clipper2Lib.dll', 'Newtonsoft.Json.dll')
Install-Folder $placer $placerFolder @('SiteAdaptation.Placer.dll', 'SiteAdaptation.Placer.pdb', 'SiteAdaptation.Common.dll', 'SiteAdaptation.Common.pdb', 'Clipper2Lib.dll', 'Newtonsoft.Json.dll')
# The test host folder holds the test host only. It loads the placer, the reader and Common from their own folders
Install-Folder $host_ $hostFolder @('SiteAdaptation.TestHost.dll', 'SiteAdaptation.TestHost.pdb')
# The dialog watcher of R108 is a program and not an add-in. Revit reads manifests in the Addins root only, so this folder is never loaded
$watcher = Join-Path $repo 'dotnet\SiteAdaptation.DialogWatcher\bin\Release\net48'
$watcherFolder = Join-Path $hostFolder 'Watcher'
Install-Folder $watcher $watcherFolder @('SiteAdaptation.DialogWatcher.exe', 'SiteAdaptation.DialogWatcher.exe.config', 'SiteAdaptation.DialogWatcher.pdb', 'SiteAdaptation.Common.dll', 'Newtonsoft.Json.dll')

# Revit loads one SiteAdaptation.Common.dll for every add-in of the tool, so every folder of the tool under the add-ins root, the
# SiteAdaptationAddIn of Install-SiteAdaptation.ps1 too, and the reader build must hold the Common of this build (R370)
. (Join-Path $repo 'dotnet\SiteAdaptation.AddIn\InstallChecks.ps1')
$removed = @(Remove-DevManifests $addins)
"Manifests of developer add-ins taken away: $(if ($removed.Count) { $removed -join ', ' } else { 'none were there' }), a run writes them for its own time"
$built = (Get-FileHash (Join-Path $placer 'SiteAdaptation.Common.dll')).Hash
$stale = @(Get-StaleCommons $addins $built)
$same = $stale.Count -eq 0 -and (Get-FileHash (Join-Path $reader 'SiteAdaptation.Common.dll')).Hash -eq $built

foreach ($folder in @($readerFolder, $placerFolder, $hostFolder, $watcherFolder)) {
    $leaf = Split-Path $folder -Leaf
    Get-ChildItem $folder -File | Where-Object { $_.Extension -in '.dll', '.exe' } | Select-Object @{
        n = 'Folder'
        e = { $leaf }
    }, Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 200 | Out-Host
}
"SiteAdaptation.Common.dll the same in every folder of the tool and the build: $same"
if (-not $same) {
    throw ('The Common DLLs differ, run the install of each add-in again with Revit closed. Not this build: ' + ($stale -join ', '))
}
