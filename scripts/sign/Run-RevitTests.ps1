#Requires -Version 7
# SETUP-sign. Runs dotnet\SiteAdaptation.RevitTests, the ricaun.RevitTest test, which starts a real Revit 2024. Run it under python/window_lock.py:
#   py -3.12 python\window_lock.py --round SETUP-sign --label "ricaun test" -- pwsh scripts\sign\Run-RevitTests.ps1 -Model <copy> -OutDir <folder>
# -Model is a copy of a model under the work root, the test copies it again and opens that copy. While the test runs, a loop looks at the
# security boxes of that Revit and logs each with its text. -Answer loadOnce clicks Load Once on a box that names this tool or ricaun and leaves
# every other box alone, -Answer none clicks nothing. Result: <OutDir>\result.json and test.log, read them, never the exit code.
param(
    [Parameter(Mandatory)][string]$Model,
    [Parameter(Mandatory)][string]$OutDir,
    [ValidateSet('loadOnce', 'none')][string]$Answer = 'none',
    [int]$MaxSeconds = 600,
    [string]$Expected = ''
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -Namespace Sa -Name Msg -MemberDefinition '[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr hwnd, uint msg, System.IntPtr w, System.IntPtr l);'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$result = [ordered]@{ started = (Get-Date).ToString('s'); model = (Split-Path $Model -Leaf); answerMode = $Answer; prompts = @(); reason = $null; testExit = $null; ended = $null }
function Save-Result { $result.ended = (Get-Date).ToString('s'); $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutDir 'result.json') -Encoding utf8 }

if (Get-Process LogonUI -ErrorAction SilentlyContinue) { $result.reason = 'screen is locked, nothing started'; Save-Result; return }
if (Get-Process Revit -ErrorAction SilentlyContinue) { $result.reason = 'a Revit process already runs, nothing started'; Save-Result; return }

$UIA = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]
function Prop($p, $v) { New-Object System.Windows.Automation.PropertyCondition ($p), $v }

$env:SITEADAPT_REVIT_TEST_MODEL = $Model
if ($Expected) { $env:SITEADAPT_REVIT_TEST_FLOORS = $Expected }
$log = Join-Path $OutDir 'test.log'
$err = Join-Path $OutDir 'test.err.log'
$testArgs = @('test', ('"' + (Join-Path $repo 'dotnet\SiteAdaptation.RevitTests') + '"'), '-c', 'Release', '--logger', '"console;verbosity=detailed"')
$proc = Start-Process -FilePath 'dotnet' -ArgumentList $testArgs -PassThru -NoNewWindow -RedirectStandardOutput $log -RedirectStandardError $err
$start = Get-Date
$handled = @{}
try {
    while (-not $proc.HasExited -and ((Get-Date) - $start).TotalSeconds -lt $MaxSeconds) {
        Start-Sleep -Milliseconds 800
        if (Get-Process LogonUI -ErrorAction SilentlyContinue) { $result.reason = 'screen locked during the run'; break }
        try {
            foreach ($revit in @(Get-Process Revit -ErrorAction SilentlyContinue)) {
                $inner = Prop $UIA::ControlTypeProperty $CT::Window
                $wins = @()
                foreach ($top in $UIA::RootElement.FindAll($Scope::Children, (Prop $UIA::ProcessIdProperty $revit.Id))) { $wins += $top; foreach ($c in $top.FindAll($Scope::Children, $inner)) { $wins += $c } }
                foreach ($win in $wins) {
                    $key = $win.GetRuntimeId() -join '.'
                    if ($handled.ContainsKey($key)) { continue }
                    $load = $win.FindFirst($Scope::Descendants, (Prop $UIA::NameProperty 'Load Once'))
                    if (-not $load) { continue }
                    $handled[$key] = $true
                    $texts = @(foreach ($d in $win.FindAll($Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) { $n = $d.Current.Name; if ($n) { $n } }) | Select-Object -Unique
                    $detail = $texts | Where-Object { $_ -like 'Name:*' } | Select-Object -First 1
                    $entry = [ordered]@{ at = [math]::Round(((Get-Date) - $start).TotalSeconds, 1); detail = $detail; answered = 'not answered' }
                    if ($Answer -eq 'loadOnce' -and $detail -match 'SiteAdaptation|Site Adaptation|ricaun') {
                        $pat = $null
                        if ($load.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pat)) { $pat.Invoke(); $entry.answered = 'Load Once (invoke)' }
                        elseif ($load.Current.NativeWindowHandle -ne 0) { [void][Sa.Msg]::PostMessage([IntPtr]$load.Current.NativeWindowHandle, 0xF5, [IntPtr]::Zero, [IntPtr]::Zero); $entry.answered = 'Load Once (click message)' }
                    }
                    $result.prompts += $entry
                }
            }
        }
        catch { $result.skippedReads = 1 + [int]$result.skippedReads }
    }
    if (-not $proc.HasExited) {
        if (-not $result.reason) { $result.reason = "time limit $MaxSeconds s, the test run was stopped" }
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }
    elseif (-not $result.reason) { $result.reason = 'the test run ended' }
    $result.testExit = if ($proc.HasExited) { $proc.ExitCode } else { $null }
}
finally {
    Get-Process Revit -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill(); [void]$_.WaitForExit(10000) }
    $result.revitLeftOpen = [bool](Get-Process Revit -ErrorAction SilentlyContinue)
    Save-Result
}
