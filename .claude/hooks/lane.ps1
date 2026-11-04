# Shared by the lane hooks and scripts/status.ps1. Finds the lane from the folder, never from a setting.
# The main repo folder holds .git as a folder, a worktree holds .git as a file that points at the main repo.

function Get-RepoRoot([string]$Start) {
    $folder = $Start
    while ($folder) {
        if (Test-Path -LiteralPath (Join-Path $folder '.git')) { return $folder }
        $parent = Split-Path -Parent $folder
        if ($parent -eq $folder) { break }
        $folder = $parent
    }
    return $null
}

function Test-MainFolder([string]$Root) {
    return Test-Path -LiteralPath (Join-Path $Root '.git') -PathType Container
}

function Get-MainFolder([string]$Root) {
    if (Test-MainFolder $Root) { return $Root }
    $line = Get-Content -LiteralPath (Join-Path $Root '.git') -TotalCount 1
    $gitDir = ($line -replace '^gitdir:\s*', '').Trim()
    if (-not [IO.Path]::IsPathRooted($gitDir)) { $gitDir = Join-Path $Root $gitDir }
    # A worktree points at <main>\.git\worktrees\<name>
    return Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $gitDir))
}

function Get-LaneName([string]$Root) {
    if (Test-MainFolder $Root) { return 'lead' }
    return Split-Path -Leaf $Root
}

function Get-LaneRole([string]$Root) {
    if (Test-MainFolder $Root) { return 'lead' }
    if ((Split-Path -Leaf $Root) -eq 'revit-test') { return 'revit tester' }
    return 'builder'
}

# A task file opens with lines between two --- marks. Returns the id, lane, status, allowed folders and path of each
function Read-TaskFile([string]$Path) {
    return Read-TaskLines (Get-Content -LiteralPath $Path) $Path
}

function Read-TaskLines([string[]]$Lines, [string]$Path) {
    $task = @{
        path    = $Path
        id      = ''
        lane    = ''
        status  = ''
        allowed = @()
    }
    $inHead = $false
    $inAllowed = $false
    foreach ($line in $Lines) {
        if ($line.Trim() -eq '---') {
            if ($inHead) { break }
            $inHead = $true
            continue
        }
        if (-not $inHead) { break }
        if ($inAllowed -and $line -match '^\s+-\s+(.+?)\s*$') {
            $task.allowed += $Matches[1].Trim('"').Trim("'").Replace('\', '/')
            continue
        }
        $inAllowed = $false
        if ($line -match '^(id|lane|status):\s*(.*?)\s*$') { $task[$Matches[1]] = $Matches[2] }
        elseif ($line -match '^allowed:\s*$') { $inAllowed = $true }
    }
    return $task
}

# The open task whose lane is this folder's lane, the first by name when more than one is open. The copy on origin/main decides, so
# a lane cannot widen its own allowed list, and its status: done on the branch does not lock it out before the merge. A task not
# merged yet gives no folders. Only a folder with no origin/main reads the file on disk.
function Get-CurrentTask([string]$Root) {
    $folder = Join-Path $Root 'steps\tasks'
    if (-not (Test-Path -LiteralPath $folder)) { return $null }
    $lane = Get-LaneName $Root
    & git -C $Root rev-parse --verify --quiet origin/main 2>$null | Out-Null
    $hasMain = $LASTEXITCODE -eq 0
    foreach ($file in Get-ChildItem -LiteralPath $folder -Filter '*.md' | Sort-Object Name) {
        if ($file.Name -eq 'README.md') { continue }
        $task = Read-TaskFile $file.FullName
        if ($task.lane -ne $lane) { continue }
        if ($hasMain) {
            $lines = @(& git -C $Root show "origin/main:steps/tasks/$($file.Name)" 2>$null)
            if ($LASTEXITCODE -ne 0) { continue }
            $task = Read-TaskLines $lines $file.FullName
        }
        if ($task.status -eq 'open' -and $task.lane -eq $lane) { return $task }
    }
    return $null
}

function Get-ProjectConfig([string]$Root) {
    $path = Join-Path $Root 'config\project.json'
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    return Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
}

# The roots D19 names, beside inputRoot of config
function Get-InputRoots([string]$Root) {
    $roots = @('C:\02-Site Adaptation', 'C:\10- ROSHEN MECCA')
    if ($Root) {
        $config = Get-ProjectConfig $Root
        if ($config -and $config.inputRoot) { $roots += [string]$config.inputRoot }
    }
    return $roots
}

# As python/project_profile.py work_root
function Get-WorkRoot {
    if ($env:APPDATA) {
        $machine = Join-Path $env:APPDATA 'SiteAdaptation\machine.json'
        if (Test-Path -LiteralPath $machine) {
            $work = (Get-Content -LiteralPath $machine -Raw | ConvertFrom-Json).workRoot
            if ($work) { return [string]$work }
        }
    }
    return Join-Path $HOME 'Documents\SiteAdaptation'
}

function Get-StatusPath([string]$Root) {
    return Join-Path (Get-MainFolder $Root) 'steps\STATUS.md'
}

function Read-HookInput {
    $text = [Console]::In.ReadToEnd()
    if (-not $text) { return $null }
    return $text | ConvertFrom-Json
}

function Get-HookFolder($Payload) {
    if ($Payload -and $Payload.cwd) { return [string]$Payload.cwd }
    if ($env:CLAUDE_PROJECT_DIR) { return $env:CLAUDE_PROJECT_DIR }
    return (Get-Location).Path
}

function Test-InsideFolder([string]$Path, [string]$Folder) {
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $base = [IO.Path]::GetFullPath($Folder).TrimEnd('\')
    return $full.Equals($base, [StringComparison]::OrdinalIgnoreCase) -or $full.StartsWith($base + '\', [StringComparison]::OrdinalIgnoreCase)
}
