# Writes the work root of this machine into %APPDATA%\SiteAdaptation\machine.json, the file WorkRoot.cs, TestHostPaths.ps1,
# project_profile.py, scenario_tools.py and the lane hooks read. Run once per PC, and again to move the work root.
# The last folder must be named SiteAdaptation, WorkRoot.Of refuses any other name since the test host keeps its files in a folder of
# that name, so the work root of a PC whose work folder is C:\SA-work is C:\SA-work\SiteAdaptation.
# -AppData lets a test write into a temporary folder.
param(
    [string]$WorkRoot = 'C:\SA-work\SiteAdaptation',
    [string]$AppData = $env:APPDATA
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\.claude\hooks\lane.ps1')

function Stop-Refused([string]$Why) {
    Write-Output "REFUSED $Why"
    exit 1
}

if (-not $AppData) { Stop-Refused 'no APPDATA folder is known' }
# As TestHostGuard.IsPlainFullPath: a drive path, no parent step, no forward slash, and the path Windows would open is the path given
$plain = $WorkRoot -match '^[A-Za-z]:\\' -and @($WorkRoot -split '\\') -notcontains '..' -and $WorkRoot.IndexOf('/') -lt 0 -and
    $WorkRoot.IndexOf(':', 2) -lt 0 -and [IO.Path]::GetFullPath($WorkRoot) -ceq $WorkRoot
if (-not $plain) { Stop-Refused "the work root is not a plain full path: $WorkRoot" }
if ((Split-Path $WorkRoot -Leaf) -cne 'SiteAdaptation') { Stop-Refused "the last folder of the work root must be named SiteAdaptation: $WorkRoot" }
# A run writes only under the work root, so the work root never lies in a folder of real inputs
foreach ($inputRoot in Get-InputRoots (Get-RepoRoot $PSScriptRoot)) {
    if (Test-InsideFolder $WorkRoot $inputRoot) { Stop-Refused "the work root lies under the input root ${inputRoot}: $WorkRoot" }
}

New-Item -ItemType Directory -Force $WorkRoot | Out-Null
$folder = Join-Path $AppData 'SiteAdaptation'
New-Item -ItemType Directory -Force $folder | Out-Null
$machine = Join-Path $folder 'machine.json'
# Other keys of a machine file that is already there are kept
$values = [ordered]@{}
if (Test-Path -LiteralPath $machine) {
    $old = Get-Content -Raw -LiteralPath $machine | ConvertFrom-Json
    foreach ($property in $old.PSObject.Properties) { $values[$property.Name] = $property.Value }
}
$values['workRoot'] = $WorkRoot
[IO.File]::WriteAllText($machine, (($values | ConvertTo-Json -Depth 10) + "`n"), (New-Object Text.UTF8Encoding $false))
Write-Output "WROTE $machine, workRoot $WorkRoot"
exit 0
