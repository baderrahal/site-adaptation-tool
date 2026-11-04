# Runs a guard written for bash, so a hook is called the same way as the others and needs no Git Bash on its path. Exit 2 from the guard blocks
# the tool, as it does when called alone. With no bash to run it the tool is blocked, a guard that cannot run must not read as a pass.
param([Parameter(Mandatory)][string]$Guard)

$bash = $env:CLAUDE_CODE_GIT_BASH_PATH
if (-not $bash -or -not (Test-Path -LiteralPath $bash)) {
    $bash = $null
    # git may sit in cmd, bin or mingw64\bin of its install, so each folder above it is tried
    $git = Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    $folder = $null
    if ($git) { $folder = Split-Path -Parent $git.Source }
    while ($folder -and -not $bash) {
        foreach ($place in @('bin\bash.exe', 'usr\bin\bash.exe')) {
            if (-not $bash -and (Test-Path -LiteralPath (Join-Path $folder $place))) { $bash = Join-Path $folder $place }
        }
        $parent = Split-Path -Parent $folder
        if ($parent -eq $folder) { break }
        $folder = $parent
    }
    if (-not $bash) {
        # Never the bash of System32, it is the WSL launcher and not a Git Bash
        $found = Get-Command bash -CommandType Application -ErrorAction SilentlyContinue | Where-Object { $_.Source -notmatch '\\System32\\' } | Select-Object -First 1
        if ($found) { $bash = $found.Source }
    }
}
if (-not $bash -or -not (Test-Path -LiteralPath $bash)) {
    [Console]::Error.WriteLine("Blocked by .claude/hooks/sh-guard.ps1: no Git Bash found to run $Guard. Set CLAUDE_CODE_GIT_BASH_PATH.")
    exit 2
}

$text = [Console]::In.ReadToEnd()
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = $bash
$start.Arguments = '"' + (Join-Path $PSScriptRoot $Guard) + '"'
$start.UseShellExecute = $false
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$process = [System.Diagnostics.Process]::Start($start)
$writer = New-Object System.IO.StreamWriter($process.StandardInput.BaseStream, (New-Object Text.UTF8Encoding $false))
$writer.Write($text)
$writer.Close()
$errors = $process.StandardError.ReadToEndAsync()
$out = $process.StandardOutput.ReadToEnd()
$process.WaitForExit()
if ($out) { [Console]::Out.Write($out) }
if ($errors.Result) { [Console]::Error.Write($errors.Result) }
exit $process.ExitCode
