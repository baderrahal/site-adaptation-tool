# Stop. Runs the quick tests, a failure blocks stopping and shows the failures. A folder with nothing changed since its last pass stops at once.
# After two blocks in a row it writes a Blocker line to steps/STATUS.md and lets the session stop, so a fault it cannot fix does not spin.
. (Join-Path $PSScriptRoot 'lane.ps1')

$limit = 2
$payload = Read-HookInput
$root = Get-RepoRoot (Get-HookFolder $payload)
if (-not $root) { exit 0 }

$gitDir = (& git -C $root rev-parse --absolute-git-dir 2>$null)
if (-not $gitDir) { exit 0 }
$passFile = Join-Path $gitDir 'sa-quick-pass'
$blockFile = Join-Path $gitDir 'sa-quick-blocks'

# A stop that is not the answer to a block starts the count again
if (-not $payload.stop_hook_active -and (Test-Path -LiteralPath $blockFile)) { Remove-Item -LiteralPath $blockFile }

# A second edit of a file already changed leaves git status the same, so the state holds the size and time of each changed file
$rows = @(& git -C $root rev-parse HEAD 2>$null)
foreach ($line in @(& git -C $root status --porcelain --untracked-files=all 2>$null)) {
    $path = ($line.Substring(3) -replace '^.* -> ', '').Trim('"')
    $file = Join-Path $root $path
    $stamp = 'gone'
    if (Test-Path -LiteralPath $file -PathType Leaf) {
        $item = Get-Item -LiteralPath $file
        $stamp = "$($item.Length) $($item.LastWriteTimeUtc.Ticks)"
    }
    $rows += "$line $stamp"
}
$state = $rows -join "`n"
if ((Test-Path -LiteralPath $passFile) -and (Get-Content -LiteralPath $passFile -Raw).Trim() -eq $state.Trim()) { exit 0 }

$output = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'scripts\quick-tests.ps1') 2>&1
if ($LASTEXITCODE -eq 0) {
    Set-Content -LiteralPath $passFile -Value $state
    if (Test-Path -LiteralPath $blockFile) { Remove-Item -LiteralPath $blockFile }
    exit 0
}

$blocks = 0
if (Test-Path -LiteralPath $blockFile) { $blocks = [int](Get-Content -LiteralPath $blockFile -Raw) }
if ($blocks -ge $limit) {
    Remove-Item -LiteralPath $blockFile
    Add-StatusBlocker $root "Blocker: $(Get-LaneName $root), quick tests still fail after $limit blocks, $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
    [Console]::Error.WriteLine("Quick tests still fail after $limit blocks, the session may stop. A Blocker line is in steps/STATUS.md.")
    exit 0
}
$blocks += 1
Set-Content -LiteralPath $blockFile -Value $blocks
$tail = ($output | Select-Object -Last 40 | ForEach-Object { "$_" }) -join "`n"
[Console]::Error.WriteLine("Quick tests failed, block $blocks of $limit. Fix them before stopping.`n$tail")
exit 2
