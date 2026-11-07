#Requires -Version 7
# Bader's order of 7 Nov 21:06. The stage 1 read of a new project folder, outside Revit, by the worker of the installed add-in, so Lead can
# fill steps/bader-form-8nov.md before anyone opens Revit. It copies no file of the folder and writes only under <work root>\intake.
# Two reads: the raw read, then a read with the proposed columns confirmed, as card 1 does today (F22 makes the second one go).
# The folder is hashed before and after, a change fails the run, as CLAUDE.md asks of every run on real inputs.
param(
    [Parameter(Mandatory = $true)][string]$Folder,
    [string]$WorkRoot = '',
    [string]$AddinFolder = (Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024\SiteAdaptationAddIn'),
    [string]$CoverScript = ''
)
$ErrorActionPreference = 'Stop'

function Get-Ledger([string]$Root) {
    $rows = @{}
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File) {
        $rows[$file.FullName] = '{0}|{1}|{2}' -f $file.Length, $file.LastWriteTimeUtc.Ticks, (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    return $rows
}

function Invoke-Read([string]$Name, [string]$Out, [object[]]$Inputs) {
    New-Item -ItemType Directory -Force -Path $Out | Out-Null
    $request = [ordered]@{
        formatVersion = 1
        runId = "intake_$Name"
        document = [ordered]@{
            path = (Join-Path $Out 'none.rvt')
            sessionId = 'intake-outside-revit'
        }
        command = 'dataCheck'
        inputs = $Inputs
        projectMode = 'rev6'
        scope = [ordered]@{ kind = 'project' }
        outputFolder = $Out
        timeLimitS = 3600.0
    }
    $requestPath = Join-Path (Split-Path -Parent $Out) "request_$Name.json"
    $request | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $requestPath -Encoding utf8NoBOM
    $clock = [Diagnostics.Stopwatch]::StartNew()
    & $worker --request $requestPath *> (Join-Path (Split-Path -Parent $Out) "worker_$Name.log")
    $code = $LASTEXITCODE
    $clock.Stop()
    return [pscustomobject]@{
        Name = $Name
        Code = $code
        Minutes = [math]::Round($clock.Elapsed.TotalMinutes, 1)
        Out = $Out
    }
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json -Depth 40
}

function Get-Value($Field) {
    if ($null -eq $Field) { return $null }
    if ($Field -is [pscustomobject] -and $Field.PSObject.Properties.Name -contains 'value') { return $Field.value }
    return $Field
}

$Folder = (Resolve-Path -LiteralPath $Folder).Path
# The work root as .claude/hooks/lane.ps1 Get-WorkRoot finds it, machine.json first, never the Documents folder OneDrive moves
if (-not $WorkRoot) {
    $machine = Join-Path $env:APPDATA 'SiteAdaptation\machine.json'
    if (Test-Path -LiteralPath $machine) { $WorkRoot = [string](Get-Content -LiteralPath $machine -Raw | ConvertFrom-Json).workRoot }
    if (-not $WorkRoot) { $WorkRoot = Join-Path $HOME 'Documents\SiteAdaptation' }
}
$worker = Join-Path $AddinFolder 'Worker\CadReader.exe'
$config = Join-Path $AddinFolder 'ShippedSet\config\project.json'
$sheet = Join-Path $AddinFolder 'ShippedSet\config\shared_parameters.xlsx'
foreach ($need in @($worker, $config)) { if (-not (Test-Path -LiteralPath $need)) { throw "not found: $need, install the add-in first" } }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$intake = Join-Path $WorkRoot 'intake'
$run = Join-Path $intake $stamp
New-Item -ItemType Directory -Force -Path $run | Out-Null
$started = Get-Date
$before = Get-Ledger $Folder

$base = @(
    [ordered]@{ role = 'config'
        path = $config
        sha256 = (Get-FileHash -LiteralPath $config -Algorithm SHA256).Hash.ToLowerInvariant() },
    [ordered]@{ role = 'scanFolder'
        path = $Folder
        sha256 = '' }
)
if (Test-Path -LiteralPath $sheet) { $base += [ordered]@{ role = 'parameterSheet'
        path = $sheet
        sha256 = (Get-FileHash -LiteralPath $sheet -Algorithm SHA256).Hash.ToLowerInvariant() } }

$raw = Invoke-Read 'raw' (Join-Path $run 'raw') $base
$proposal = Read-Json (Join-Path $raw.Out 'project_profile.json')
$second = $null
if ($proposal -and $proposal.typologyColumn.proposed -and $proposal.areaColumn.proposed) {
    # The proposal confirmed as it stands, what a person pressing Confirm with no change gives
    # A level the read could not name is named by the person in the Confirm window. Here it is left out, and level 1 takes the name of the
    # proposed area column when no word came, CHOSEN by Lead and said in the report
    $levels = @($proposal.levels | Where-Object { $_.word } | ForEach-Object { [ordered]@{ level = $_.level
                word = $_.word } })
    $levelNote = 'the level words of the read'
    if (-not ($levels | Where-Object { $_.level -eq 1 })) {
        $levels = @([ordered]@{ level = 1
                word = [string]$proposal.areaColumn.proposed.column }) + $levels
        $levelNote = "level 1 named after the area column $($proposal.areaColumn.proposed.column), the read gave no word"
    }
    $confirmed = [ordered]@{
        codeMap = @($proposal.codeMap.proposed | ForEach-Object { [ordered]@{ old = $_.old
                new = $_.new } })
        levels = @($levels)
        typologyColumn = [ordered]@{
            workbook = $proposal.typologyColumn.proposed.workbook
            sheet = $proposal.typologyColumn.proposed.sheet
            column = $proposal.typologyColumn.proposed.column
        }
        areaColumn = [ordered]@{
            workbook = $proposal.areaColumn.proposed.workbook
            sheet = $proposal.areaColumn.proposed.sheet
            column = $proposal.areaColumn.proposed.column
        }
    }
    $profilePath = Join-Path $run 'confirmed_profile.json'
    $confirmed | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $profilePath -Encoding utf8NoBOM
    $second = Invoke-Read 'confirmed' (Join-Path $run 'confirmed') ($base + [ordered]@{ role = 'projectProfile'
        path = $profilePath
        sha256 = (Get-FileHash -LiteralPath $profilePath -Algorithm SHA256).Hash.ToLowerInvariant() })
}

$after = Get-Ledger $Folder
$changed = @($before.Keys | Where-Object { $after[$_] -ne $before[$_] }) + @($after.Keys | Where-Object { -not $before.ContainsKey($_) })

$last = if ($second -and $second.Code -eq 0) { $second } else { $raw }
$check = Read-Json (Join-Path $raw.Out 'data_check.json')
$plots = Read-Json (Join-Path $last.Out 'plots.json')

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# Intake of a new project folder')
$lines.Add('')
$lines.Add("Written by scripts/new-project-intake.ps1 at $(Get-Date -Format 'yyyy-MM-dd HH:mm'). Folder: $Folder. Counts and names only.")
$lines.Add('')
$lines.Add('## Run')
$lines.Add('')
$lines.Add("- Raw read: exit $($raw.Code), $($raw.Minutes) min.")
if ($second) {
    $lines.Add("- Read with the proposed columns confirmed: exit $($second.Code), $($second.Minutes) min, $levelNote.")
    $answer = Read-Json (Join-Path $run 'answer_intake_confirmed.json')
    if ($second.Code -ne 0 -and $answer) { $lines.Add("  - The worker said: $($answer.message)") }
} else { $lines.Add('- Read with the proposed columns confirmed: not run, the raw read proposed no villa type or area column.') }
$lines.Add("- Total $([math]::Round(((Get-Date) - $started).TotalMinutes, 1)) min. Files in the folder: $($before.Count). Changed by the run: $($changed.Count).")
$lines.Add('')
$lines.Add('## What the read found, by need')
$lines.Add('')
$lines.Add('| Need | Mark | Why |')
$lines.Add('|---|---|---|')
foreach ($need in @($check.needs)) { $lines.Add("| $($need.need) | $($need.mark) | $(([string]$need.line) -replace '\|', '/') |") }
$lines.Add('')
$lines.Add('## Files by kind')
$lines.Add('')
$lines.Add('| Kind | Read | Not read |')
$lines.Add('|---|---|---|')
foreach ($group in @($check.files | Group-Object kind)) {
    $read = @($group.Group | Where-Object { $_.status -eq 'read' }).Count
    $lines.Add("| $($group.Name) | $read | $($group.Count - $read) |")
}
foreach ($file in @($check.files | Where-Object { $_.status -ne 'read' })) { $lines.Add("- Not read: $($file.name), $($file.reason)") }
$lines.Add('')
$lines.Add('## Proposed columns')
$lines.Add('')
if ($proposal) {
    $t = $proposal.typologyColumn.proposed
    $a = $proposal.areaColumn.proposed
    $lines.Add("- Villa type: workbook $($t.workbook), sheet $($t.sheet), column $($t.column), $($t.plots) plots, $($t.prototypePlots) of them with a type of a prototype file. Candidates: $(@($proposal.typologyColumn.candidates).Count).")
    $lines.Add("- Area: workbook $($a.workbook), sheet $($a.sheet), column $($a.column), $($a.plots) plots, $($a.areas) areas. Candidates: $(@($proposal.areaColumn.candidates).Count).")
}
else { $lines.Add('- No profile was written, see the worker log of the raw read.') }
$lines.Add('')
$lines.Add('## Neighbourhoods, from the read with the columns confirmed')
$lines.Add('')
$lines.Add('| Area | Plots with an outline | With a villa type |')
$lines.Add('|---|---|---|')
$byArea = @{}
foreach ($plot in @($plots.plots)) {
    $area = [string](Get-Value $plot.area)
    if (-not $byArea.ContainsKey($area)) { $byArea[$area] = @(0, 0) }
    $byArea[$area][0]++
    if (Get-Value $plot.villaType) { $byArea[$area][1]++ }
}
foreach ($key in ($byArea.Keys | Sort-Object { - $byArea[$_][1] })) { $lines.Add("| $key | $($byArea[$key][0]) | $($byArea[$key][1]) |") }
$lines.Add("- Plots listed apart, no villa goes on them: $(@($plots.otherPlots).Count).")
$lines.Add('')
$lines.Add('## Road surfaces')
$lines.Add('')
$xml = @(Get-ChildItem -LiteralPath $Folder -Recurse -File -Include *.xml)
$lines.Add("- LandXML files in the folder: $($xml.Count).")
foreach ($file in $xml) { $lines.Add("  - $($file.Name)") }
if ($CoverScript -and (Test-Path -LiteralPath $CoverScript)) {
    $cover = & (Join-Path (Split-Path -Parent $PSScriptRoot) '.venv\Scripts\python.exe') -I $CoverScript --folder $Folder --plots (Join-Path $last.Out 'plots.json') 2>&1
    $lines.Add('- Cover of the plots by each surface, from the cover script:')
    foreach ($line in $cover) { $lines.Add("  - $line") }
}
else { $lines.Add('- Cover of the plots by each surface: UNKNOWN, no cover script given.') }
$lines.Add('')
$lines.Add('## Folder check')
$lines.Add('')
if ($changed.Count -eq 0) { $lines.Add("- PASS: $($before.Count) files, name, size, write time and SHA-256 the same before and after.") }
else { $lines.Add("- FAIL: $($changed.Count) files changed during the run.")
    foreach ($path in $changed) { $lines.Add("  - $path") } }

$report = Join-Path $intake 'intake.md'
($lines -join "`n") + "`n" | Set-Content -LiteralPath $report -Encoding utf8NoBOM -NoNewline
Copy-Item -LiteralPath $report -Destination (Join-Path $run 'intake.md')
Write-Output "intake written: $report, $([math]::Round(((Get-Date) - $started).TotalMinutes, 1)) min, folder changed: $($changed.Count)"
if ($changed.Count -gt 0) { exit 1 }
