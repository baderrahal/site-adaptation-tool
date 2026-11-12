# The quick tests: the pytest files and the dotnet filter named in scripts/quick-set.txt. Exit 1 when any fail.
# The Stop hook and the task skill run this. The full gate stays with GitHub.
. (Join-Path $PSScriptRoot '..\.claude\hooks\lane.ps1')

$root = Get-RepoRoot $PSScriptRoot
$main = Get-MainFolder $root
Remove-Item Env:\SITE_ADAPTATION_INPUT -ErrorAction SilentlyContinue

# A worktree has no .venv of its own
$python = Join-Path $root '.venv\Scripts\python.exe'
if (-not (Test-Path -LiteralPath $python)) { $python = Join-Path $main '.venv\Scripts\python.exe' }
if (-not (Test-Path -LiteralPath $python)) {
    Write-Output "FAIL no .venv in $root or $main, run the setup commands of CLAUDE.md."
    exit 1
}

$files = @()
$filter = ''
foreach ($line in Get-Content -LiteralPath (Join-Path $PSScriptRoot 'quick-set.txt')) {
    if ($line -match '^pytest\s+(.+)$') { $files += $Matches[1].Trim() }
    elseif ($line -match '^dotnet-filter\s+(.+)$') { $filter = $Matches[1].Trim() }
}

# Bader's rule of 12 Nov: a test writes only under the work root, so the temporary files of both test sets go there too
$temp = Join-Path (Get-WorkRoot) 'tmp'
New-Item -ItemType Directory -Force $temp | Out-Null
$env:TEMP = $temp
$env:TMP = $temp

Push-Location $root
try {
    $failed = 0
    & $python 'scripts\checks\write_places.py' --save | Out-Null
    & $python -m pytest @files -q -p no:cacheprovider --tb=short
    if ($LASTEXITCODE -ne 0) { $failed += 1 }
    & dotnet test 'dotnet\SiteAdaptation.Tests\SiteAdaptation.Tests.csproj' -c Release -f net8.0 --nologo -v quiet --filter $filter
    if ($LASTEXITCODE -ne 0) { $failed += 1 }
    & $python 'scripts\checks\write_places.py' --check
    if ($LASTEXITCODE -ne 0) { $failed += 1 }
}
finally {
    Pop-Location
}
if ($failed -gt 0) {
    Write-Output "FAIL quick tests, $failed of 3 parts failed, pytest, dotnet and the write places check."
    exit 1
}
Write-Output 'PASS quick tests, pytest and dotnet.'
exit 0
