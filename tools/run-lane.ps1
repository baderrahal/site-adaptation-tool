<#
Runs one lane of the build, one phase session after another (D17, Bader 2026-10-29).
Lane A runs in the repo folder on main. Lane B runs in a detached worktree of the repo at the sibling folder
"Site Adaptation Tool Lane B WT", since main is checked out in the repo folder.
The script never edits a tracked file of the repo. It writes only the output of each session, under the work root.
Stop a lane by creating steps\STOP-A or steps\STOP-B in the repo folder, both are ignored by git.
-StartFile and -Claude are for the proof of the loop only, a lane runs with their defaults.
#>
param(
    [Parameter(Mandatory = $true)][ValidateSet('A', 'B')][string]$Lane,
    [string]$StartFile,
    [string]$Claude
)

# Continue, so that git writing its progress to stderr never ends the loop under Windows PowerShell 5.1, every exit code is read instead
$ErrorActionPreference = 'Continue'
$repo = Split-Path -Parent $PSScriptRoot
$worktree = Join-Path (Split-Path -Parent $repo) 'Site Adaptation Tool Lane B WT'
$laneDir = if ($Lane -eq 'A') { $repo } else { $worktree }
$outDir = Join-Path $env:USERPROFILE 'Documents\SiteAdaptation\runner'
$stopFile = Join-Path $repo "steps\STOP-$Lane"
if (-not $StartFile) {
    $StartFile = Join-Path $laneDir $(if ($Lane -eq 'A') { 'steps\START.md' } else { 'steps\START-B.md' })
}

# The CLI on the PATH first, else the newest one the VS Code extension brings, as on the laptop
if (-not $Claude) {
    $onPath = Get-Command claude -ErrorAction SilentlyContinue
    if ($onPath) {
        $Claude = $onPath.Source
    } else {
        $found = Get-ChildItem (Join-Path $env:USERPROFILE '.vscode\extensions') -Directory -Filter 'anthropic.claude-code-*' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            ForEach-Object { Join-Path $_.FullName 'resources\native-binary\claude.exe' } |
            Where-Object { Test-Path $_ } | Select-Object -First 1
        if (-not $found) { throw 'No claude CLI found on the PATH or in the VS Code extensions folder.' }
        $Claude = $found
    }
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Invoke-Git {
    param([string]$Dir, [string[]]$GitArgs)
    $text = & git -C $Dir @GitArgs 2>&1 | Out-String
    return [pscustomobject]@{
        Code = $LASTEXITCODE
        Text = $text
    }
}

function Update-Lane {
    $fetch = Invoke-Git $repo @('fetch', '--prune', 'origin')
    if ($fetch.Code -ne 0) { return $fetch }
    if ($Lane -eq 'A') {
        foreach ($step in @(@('switch', 'main'), @('pull', '--ff-only'))) {
            $result = Invoke-Git $repo $step
            if ($result.Code -ne 0) { return $result }
        }
        return $result
    }
    if (-not (Test-Path (Join-Path $worktree '.git'))) {
        Write-Host "Making the Lane B worktree at $worktree"
        $result = Invoke-Git $repo @('worktree', 'add', '--detach', $worktree, 'origin/main')
        if ($result.Code -ne 0) { return $result }
    }
    return Invoke-Git $worktree @('checkout', '--detach', 'origin/main')
}

function Get-LastLines {
    param([string]$Text, [int]$Count)
    $lines = @($Text -split "`r?`n" | Where-Object { $_.Trim() -ne '' })
    if ($lines.Count -eq 0) { return @() }
    return $lines[[Math]::Max(0, $lines.Count - $Count)..($lines.Count - 1)]
}

$retried = $false
while ($true) {
    if (Test-Path $stopFile) {
        Write-Host "Lane $Lane stops, $stopFile exists."
        exit 0
    }

    $git = Update-Lane
    $failText = $null
    if ($git.Code -ne 0) {
        $failText = "git failed with exit code $($git.Code)`n$($git.Text)"
    } else {
        $stamp = Get-Date -Format 'yyyyMMdd-HHmm'
        $outFile = Join-Path $outDir "${Lane}_$stamp.txt"
        $prompt = Get-Content -Raw -Encoding UTF8 $StartFile
        Write-Host "Lane $Lane, session from $StartFile in $laneDir, output $outFile"
        Push-Location $laneDir
        try {
            $output = $prompt | & $Claude -p --model opus --effort high --permission-mode auto --max-budget-usd 200 2>&1 | Out-String
            $code = $LASTEXITCODE
        } finally {
            Pop-Location
        }
        Set-Content -Path $outFile -Value $output -Encoding UTF8
        $last = (Get-LastLines $output 1 | Select-Object -First 1)
        if ($null -eq $last) { $last = '' }
        $last = $last.Trim()
        Write-Host "Lane $Lane, exit code $code, last line: $last"
        if ($code -eq 0 -and $last -match '^PHASE DONE \S+') {
            $retried = $false
            continue
        }
        if ($code -eq 0 -and $last -match '^BLOCKED\b') {
            $retried = $false
            Write-Host 'Blocked, looking again in 10 minutes.'
            Start-Sleep -Seconds 600
            continue
        }
        if ($code -eq 0 -and $last -match '^QUESTION (.+)$') {
            [console]::Beep(880, 400)
            Write-Host "Lane $Lane waits for Bader on: $($Matches[1])"
            exit 0
        }
        if ($code -eq 0 -and $last -eq 'LANE DONE') {
            Write-Host "Lane $Lane is done."
            exit 0
        }
        $failText = "the session ended with exit code $code and an unknown last line`n$output"
    }

    if ($retried) {
        Write-Host "Lane $Lane stops after one retry. The last 20 lines:"
        Get-LastLines $failText 20 | ForEach-Object { Write-Host $_ }
        exit 1
    }
    $retried = $true
    Write-Host 'Waiting one minute, then one retry.'
    Start-Sleep -Seconds 60
}
