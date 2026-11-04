#Requires -Version 7
# R370, docs/design/08-install.md. Installs the add-in for this user only. Every path lies under the add-ins root of %APPDATA%, so no
# admin right is needed, and the script reads and writes no registry key (R106). Revit 2024 must be closed, it holds the DLLs open.
# Revit 2023 and 2025 may run, they load no add-in of this folder.
# -AddinsRoot, -AddInBuild, -WorkerBuild and -RevitProcesses let a test run it against a temporary folder and planted builds
param(
    [switch]$NoBuild,
    [string]$AddinsRoot = (Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024'),
    [string]$AddInBuild = '',
    [string]$WorkerBuild = '',
    [scriptblock]$RevitProcesses = { Get-RevitProcesses }
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'InstallChecks.ps1')
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $AddInBuild) {
    $AddInBuild = Join-Path $repo 'dotnet\SiteAdaptation.AddIn\bin\Release\net48'
}
if (-not $WorkerBuild) {
    $WorkerBuild = Join-Path $repo 'dotnet\CadReader\bin\Release\net48'
}
$common = 'SiteAdaptation.Common.dll'

function Assert-Revit2024Closed {
    if (@(Select-Revit2024 (& $RevitProcesses)).Count -gt 0) {
        throw 'Revit 2024 is running. Close it before the install.'
    }
}

Assert-Revit2024Closed
if (-not $NoBuild) {
    & dotnet build (Join-Path $repo 'dotnet\SiteAdaptation.sln') -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'The build failed, nothing was installed.'
    }
}
# Revit may have started during the build, so the check runs again right before the first copy
Assert-Revit2024Closed

$addInFolder = Join-Path $AddinsRoot 'SiteAdaptationAddIn'
$workerFolder = Join-Path $addInFolder 'Worker'

function Copy-Files($source, $target, $files) {
    New-Item -ItemType Directory -Force $target | Out-Null
    foreach ($file in $files) {
        Copy-Item -LiteralPath $file -Destination $target -Force
    }
}

# The add-in folder takes the add-in, Common and the clipper Common needs (R475) by name and nothing else. Revit serves its own Newtonsoft.Json by the redirect of
# Revit.exe.config, and the resolver of the add-in would serve a copy of this folder to the tool (steps/lane-b.md, 5a PR 2 review)
$addInFiles = @('SiteAdaptation.AddIn.dll', 'SiteAdaptation.AddIn.pdb', $common, 'SiteAdaptation.Common.pdb', 'Clipper2Lib.dll') |
    ForEach-Object { Join-Path $AddInBuild $_ } |
    Where-Object { $_ -like '*.dll' -or (Test-Path -LiteralPath $_) }
$addInFiles += Join-Path $AddInBuild 'Theme\Fonts\OFL.txt'
Copy-Files $AddInBuild $addInFolder $addInFiles

# The worker runs as its own process with the redirects of its exe.config, in a folder Revit does not load, so it takes every
# library of its build
$workerFiles = Get-ChildItem -LiteralPath $WorkerBuild -File |
    Where-Object { $_.Name -in 'CadReader.exe', 'CadReader.exe.config' -or $_.Extension -in '.dll', '.pdb' } |
    ForEach-Object { $_.FullName }
Copy-Files $WorkerBuild $workerFolder $workerFiles

# R486, Q377. The set Empty project copies for a new project, in its layout under the repo root: the files the C# of a build reads by name. CheckRun.BesideConfig, the layer
# list of ReadInputs, the sheet of ParameterSheet and the rules profile, the last three by their keys in config. No override list (R487). Data files only in a subfolder, so
# Revit loads nothing from it, it loads the manifest and the assemblies that name
$setFolder = Join-Path $addInFolder 'ShippedSet'
$projectFile = Join-Path $repo 'config\project.json'
$project = Get-Content -Raw -LiteralPath $projectFile | ConvertFrom-Json -AsHashtable
$setFiles = @('config/project.json', 'config/floor_outlines.json', 'config/prototype_offsets.json', 'config/rule_values.json', "config/$($project.addin.readData.cadLayers)", $project.addin.parameterSheet, $project.profile)
if (Test-Path -LiteralPath $setFolder) {
    Remove-Item -LiteralPath $setFolder -Recurse -Force
}
foreach ($file in $setFiles) {
    $target = Join-Path $setFolder $file
    New-Item -ItemType Directory -Force (Split-Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath (Join-Path $repo $file) -Destination $target -Force
}

# The manifest takes its fixed AddInId and the rest from the repo, and the full path of the DLL of this machine
[xml]$manifest = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'SiteAdaptationAddIn_Revit2024.addin')
$dll = Join-Path $addInFolder 'SiteAdaptation.AddIn.dll'
$manifest.RevitAddIns.AddIn.Assembly = $dll
$manifestPath = Join-Path $AddinsRoot 'SiteAdaptationAddIn_Revit2024.addin'
$manifest.Save($manifestPath)

# SETUP-sign. Revit asks once for each manifest with an unsigned DLL, so the manifests of the reader, the placer and the test host an older
# install left here are removed and the tool stays one manifest, one entry DLL and so one question. A run of the test host writes them again
# for its own time and removes them after it (Get-ToolAddIns, Run-TestHost.ps1). Their DLL folders stay, Revit does not load a folder
$removedDev = @(Remove-DevManifests $AddinsRoot)

# Revit loads one Common for every add-in of the tool, the first it meets, so every folder of the tool gets the Common of this
# build. A Common a running worker holds open has already stopped the script at the Worker copy above. A copy that fails here,
# onto the Common of another add-in of the tool, is caught, and the check below names the file and stops the script
$built = (Get-FileHash -LiteralPath (Join-Path $AddInBuild $common)).Hash
$found = @(Get-ToolCommons $AddinsRoot)
$failed = @()
foreach ($file in $found) {
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -eq $built) {
        continue
    }
    try {
        Copy-Item -LiteralPath (Join-Path $AddInBuild $common) -Destination $file.FullName -Force
    }
    catch {
        $failed += "$($file.FullName): $($_.Exception.Message)"
    }
}
$differ = @(Get-StaleCommons $AddinsRoot $built)

Get-ChildItem -LiteralPath $addInFolder -File -Recurse | Select-Object @{
    n = 'Folder'
    e = { Split-Path $_.DirectoryName -Leaf }
}, Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 200 | Out-Host
"Manifest $manifestPath names $dll"
"Manifests of developer add-ins taken away: $(if ($removedDev.Count) { $removedDev -join ', ' } else { 'none were there' })"
"Manifests of this tool in ${AddinsRoot}: $((@(Get-ChildItem -LiteralPath $AddinsRoot -Filter 'SiteAdaptation*.addin' -File | ForEach-Object Name)) -join ', ')"
if ($differ.Count -gt 0) {
    $failed | ForEach-Object { "Not replaced: $_" } | Out-Host
    throw ('The Common DLLs differ, close the program that holds it and run the install again: ' + ($differ -join ', '))
}
"one SiteAdaptation.Common.dll in $($found.Count) folders of the tool, SHA-256 $built"
