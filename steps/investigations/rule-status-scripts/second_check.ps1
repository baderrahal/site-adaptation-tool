# Round AUDIT-RULES. The second check of steps/investigations/rule-status-audit.md by another tool than audit.py, run from the repo root:
# pwsh -NoProfile -File steps/investigations/rule-status-scripts/second_check.ps1
$ErrorActionPreference = 'Stop'
$audit = Get-Content 'steps/investigations/rule-status-audit.md' -Encoding utf8
$ids = (Select-String -Path 'docs/rules/index.md' -Pattern '^- R\d+ ').Count
"rule ids in the index: $ids"
$main = ($audit | Where-Object { $_ -match '^\| R\d+ \|' -and $_ -notmatch 'process, no value' }).Count
$proc = ($audit | Where-Object { $_ -match '^\| R\d+ \|.*process, no value' }).Count
"rule rows with a value: $main, process: $proc, sum: $($main + $proc)"

$rules = Get-Content 'rules/roshn-makkah.json' -Raw -Encoding utf8 | ConvertFrom-Json
$shares = @{}
foreach ($p in $rules.levelProfiles.parsons2024Rev6.keyRules.PSObject.Properties) {
    if ($p.Value -is [psobject] -and $null -ne $p.Value.PSObject.Properties['share']) { $shares[$p.Name] = $p.Value.share }
}
foreach ($h in 'villa', 'alone', 'cluster') { $shares["hands.$h"] = $rules.hands.$h.share }
"keyRules and hands entries with a share: $($shares.Count)"

$sec5 = $false
$sum = @{}
$measuredRows = @()
foreach ($line in $audit) {
    if ($line -match '^## 5 ') { $sec5 = $true }
    elseif ($line -match '^## 6 ') { $sec5 = $false }
    if ($sec5 -and $line -match '^\| (.+?) \| (\d+) \| (value|host) \| ([^|]+?) \| ([^|]*?) \|') {
        $sum[$Matches[4]] += [int]$Matches[2]
        if ($Matches[4] -eq 'MEASURED') { $measuredRows += , @($Matches[1], $Matches[5]) }
    }
}
foreach ($k in $sum.Keys | Sort-Object) { "section 5 $k $($sum[$k])" }
$bad = 0
foreach ($row in $measuredRows) {
    $name = if ($row[0] -match 'keyRules\.(.+)\.rule') { $Matches[1] } elseif ($row[0] -match '^hands\.(\w+)\.rule') { "hands." + $Matches[1] } else { $null }
    $share = [double]($row[1] -replace '^share ([0-9.]+).*$', '$1')
    if ($null -eq $name -or -not $shares.ContainsKey($name) -or [double]$shares[$name] -ne $share) { $bad++
        "mismatch $($row[0]) $($row[1])" }
}
"MEASURED rows $($measuredRows.Count), shares that differ from the JSON: $bad"

# Round ADDIN 4g1 (R401). The block status of the rules profile recounted from the JSON as written, with no line of python/rule_status.py
$profilePath = (Get-Content 'config/project.json' -Raw -Encoding utf8 | ConvertFrom-Json).profile
$all = Get-Content $profilePath -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
$block = $all['status']
$byStatus = [ordered]@{}
$entries = 0
$shareBad = 0
$withPage = 0
$pageUnknown = 0
foreach ($file in ($block.Keys | Where-Object { $_ -ne 'note' } | Sort-Object)) {
    "block entries of ${file}: $($block[$file].Count)"
    foreach ($e in $block[$file].Values) {
        $entries++
        $byStatus[$e['status']] += 1
        if ($e['status'] -eq 'MEASURED') {
            $node = $all
            foreach ($part in $e['measuredBy'].Split('.')) { $node = $node[$part] }
            if ([double]$node['share'] -ne [double]$e['share']) { $shareBad++ }
        }
        if ($e['status'] -eq 'DOCUMENT') { if ($e['pageOrSheet'] -eq 'UNKNOWN') { $pageUnknown++ } else { $withPage++ } }
    }
}
foreach ($k in $byStatus.Keys | Sort-Object) { "block $k $($byStatus[$k])" }
"block entries $entries, MEASURED shares that differ from the entry they name: $shareBad, DOCUMENT with a page or sheet $withPage, page UNKNOWN $pageUnknown"
