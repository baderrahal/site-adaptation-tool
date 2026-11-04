# SessionStart. Prints the lane, its current task file and steps/STATUS.md, what it prints reaches the session.
. (Join-Path $PSScriptRoot 'lane.ps1')

$payload = Read-HookInput
$root = Get-RepoRoot (Get-HookFolder $payload)
if (-not $root) { exit 0 }
$lane = Get-LaneName $root
Write-Output "Lane: $lane, role: $(Get-LaneRole $root), folder: $root"

$task = Get-CurrentTask $root
if ($task) {
    Write-Output "Current task file: $($task.path)"
    Get-Content -LiteralPath $task.path | Write-Output
}
else {
    Write-Output 'Current task file: none open for this lane.'
}

$status = Get-StatusPath $root
if (Test-Path -LiteralPath $status) {
    Write-Output "--- $status"
    Get-Content -LiteralPath $status -Encoding UTF8 | Write-Output
}
else {
    Write-Output 'steps/STATUS.md is not written yet, run scripts/status.ps1.'
}
exit 0
