#Requires -Version 7
# R370, docs/design/08-install.md. Installs the add-in for this user only. Every path lies under the add-ins root of %APPDATA%, so no
# admin right is needed, and the script reads and writes no registry key (R106). Revit must be closed, it holds the DLLs open.
# -AddinsRoot, -AddInBuild, -WorkerBuild and -RevitRunning let a test run it against a temporary folder and planted builds
param(
    [switch]$NoBuild,
    [string]$AddinsRoot = (Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024'),
    [string]$AddInBuild = '',
    [string]$WorkerBuild = '',
    [scriptblock]$RevitRunning = { [bool](Get-Process -Name Revit -ErrorAction SilentlyContinue) }
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $AddInBuild) {
    $AddInBuild = Join-Path $repo 'dotnet\SiteAdaptation.AddIn\bin\Release\net48'
}
if (-not $WorkerBuild) {
    $WorkerBuild = Join-Path $repo 'dotnet\CadReader\bin\Release\net48'
}
$common = 'SiteAdaptation.Common.dll'

if (& $RevitRunning) {
    throw 'Revit is running. Close it before the install.'
}
if (-not $NoBuild) {
    & dotnet build (Join-Path $repo 'dotnet\SiteAdaptation.sln') -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'The build failed, nothing was installed.'
    }
}

$addInFolder = Join-Path $AddinsRoot 'SiteAdaptationAddIn'
$workerFolder = Join-Path $addInFolder 'Worker'

function Copy-Files($source, $target, $files) {
    New-Item -ItemType Directory -Force $target | Out-Null
    foreach ($file in $files) {
        Copy-Item -LiteralPath $file -Destination $target -Force
    }
}

# The add-in folder takes the add-in and Common by name and nothing else. Revit serves its own Newtonsoft.Json by the redirect of
# Revit.exe.config, and the resolver of the add-in would serve a copy of this folder to the tool (steps/lane-b.md, 5a PR 2 review)
$addInFiles = @('SiteAdaptation.AddIn.dll', 'SiteAdaptation.AddIn.pdb', $common, 'SiteAdaptation.Common.pdb') |
    ForEach-Object { Join-Path $AddInBuild $_ } |
    Where-Object { $_ -like '*.dll' -or (Test-Path -LiteralPath $_) }
$addInFiles += Join-Path $AddInBuild 'Theme\Fonts\OFL.txt'
Copy-Files $AddInBuild $addInFolder $addInFiles

# The worker runs as its own process with the redirects of its exe.config, in a folder Revit does not load, so it takes every
# library of its build. A config file of the repo stays out, the user picks config/project.json in the pane
$workerFiles = Get-ChildItem -LiteralPath $WorkerBuild -File |
    Where-Object { $_.Name -in 'CadReader.exe', 'CadReader.exe.config' -or $_.Extension -in '.dll', '.pdb' } |
    ForEach-Object { $_.FullName }
Copy-Files $WorkerBuild $workerFolder $workerFiles

# The manifest takes its fixed AddInId and the rest from the repo, and the full path of the DLL of this machine
[xml]$manifest = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'SiteAdaptationAddIn_Revit2024.addin')
$dll = Join-Path $addInFolder 'SiteAdaptation.AddIn.dll'
$manifest.RevitAddIns.AddIn.Assembly = $dll
$manifestPath = Join-Path $AddinsRoot 'SiteAdaptationAddIn_Revit2024.addin'
$manifest.Save($manifestPath)

# Revit loads one Common for every add-in of the tool, the first it meets, so every folder of the tool gets the Common of this
# build. A copy that fails, such as onto a Common a running worker holds open, is left to the check below
$built = (Get-FileHash -LiteralPath (Join-Path $AddInBuild $common)).Hash
$found = @(Get-ChildItem -LiteralPath $AddinsRoot -Directory | Where-Object { $_.Name -like 'SiteAdaptation*' } |
    ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Recurse -File -Filter $common })
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
$differ = @($found | Where-Object { (Get-FileHash -LiteralPath $_.FullName).Hash -ne $built } | ForEach-Object { $_.FullName })

Get-ChildItem -LiteralPath $addInFolder -File -Recurse | Select-Object @{
    n = 'Folder'
    e = { Split-Path $_.DirectoryName -Leaf }
}, Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 200 | Out-Host
"Manifest $manifestPath names $dll"
if ($differ.Count -gt 0) {
    $failed | ForEach-Object { "Not replaced: $_" } | Out-Host
    throw ('The Common DLLs differ, close the program that holds it and run the install again: ' + ($differ -join ', '))
}
"one SiteAdaptation.Common.dll in $($found.Count) folders of the tool, SHA-256 $built"
