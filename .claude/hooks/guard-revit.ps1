# PreToolUse on Bash and PowerShell. Blocks starting Revit unless this is the revit-test lane, the shared lock is free
# and fewer than siteBuild.maxWindows Revit or Civil 3D windows are open. A command that only reads about Revit passes.
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

# True when the command opens Revit when it is run, false when it only reads about Revit
function Test-StartsRevit([string]$Command) {
    $readers = '^(grep|rg|cat|sed|awk|head|tail|less|ls|dir|find|wc|echo|printf|git|gh|type|write-output|write-host|get-content|gc|select-string|sls|test-path|get-childitem|gci|get-item|get-process|gps|stop-process|taskkill|tasklist|code|notepad)$'
    $scripts = '(run-testhost|run-parallel|start-revitlistener|send-revitjob)\.ps1'
    # The text of a heredoc is a message or a file, not a command
    $Command = $Command -replace '(?s)<<-?\s*[''"]?(\w+)[''"]?.*?\n\s*\1\b', ''
    foreach ($segment in (Split-Segments $Command)) {
        $text = $segment.Trim()
        if (-not $text) { continue }
        $first = ($text -split '\s+')[0]
        if ($text -match '^"([^"]+)"' -or $text -match "^'([^']+)'") { $first = $Matches[1] }
        $name = ($first.Trim('"', "'") -split '[\/]')[-1].ToLowerInvariant()
        if ($name -match '^revit(\.exe)?$') { return $true }
        if ($name -match $readers) { continue }
        if ($text -match '(?i)revit\.exe') { return $true }
        if ($text -match '(?i)\b(start-process|saps|start)\s+(-filepath\s+)?["'']?revit\b') { return $true }
        if ($text -match "(?i)$scripts") { return $true }
        if ($text -match '(?i)dotnet\s+test\b.*revittests?\b') { return $true }
    }
    return $false
}

$payload = Read-HookInput
$command = [string]$payload.tool_input.command
if (-not $command -or $command -notmatch '(?i)revit|run-testhost|run-parallel') { exit 0 }
if (-not (Test-StartsRevit $command)) { exit 0 }

$root = Get-RepoRoot (Get-HookFolder $payload)
if (-not $root) { Stop-Revit 'this folder is not in the repo, so its lane is not known.' }
$role = Get-LaneRole $root
if ($role -ne 'revit tester') { Stop-Revit "only the worktree named revit-test opens Revit, this lane is $(Get-LaneName $root)." }

$config = Get-ProjectConfig $root
$lock = ([string]$config.siteBuild.lock).Replace('{workRoot}', (Get-WorkRoot))
if ($env:SITE_ADAPTATION_HOOK_LOCK) { $lock = $env:SITE_ADAPTATION_HOOK_LOCK }
if (Test-Path -LiteralPath $lock) { Stop-Revit "the shared lock file $lock is held, wait until it is free." }

$limit = [int]$config.siteBuild.maxWindows
$names = @($config.siteBuild.windowProcesses)
if ($null -ne $env:SITE_ADAPTATION_HOOK_WINDOWS) {
    $open = [int]$env:SITE_ADAPTATION_HOOK_WINDOWS
}
else {
    $open = @(Get-Process -Name $names -ErrorAction SilentlyContinue).Count
}
if ($open -ge $limit) { Stop-Revit "$open Revit or Civil 3D windows are open, the limit is under $limit." }
exit 0
