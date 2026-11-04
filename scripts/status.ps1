# Writes steps/STATUS.md in the main repo folder from git branches, open PRs and check results. Lanes never edit it by hand.
# The file is git ignored, one copy for all lanes, so a status change never dirties a tree or needs a commit.
# -Note adds a line such as "merging setup-lanes now" under Notes. The Waiting for you lines of the Notification hook are kept.
param([string]$Note = '')

. (Join-Path $PSScriptRoot '..\.claude\hooks\lane.ps1')

$root = Get-RepoRoot (Get-Location).Path
if (-not $root) { $root = Get-RepoRoot $PSScriptRoot }
$status = Get-StatusPath $root
$time = Get-Date -Format 'yyyy-MM-dd HH:mm'

function Get-Section([string[]]$Lines, [string]$Heading) {
    $keep = @()
    $inside = $false
    foreach ($line in $Lines) {
        if ($line -eq $Heading) {
            $inside = $true
            continue
        }
        if ($inside -and $line -like '## *') { break }
        if ($inside -and $line) { $keep += $line }
    }
    return $keep
}

$old = @()
if (Test-Path -LiteralPath $status) { $old = @(Get-Content -LiteralPath $status -Encoding UTF8) }
$waiting = @(Get-Section $old '## Waiting for you')
$notes = @(Get-Section $old '## Notes')
if ($Note) { $notes = @("- $time $(Get-LaneName $root): $Note") + $notes }
$notes = @($notes | Select-Object -First 5)

& git -C $root fetch --quiet --prune origin 2>$null
if ($LASTEXITCODE -ne 0) { $fetchLine = "git fetch failed, branches below may be old." } else { $fetchLine = '' }

$prLines = @()
# A shell script named gh may sit first on the path and PowerShell cannot run it, so take the program itself
$gh = Get-Command gh -CommandType Application -ErrorAction SilentlyContinue | Where-Object { $_.Extension -eq '.exe' } | Select-Object -First 1
$raw = $null
if ($gh) {
    Push-Location $root
    $raw = & $gh.Source pr list --state open --json number,title,headRefName,isDraft,statusCheckRollup,url 2>$null
    $ghExit = $LASTEXITCODE
    Pop-Location
}
if (-not $gh -or $ghExit -ne 0 -or -not $raw) {
    $prLines = @('- UNKNOWN, gh pr list did not answer.')
}
else {
    # Windows PowerShell hands a JSON list on as one object, so each item is taken out on its own
    $prs = @(($raw -join "`n") | ConvertFrom-Json | ForEach-Object { $_ })
    if ($prs.Count -eq 0) { $prLines = @('- none') }
    foreach ($pr in $prs) {
        $checks = @($pr.statusCheckRollup)
        $failed = @($checks | Where-Object { $_.conclusion -in @('FAILURE', 'CANCELLED', 'TIMED_OUT') }).Count
        $running = @($checks | Where-Object { $_.status -and $_.status -ne 'COMPLETED' }).Count
        $passed = @($checks | Where-Object { $_.conclusion -eq 'SUCCESS' }).Count
        $skipped = @($checks | Where-Object { $_.conclusion -in @('SKIPPED', 'NEUTRAL') }).Count
        $draft = ''
        if ($pr.isDraft) { $draft = ', draft' }
        $prLines += "- #$($pr.number) $($pr.headRefName)$draft, checks: $passed passed, $failed failed, $running running, $skipped skipped. $($pr.title)"
    }
}

$branchLines = @(& git -C $root for-each-ref --sort=-committerdate --format='- %(refname:short), %(committerdate:short), %(subject)' refs/remotes/origin 2>$null |
    Where-Object { $_ -notmatch '^- origin(/HEAD|/main)?,' } | Select-Object -First 15)
$head = & git -C $root rev-parse --short origin/main 2>$null

$out = @('# Status', '', "Written by scripts/status.ps1 at $time, never by hand. Main is at $head.")
if ($fetchLine) { $out += $fetchLine }
$out += @('', '## Waiting for you')
$out += $waiting
$out += @('', '## Notes')
$out += $notes
$out += @('', '## Open PRs')
$out += $prLines
$out += @('', '## Branches on origin, newest 15')
$out += $branchLines

# Line feeds only, the steps files hold no carriage return (R343)
[IO.File]::WriteAllText($status, (($out -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))
Write-Output "Wrote $status"
