# PreToolUse on Edit and Write. Exit 2 blocks the edit and shows the line to the session.
. (Join-Path $PSScriptRoot 'lane.ps1')

function Stop-Edit([string]$Why) {
    [Console]::Error.WriteLine("Blocked by .claude/hooks/guard-edit.ps1: $Why")
    exit 2
}

$payload = Read-HookInput
$file = $payload.tool_input.file_path
if (-not $file) { $file = $payload.tool_input.notebook_path }
if (-not $file) { exit 0 }

$start = Get-HookFolder $payload
if (-not [IO.Path]::IsPathRooted($file)) { $file = Join-Path $start $file }
$file = [IO.Path]::GetFullPath($file)

$root = Get-RepoRoot $start

foreach ($inputRoot in Get-InputRoots $root) {
    if (Test-InsideFolder $file $inputRoot) { Stop-Edit "$file is in the input folder $inputRoot, input folders are read only." }
}

# Bader, 12 Nov 16:55. Cowork writes all tool code, in the Cowork folder, and runs none of these hooks. So a session that does run
# them, the lead or a lane of Claude Code, edits no tool code path, in the main folder, a worktree or the Cowork folder. A fault it
# finds goes in the Bridge report. The file's own repo folder decides the path, so a reach from another folder is seen too.
$owner = Get-RepoRoot (Split-Path -Parent $file)
if ($owner) {
    $inside = $file.Substring($owner.TrimEnd('\').Length).TrimStart('\').Replace('\', '/')
    foreach ($code in Get-ToolCodePaths) {
        if ($inside.StartsWith($code + '/', [StringComparison]::OrdinalIgnoreCase)) {
            Stop-Edit "$inside is tool code. Cowork writes all tool code in the Cowork folder, Claude Code builds, tests, reports, commits and merges. Put the fault in the Bridge report."
        }
    }
}

if (-not $root) { exit 0 }

$role = Get-LaneRole $root
$lane = Get-LaneName $root
$main = Get-MainFolder $root

if ($role -ne 'lead' -and (Test-InsideFolder $file $main) -and -not (Test-InsideFolder $file $root)) {
    Stop-Edit "lane $lane may not edit the main repo folder, $file is outside this worktree."
}

# Outside the repo a lane writes only to its scratch, the work root and the session's own memory. Anywhere else, such as another
# lane's worktree, a demo config or an install folder, is outside its task.
if (-not (Test-InsideFolder $file $root)) {
    if ($role -eq 'lead') { exit 0 }
    foreach ($place in @($env:TEMP, (Get-WorkRoot), (Join-Path $HOME '.claude'))) {
        if ($place -and (Test-InsideFolder $file $place)) { exit 0 }
    }
    Stop-Edit "$file is outside this worktree and outside the scratch and work folders, lane $lane may not write there."
}

$relative = $file.Substring($root.TrimEnd('\').Length).TrimStart('\').Replace('\', '/')

if ($relative -ieq 'steps/STATUS.md') { Stop-Edit 'steps/STATUS.md is written by scripts/status.ps1 and the Notification hook, never by hand.' }
if ($relative -ieq 'steps/PROGRESS.md') { Stop-Edit 'steps/PROGRESS.md is written by scripts/status.ps1 -Progress, never by hand.' }

if ($relative -notmatch '/' -and -not (Test-Path -LiteralPath $file) -and $relative -ine 'CLAUDE.md') {
    Stop-Edit "no new files at the repo root, only CLAUDE.md, .claude and .github live there by rule. $relative needs a folder."
}

if ($relative -match '^tests/(acceptance|golden|holdout)(/|$)' -and $role -ne 'lead') {
    Stop-Edit "tests/acceptance, tests/golden and tests/holdout are owned by the lead, this lane is $lane."
}

$task = Get-CurrentTask $root
if (-not $task) {
    if ($role -eq 'lead') { exit 0 }
    Stop-Edit "no open task in steps/tasks has lane: $lane, so this lane has no folders to edit. Ask the lead for a task."
}

$own = ($task.path.Substring($root.TrimEnd('\').Length).TrimStart('\')).Replace('\', '/')
if ($relative -ieq $own) { exit 0 }
foreach ($allowed in $task.allowed) {
    $item = $allowed.TrimEnd('/')
    if ($relative -ieq $item -or $relative.StartsWith($item + '/', [StringComparison]::OrdinalIgnoreCase)) { exit 0 }
}
Stop-Edit "$relative is outside the allowed folders of task $($task.id): $($task.allowed -join ', ')"
