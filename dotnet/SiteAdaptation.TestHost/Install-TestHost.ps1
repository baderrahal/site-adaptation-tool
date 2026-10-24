# Builds the solution and installs the reader, the placer and the test host from that one build (R103).
# Each add-in sits in its own folder with its own manifest that names the full DLL path.
# Revit must be closed, it holds the DLLs open. -SkipSigningReport leaves the add-in security answers unread.
param(
    [switch]$NoBuild,
    [switch]$SkipSigningReport
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$addins = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024'
$signing = 'HKCU:\Software\Autodesk\Revit\Autodesk Revit 2024\CodeSigning'

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

# A manifest is written with the DLL path of this machine, the rest of it comes from the repo.
# One that already names this DLL and this AddInId is left as it is
function Install-Manifest($repoManifest, $manifestName, $dll) {
    [xml]$xml = Get-Content -Raw $repoManifest
    $xml.RevitAddIns.AddIn.Assembly = $dll
    $id = $xml.RevitAddIns.AddIn.AddInId
    $path = Join-Path $addins $manifestName
    $state = 'written'
    if (Test-Path $path) {
        [xml]$there = Get-Content -Raw $path
        if ($there.RevitAddIns.AddIn.Assembly -eq $dll -and $there.RevitAddIns.AddIn.AddInId -eq $id) {
            $state = 'kept, it already names this DLL'
        }
    }
    if ($state -eq 'written') {
        $xml.Save($path)
    }
    return [pscustomobject]@{
        Manifest = $manifestName
        State    = $state
        Assembly = $dll
        AddInId  = $id
    }
}

$reader = Join-Path $repo 'dotnet\RevitPrototypeReader\bin\Release\net48'
$placer = Join-Path $repo 'dotnet\SiteAdaptation.Placer\bin\Release\net48'
$host_ = Join-Path $repo 'dotnet\SiteAdaptation.TestHost\bin\Release\net48'
$readerFolder = Join-Path $addins 'SiteAdaptation'
$placerFolder = Join-Path $addins 'SiteAdaptationPlacer'
$hostFolder = Join-Path $addins 'SiteAdaptationTestHost'

Install-Folder $reader $readerFolder @('RevitPrototypeReader.dll', 'RevitPrototypeReader.pdb', 'SiteAdaptation.Common.dll', 'SiteAdaptation.Common.pdb', 'Newtonsoft.Json.dll')
Install-Folder $placer $placerFolder @('SiteAdaptation.Placer.dll', 'SiteAdaptation.Placer.pdb', 'SiteAdaptation.Common.dll', 'SiteAdaptation.Common.pdb', 'Newtonsoft.Json.dll')
# The test host folder holds the test host only. It loads the placer, the reader and Common from their own folders
Install-Folder $host_ $hostFolder @('SiteAdaptation.TestHost.dll', 'SiteAdaptation.TestHost.pdb')
# The dialog watcher of R108 is a program and not an add-in. Revit reads manifests in the Addins root only, so this folder is never loaded
$watcher = Join-Path $repo 'dotnet\SiteAdaptation.DialogWatcher\bin\Release\net48'
$watcherFolder = Join-Path $hostFolder 'Watcher'
Install-Folder $watcher $watcherFolder @('SiteAdaptation.DialogWatcher.exe', 'SiteAdaptation.DialogWatcher.exe.config', 'SiteAdaptation.DialogWatcher.pdb', 'SiteAdaptation.Common.dll', 'Newtonsoft.Json.dll')

$manifests = @(
    (Install-Manifest (Join-Path $repo 'dotnet\RevitPrototypeReader\SiteAdaptation.addin') 'SiteAdaptation_Revit2024.addin' (Join-Path $readerFolder 'RevitPrototypeReader.dll'))
    (Install-Manifest (Join-Path $repo 'dotnet\SiteAdaptation.Placer\SiteAdaptationPlacer_Revit2024.addin') 'SiteAdaptationPlacer_Revit2024.addin' (Join-Path $placerFolder 'SiteAdaptation.Placer.dll'))
    (Install-Manifest (Join-Path $repo 'dotnet\SiteAdaptation.TestHost\SiteAdaptationTestHost_Revit2024.addin') 'SiteAdaptationTestHost_Revit2024.addin' (Join-Path $hostFolder 'SiteAdaptation.TestHost.dll'))
)

# Revit loads one SiteAdaptation.Common.dll for all three, so all must come from this build
$built = (Get-FileHash (Join-Path $placer 'SiteAdaptation.Common.dll')).Hash
$hashes = @((Get-FileHash (Join-Path $readerFolder 'SiteAdaptation.Common.dll')).Hash, (Get-FileHash (Join-Path $placerFolder 'SiteAdaptation.Common.dll')).Hash, (Get-FileHash (Join-Path $watcherFolder 'SiteAdaptation.Common.dll')).Hash, (Get-FileHash (Join-Path $reader 'SiteAdaptation.Common.dll')).Hash)
$same = @($hashes | Where-Object { $_ -ne $built }).Count -eq 0

# Revit asks once per add-in with no signature whether to load it, and keeps the answer under CodeSigning.
# The install only reports that answer and never writes the key. Bader answers the question in Revit
foreach ($entry in $manifests) {
    $id = [string]$entry.AddInId
    $now = $null
    if ($SkipSigningReport) {
        $entry | Add-Member -NotePropertyName CodeSigning -NotePropertyValue 'not read'
        continue
    }
    if (Test-Path $signing) {
        $now = (Get-ItemProperty -Path $signing -ErrorAction SilentlyContinue).$id
    }
    $state = if ($now -eq 1) { 'Always Load is set' } else { 'no answer kept, Revit asks at its next start' }
    $entry | Add-Member -NotePropertyName CodeSigning -NotePropertyValue $state
}

$manifests | Format-Table -AutoSize | Out-String -Width 250 | Out-Host
foreach ($folder in @($readerFolder, $placerFolder, $hostFolder, $watcherFolder)) {
    $leaf = Split-Path $folder -Leaf
    Get-ChildItem $folder -File | Where-Object { $_.Extension -in '.dll', '.exe' } | Select-Object @{
        n = 'Folder'
        e = { $leaf }
    }, Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 200 | Out-Host
}
"SiteAdaptation.Common.dll the same in the reader, the placer, the watcher and the build: $same"
if (-not $same) {
    throw 'The Common DLLs differ, run the install again with Revit closed.'
}
