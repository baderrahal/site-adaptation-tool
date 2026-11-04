# Stop. Runs the quick tests, a failure blocks stopping and shows the failures. A folder with nothing changed since its
# last pass stops at once. After 3 blocks in a row the session may stop, so a fault it cannot fix does not spin forever.
. (Join-Path $PSScriptRoot 'lane.ps1')

$limit = 3
$payload = Read-HookInput
$root = Get-RepoRoot (Get-HookFolder $payload)
if (-not $root) { exit 0 }

$gitDir = (& git -C $root rev-parse --absolute-git-dir 2>$null)
if (-not $gitDir) { exit 0 }
$passFile = Join-Path $gitDir 'sa-quick-pass'
$blockFile = Join-Path $gitDir 'sa-quick-blocks'

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

$output = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'scripts\quick-tests.ps1') 2>&1
if ($LASTEXITCODE -eq 0) {
    Set-Content -LiteralPath $passFile -Value $state
    if (Test-Path -LiteralPath $blockFile) { Remove-Item -LiteralPath $blockFile }
    exit 0
}

$blocks = 0
if (Test-Path -LiteralPath $blockFile) { $blocks = [int](Get-Content -LiteralPath $blockFile -Raw) }
$blocks += 1
if ($blocks -gt $limit) {
    Remove-Item -LiteralPath $blockFile
    [Console]::Error.WriteLine("Quick tests still fail after $limit blocks, the session may stop. Say so in the report, a fix on its third attempt goes back to measuring.")
    exit 0
}
Set-Content -LiteralPath $blockFile -Value $blocks
$tail = ($output | Select-Object -Last 40 | ForEach-Object { "$_" }) -join "`n"
[Console]::Error.WriteLine("Quick tests failed, block $blocks of $limit. Fix them before stopping.`n$tail")
exit 2
