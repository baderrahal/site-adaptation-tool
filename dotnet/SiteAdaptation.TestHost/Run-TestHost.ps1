# One run of the Revit test host for one prototype (R101, R102).
# It copies the site model to <name>_TEST-<code>.rvt by a file copy, writes the job file, starts Revit 2024
# with the token of this run and waits for Revit to exit. The original model is never opened.
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Z0-9]{1,3}$')]
    [string]$Code,
    [int]$TimeoutMinutes = 30
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$config = Get-Content -Raw (Join-Path $repo 'config\project.json') | ConvertFrom-Json
$root = if ($env:SITE_ADAPTATION_INPUT) { $env:SITE_ADAPTATION_INPUT } else { $config.inputRoot }
$original = Join-Path $root $config.testHost.model
$planPath = $config.pastJob.output
$docs = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'SiteAdaptation'
$jobPath = Join-Path $docs 'testhost_job.json'

if (Get-Process -Name Revit -ErrorAction SilentlyContinue) {
    throw 'Revit is running. The test host needs a Revit of its own, close the other one first.'
}
if (Test-Path $jobPath) {
    throw "A job file is already waiting at $jobPath. Look at it before you run again."
}

# Revit.exe from the install folder the Revit 2024 setup wrote to the registry
$install = Get-ChildItem 'HKLM:\SOFTWARE\Autodesk\Revit\2024' -ErrorAction Stop |
    ForEach-Object { (Get-ItemProperty $_.PSPath).InstallationLocation } |
    Where-Object { $_ } | Select-Object -First 1
$revit = Join-Path $install 'Revit.exe'
if (-not (Test-Path $revit)) {
    throw "Revit.exe is not in the Revit 2024 install folder $install"
}

# The prototype file of the code comes from the plan, never from the code alone
$plan = Get-Content -Raw $planPath | ConvertFrom-Json
$files = @($plan.plots | ForEach-Object { $_.prototypeFile.value } | Where-Object { $_ -match ('-' + $Code + '-ZZ-MOD-') } | Sort-Object -Unique)
if ($files.Count -ne 1) {
    throw "The plan names $($files.Count) prototype files for $Code, one is needed: $($files -join ', ')"
}
$prototype = $files[0]
$plots = @($plan.plots | Where-Object { $_.prototypeFile.value -eq $prototype }).Count

# A fresh copy every run. An earlier copy of the same code is kept under the next free run number
$folder = Split-Path $original
$base = [IO.Path]::GetFileNameWithoutExtension($original)
$copy = Join-Path $folder ($base + '_TEST-' + $Code + '.rvt')
if (Test-Path $copy) {
    $n = 1
    while (Test-Path (Join-Path $folder ($base + '_TEST-' + $Code + '_run' + $n + '.rvt'))) {
        $n++
    }
    Rename-Item $copy ($base + '_TEST-' + $Code + '_run' + $n + '.rvt')
}
$before = Get-Item $original
$hashBefore = (Get-FileHash $original).Hash
Copy-Item $original $copy

# Outputs of an earlier run of this code are moved aside so a run that writes nothing cannot pass on old files
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$old = @(Get-ChildItem $docs -Filter ('testhost_' + $Code + '_*.json'))
if ($old.Count -gt 0) {
    $aside = Join-Path $docs ('testhost_old\' + $Code + '_' + $stamp)
    New-Item -ItemType Directory -Force $aside | Out-Null
    $old | Move-Item -Destination $aside
}

$token = [guid]::NewGuid().ToString('N')
$job = [ordered]@{
    token         = $token
    code          = $Code
    model         = $copy
    prototypeFile = $prototype
    plan          = $planPath
    createdUtc    = (Get-Date).ToUniversalTime().ToString('o')
    totalMinutes  = [Math]::Min(27, $TimeoutMinutes - 2)
}
$job | ConvertTo-Json | Set-Content -Path $jobPath -Encoding utf8

$env:SITEADAPTATION_TESTHOST_TOKEN = $token
$start = Get-Date
$process = Start-Process -FilePath $revit -PassThru
Remove-Item Env:\SITEADAPTATION_TESTHOST_TOKEN
"Revit $($process.Id) started at $($start.ToString('HH:mm:ss')) for $Code, $prototype, $plots plots in the plan, copy $copy"

$exited = $process.WaitForExit($TimeoutMinutes * 60000)
$end = Get-Date
$logPath = Join-Path $docs ('testhost_' + $Code + '_log.json')
if (-not $exited) {
    $title = (Get-Process -Id $process.Id -ErrorAction SilentlyContinue).MainWindowTitle
    "Revit did not exit in $TimeoutMinutes minutes. Window title: $title"
    if (Test-Path $logPath) {
        $stuck = (Get-Content -Raw $logPath | ConvertFrom-Json).steps | Where-Object { -not $_.endedUtc } | Select-Object -First 1
        "Stuck in step: $($stuck.name)"
    }
    else {
        'No log was written, the test host never claimed the job.'
    }
    Stop-Process -Id $process.Id -Force
    $process.WaitForExit(60000) | Out-Null
}

if (Test-Path $jobPath) {
    $left = Get-Content -Raw $jobPath | ConvertFrom-Json
    if ($left.token -eq $token) {
        Remove-Item $jobPath
        'The job file was still there, the test host did not claim it. It is removed now.'
    }
}
$refused = Join-Path $docs 'testhost_refused.json'
if ((Test-Path $refused) -and (Get-Item $refused).LastWriteTime -gt $start) {
    'Refused: ' + (Get-Content -Raw $refused | ConvertFrom-Json).reason
}

$after = Get-Item $original
$same = ((Get-FileHash $original).Hash -eq $hashBefore) -and ($after.LastWriteTimeUtc -eq $before.LastWriteTimeUtc) -and ($after.Length -eq $before.Length)
"Run time {0:0.0} minutes, exit code {1}, original model unchanged: {2}" -f ($end - $start).TotalMinutes, $(if ($exited) { $process.ExitCode } else { 'stopped' }), $same

if (Test-Path $logPath) {
    $log = Get-Content -Raw $logPath | ConvertFrom-Json
    "Log result: $($log.result)"
    $log.steps | Select-Object name, result, seconds, detail | Format-Table -Wrap -AutoSize | Out-String -Width 250
    if ($log.linkOpenings) {
        'Link openings:'
        $log.linkOpenings | Select-Object @{
            n = 'file'
            e = { Split-Path $_.path -Leaf }
        }, step, seconds | Format-Table -AutoSize | Out-String -Width 200
    }
    "Dialogs $(@($log.dialogs).Count), failure lines $(@($log.failures).Count)"
    foreach ($step in @($log.steps | Where-Object { $_.exception })) {
        "Exception in $($step.name):"
        $step.exception
    }
}
'TEST copies and Revit backups next to the model:'
Get-ChildItem $folder | Where-Object { $_.Name -like ($base + '_TEST-*') } | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 200
