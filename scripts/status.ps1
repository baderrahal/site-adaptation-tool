# Writes steps/STATUS.md in the main repo folder from git branches, open PRs and check results. Lanes never edit it by hand.
# The file is git ignored, one copy for all lanes, so a status change never dirties a tree or needs a commit.
# -Note adds a line such as "merging setup-lanes now" under Notes. The Waiting for you lines of the Notification hook and the Blockers are kept.
# -Progress also writes steps/PROGRESS.md of this folder from git, PRs and checks, for a records PR. Its last line is a hash, a test fails on a hand edit.
param([string]$Note = '', [switch]$Progress)

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
$blockers = @(Get-Section $old '## Blockers')
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
if ($blockers.Count -gt 0) {
    $out += @('', '## Blockers')
    $out += $blockers
}
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

# Short lines of plain text, as R400 and R15 hold the records
function Limit-Line([string]$Text) {
    $plain = $Text.Replace([string][char]59, ',').Replace([string][char]0x2014, ',')
    if ($plain.Length -gt 190) { return $plain.Substring(0, 190) }
    return $plain
}

if ($Progress) {
    $state = "STATE WAITING, $time"
    $openPrs = @()
    if ($prs) { $openPrs = @($prs) }
    if ($openPrs.Count -gt 0) {
        $names = ($openPrs | ForEach-Object { $_.headRefName.Replace(',', ' ') }) -join ' and '
        $state = "STATE RUNNING, $names, $time"
    }
    $tasks = @()
    $taskFolder = Join-Path $root 'steps\tasks'
    if (Test-Path -LiteralPath $taskFolder) {
        foreach ($file in Get-ChildItem -LiteralPath $taskFolder -Filter '*.md' | Sort-Object Name) {
            if ($file.Name -ne 'README.md') { $tasks += Read-TaskFile $file.FullName }
        }
    }
    $merged = @()
    if ($gh) {
        Push-Location $root
        $mergedRaw = & $gh.Source pr list --state merged --limit 8 --json number,title,mergedAt 2>$null
        Pop-Location
        if ($mergedRaw) { $merged = @(($mergedRaw -join "`n") | ConvertFrom-Json | ForEach-Object { $_ }) }
    }
    $questions = 0
    foreach ($name in @('steps\questions-open.md', 'steps\history\questions-open-older.md')) {
        $path = Join-Path $root $name
        if (Test-Path -LiteralPath $path) { $questions += @(Select-String -LiteralPath $path -Pattern '^Q\d{3}\. ').Count }
    }
    $body = @($state, '# Progress', "Written by scripts/status.ps1 -Progress at $time from git, PRs and checks, never by hand. Main is at $head.", '', '## State', '')
    $body += @($tasks | ForEach-Object { Limit-Line "- Task $($_.id), lane $($_.lane), status $($_.status)" })
    $body += @('', '## Done', '')
    $body += @($merged | ForEach-Object { Limit-Line "- PR $($_.number), merged $($_.mergedAt.ToString().Substring(0, 10)): $($_.title)" })
    $body += @('', '## Open bugs', '')
    $body += @($openPrs | Where-Object { @($_.statusCheckRollup | Where-Object { $_.conclusion -in @('FAILURE', 'CANCELLED', 'TIMED_OUT') }).Count -gt 0 } | ForEach-Object { Limit-Line "- PR $($_.number) has a failed check: $($_.title)" })
    $body += @('- Known bugs and leftovers: steps/backlog.md, steps/latent.md', '', "## Bader's turn", '', "- $questions questions open, steps/questions-open.md and steps/history/questions-open-older.md")
    foreach ($name in @('steps/questions-for-bader.md', 'steps/bader-trust.md')) {
        if (Test-Path -LiteralPath (Join-Path $root $name)) { $body += "- Read $name" }
    }
    $body += @('', '## Next three steps', '')
    $next = @($tasks | Where-Object { $_.status -ne 'done' } | Select-Object -First 3 | ForEach-Object { Limit-Line "- Task $($_.id) in lane $($_.lane), $($_.status)" })
    if ($next.Count -eq 0) { $next = @('- Lead starts the next round with /round') }
    $body += $next
    $text = ($body -join "`n") + "`n"
    $hash = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace('-', '').ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $root 'steps\PROGRESS.md'), ($text + "Hash of the lines above, a hand edit fails a test: $hash`n"), (New-Object Text.UTF8Encoding $false))
    Write-Output 'Wrote steps\PROGRESS.md'
}
