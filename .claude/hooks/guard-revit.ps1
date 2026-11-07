# PreToolUse on Bash and PowerShell. Only the revit-test lane starts Revit or sends a job to one, and a start needs a free slot
# of the shared lock and fewer than siteBuild.maxWindows Revit or Civil 3D windows. A command that only reads about Revit passes.
# Found by pattern on the command text, so a script that starts Revit under another name is not seen, python/window_lock.py still holds its lock.
. (Join-Path $PSScriptRoot 'lane.ps1')

function Stop-Revit([string]$Why) {
    [Console]::Error.WriteLine("Blocked by .claude/hooks/guard-revit.ps1: $Why")
    exit 2
}

# Splits a command on && || | & new lines and the semicolon, outside quotes, so a pattern inside a quoted argument stays one word
function Split-Segments([string]$Command) {
    $semicolon = [char]59
    $parts = New-Object System.Collections.ArrayList
    $part = New-Object System.Text.StringBuilder
    $quote = [char]0
    foreach ($c in $Command.ToCharArray()) {
        if ($quote -ne [char]0) {
            if ($c -eq $quote) { $quote = [char]0 }
            [void]$part.Append($c)
        }
        elseif ($c -eq '"' -or $c -eq "'") {
            $quote = $c
            [void]$part.Append($c)
        }
        elseif ($c -eq '|' -or $c -eq '&' -or $c -eq "`n" -or $c -eq "`r" -or $c -eq $semicolon) {
            [void]$parts.Add($part.ToString())
            [void]$part.Clear()
        }
        else {
            [void]$part.Append($c)
        }
    }
    [void]$parts.Add($part.ToString())
    return $parts
}

# 'start' when the command opens Revit, 'job' when it sends a job to a Revit already open, else empty
function Get-RevitUse([string]$Command) {
    $readers = '^(grep|rg|cat|sed|awk|head|tail|less|ls|dir|find|wc|echo|printf|git|gh|type|write-output|write-host|get-content|gc|select-string|sls|test-path|get-childitem|gci|get-item|get-process|gps|stop-process|taskkill|tasklist|code|notepad)$'
    $launchers = '^(start-process|saps|start|invoke-item|ii|explorer)(\.exe)?$'
    $shells = '^(cmd|pwsh|powershell|bash|sh)(\.exe)?$'
    # Only the command a shell is handed to run, not the file or the note it is given, so a status note that names a lane passes
    $inShell = '(?i)\s(-c|-command|/c)\s.*(revit\.exe|\b(start-process|saps|start|invoke-item|ii)\b.*(\brevit\b|\.rvt\b))'
    $scripts = '(?i)\b(run-testhost|run-parallel|start-revitlistener)\b|\bpython\S*\s+(\S*[\\/])?place_codes\.py\b'
    $use = ''
    # The text of a heredoc is a message or a file, not a command
    $Command = $Command -replace '(?s)<<-?\s*[''"]?(\w+)[''"]?.*?\n\s*\1\b', ''
    foreach ($segment in (Split-Segments $Command)) {
        $text = $segment.Trim()
        if (-not $text) { continue }
        $first = ($text -split '\s+')[0]
        if ($text -match '^"([^"]+)"' -or $text -match "^'([^']+)'") { $first = $Matches[1] }
        $name = ($first.Trim('"', "'") -split '[\\/]')[-1].ToLowerInvariant()
        if ($name -match '^revit(\.exe)?$' -or $name -match '\.rvt$') { return 'start' }
        if ($name -match $readers) { continue }
        if ($name -match $launchers -and $text -match '(?i)\brevit(\.exe)?\b|\.rvt\b') { return 'start' }
        if ($name -match $shells -and $text -match $inShell) { return 'start' }
        if ($text -match $scripts) { return 'start' }
        if ($text -match '(?i)dotnet\s+test\b.*revittests?\b') { return 'start' }
        if ($text -match '(?i)\bsend-revitjob\b') { $use = 'job' }
    }
    return $use
}

# As python/window_lock.py: a machine of at least revitSlotsMinTotalGB holds revitSlots slots, window.lock and window-2.lock
function Get-LockFiles($Block) {
    $base = ([string]$Block.lock).Replace('{workRoot}', (Get-WorkRoot))
    $files = @($base)
    $slots = [int]$Block.revitSlots
    if ($slots -gt 1) {
        $totalGB = (Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB
        if ($totalGB -ge [double]$Block.revitSlotsMinTotalGB) {
            foreach ($slot in 2..$slots) {
                $name = [IO.Path]::GetFileNameWithoutExtension($base) + "-$slot" + [IO.Path]::GetExtension($base)
                $files += Join-Path (Split-Path -Parent $base) $name
            }
        }
    }
    return $files
}

$payload = Read-HookInput
$command = [string]$payload.tool_input.command
if (-not $command -or $command -notmatch '(?i)revit|run-testhost|run-parallel|place_codes|\.rvt') { exit 0 }
$use = Get-RevitUse $command
if (-not $use) { exit 0 }

$root = Get-RepoRoot (Get-HookFolder $payload)
if (-not $root) { Stop-Revit 'this folder is not in the repo, so its lane is not known.' }
# Bader's order of 7 Nov 10:05 lets the builders the file names test in Revit before their PR, for the fast build only. The file comes
# through main, which a builder cannot edit, and is deleted after 8 Nov with the fast section of CLAUDE.md
$extra = Join-Path $root '.claude\revit-lanes.txt'
$allowed = (Test-Path -LiteralPath $extra) -and (@(Get-Content -LiteralPath $extra | ForEach-Object { $_.Trim() }) -contains (Get-LaneName $root))
if ((Get-LaneRole $root) -ne 'revit tester' -and -not $allowed) { Stop-Revit "only the worktree named revit-test opens Revit, this lane is $(Get-LaneName $root)." }
# A job goes to the listener, which holds the lock itself and opens no window
if ($use -eq 'job') { exit 0 }

$block = (Get-ProjectConfig $root).siteBuild
$files = @(Get-LockFiles $block)
$free = @($files | Where-Object { -not (Test-Path -LiteralPath $_) })
if ($free.Count -eq 0) { Stop-Revit "the shared lock is held, every slot is taken: $($files -join ', ')" }

$limit = [int]$block.maxWindows
$open = @(Get-Process -Name @($block.windowProcesses) -ErrorAction SilentlyContinue).Count
if ($open -ge $limit) { Stop-Revit "$open Revit or Civil 3D windows are open, the limit is under $limit." }
exit 0
