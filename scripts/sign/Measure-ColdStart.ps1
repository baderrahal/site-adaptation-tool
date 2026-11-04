#Requires -Version 7
# SETUP-sign. Starts Revit 2024 cold, counts the add-in security prompts and takes a screenshot of each, then of the main window and the
# ribbon, and closes Revit. Run it under python/window_lock.py, it opens one Revit window. It reads and writes no registry key.
# A box of this tool is answered Load Once and a box of another maker Do Not Load, by the name of the button and never by its place, for
# this start only. Always Load is never pressed. Every answer is logged. A box with no button of that name is logged and left open.
# It stops only the Revit process it started, never another Revit of the person.
# Result: <OutDir>\result.json, read it, never the exit code.
# -Answer none leaves the first prompt unanswered, so it shows what an unattended run meets.
# -Model is a copy of a model under the work root, opened as Revit starts, since the ribbon shows its tabs with a model open only.
# The dialog is found as DialogWatcher finds it: a window of the Revit process, or a window inside one, that holds a button named Load Once.
param(
    [Parameter(Mandatory)][string]$OutDir,
    [int]$MaxSeconds = 300,
    [int]$IdleSeconds = 25,
    [ValidateSet('answer', 'none')][string]$Answer = 'answer',
    [string]$Label = 'cold start',
    [string]$Model = ''
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
# The C# below ends its statements with a tilde, put back as the semicolon here, since no .ps1 of the repo holds a semicolon
$win32 = @'
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool PrintWindow(System.IntPtr hwnd, System.IntPtr hdc, uint flags)~
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool GetWindowRect(System.IntPtr hwnd, out RECT r)~
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool ShowWindow(System.IntPtr hwnd, int cmd)~
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr hwnd, uint msg, System.IntPtr w, System.IntPtr l)~
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom~ }
'@
Add-Type -Namespace Sa -Name Win -MemberDefinition ($win32 -replace '~', [string][char]59)

New-Item -ItemType Directory -Force $OutDir | Out-Null
$result = [ordered]@{
    label    = $Label
    started  = (Get-Date).ToString('s')
    locked   = $false
    prompts  = @()
    tabs     = @()
    ribbonShot = $null
    ended    = $null
    reason   = $null
}
function Save-Result {
    $result.ended = (Get-Date).ToString('s')
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutDir 'result.json') -Encoding utf8
}

if (Get-Process LogonUI -ErrorAction SilentlyContinue) {
    $result.locked = $true
    $result.reason = 'screen is locked, nothing started'
    Save-Result
    return
}
if (Get-Process Revit -ErrorAction SilentlyContinue) {
    $result.reason = 'a Revit process already runs, nothing started'
    Save-Result
    return
}

$UIA = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]
function Prop($property, $value) {
    New-Object System.Windows.Automation.PropertyCondition ($property), $value
}

# A rectangle of the screen, so a dialog is taken with nothing else on the page
function Shot-Rect($rect, $path) {
    $w = [int]$rect.Width
    $h = [int]$rect.Height
    if ($w -le 0 -or $h -le 0) {
        return $false
    }
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, (New-Object System.Drawing.Size $w, $h))
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return $true
}

# The main window by its own handle, whatever lies over it
function Shot-Window($hwnd, $path, [switch]$RibbonOnly) {
    $r = New-Object Sa.Win+RECT
    [void][Sa.Win]::GetWindowRect([IntPtr]$hwnd, [ref]$r)
    $w = $r.Right - $r.Left
    $h = $r.Bottom - $r.Top
    if ($w -le 0 -or $h -le 0) {
        return $false
    }
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [void][Sa.Win]::PrintWindow([IntPtr]$hwnd, $hdc, 2)
    $g.ReleaseHdc($hdc)
    $g.Dispose()
    if ($RibbonOnly) {
        $c = $bmp.Clone([System.Drawing.Rectangle]::new(0, 0, $w, [Math]::Min(190, $h)), $bmp.PixelFormat)
        $bmp.Dispose()
        $bmp = $c
    }
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return $true
}

function Texts($el) {
    $names = foreach ($d in $el.FindAll($Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
        $n = $d.Current.Name
        if ($n) { $n }
    }
    return @($names | Select-Object -Unique)
}

function Candidates($pidValue) {
    $inner = Prop $UIA::ControlTypeProperty $CT::Window
    foreach ($top in $UIA::RootElement.FindAll($Scope::Children, (Prop $UIA::ProcessIdProperty $pidValue))) {
        $top
        foreach ($child in $top.FindAll($Scope::Children, $inner)) { $child }
    }
}

function Find-Button($win, $label) {
    foreach ($part in $win.FindAll($Scope::Descendants, (Prop $UIA::NameProperty $label))) {
        if ($part.Current.ControlType -eq $CT::Button -or $part.Current.ClassName -eq 'CCPushButton') {
            return $part
        }
    }
    return $null
}

# Presses the button by Invoke, or by a click message to its own window. Returns how it was pressed, or why it was not
function Press-Button($button, $label) {
    $pattern = $null
    if ($button.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
        return "$label (invoke)"
    }
    if ($button.Current.NativeWindowHandle -ne 0) {
        [void][Sa.Win]::PostMessage([IntPtr]$button.Current.NativeWindowHandle, 0xF5, [IntPtr]::Zero, [IntPtr]::Zero)
        return "$label (click message)"
    }
    return "$label takes no click"
}

# Logs one box, takes its screenshot, answers it by the name of the button and returns the entry
function Add-Prompt($win, $texts, $ours, $want) {
    $n = $result.prompts.Count + 1
    $shotName = ('prompt-{0:00}.png' -f $n)
    $shot = Shot-Rect $win.Current.BoundingRectangle (Join-Path $OutDir $shotName)
    $entry = [ordered]@{
        n        = $n
        at       = [math]::Round(((Get-Date) - $start).TotalSeconds, 1)
        title    = ($texts | Select-Object -First 1)
        detail   = ($texts | Where-Object { $_ -like 'Name:*' } | Select-Object -First 1)
        ours     = $ours
        shot     = $(if ($shot) { $shotName } else { $null })
        answered = 'not answered'
    }
    if ($Answer -eq 'answer') {
        $button = Find-Button $win $want
        $entry.answered = if ($button) { Press-Button $button $want } else { "no button named $want, left open" }
    }
    $result.prompts += $entry
}

$startArgs = @{
    FilePath = 'C:\Program Files\Autodesk\Revit 2024\Revit.exe'
    PassThru = $true
}
if ($Model) {
    $startArgs.ArgumentList = '"' + $Model + '"'
    $result.model = Split-Path $Model -Leaf
}
$proc = Start-Process @startArgs
$result.pid = $proc.Id
$start = Get-Date
$handled = @{}
$seen = @{}
$result.windowsSeen = @()
$lastEvent = Get-Date
$mainSeen = $false
try {
    while (((Get-Date) - $start).TotalSeconds -lt $MaxSeconds) {
        Start-Sleep -Milliseconds 700
        if ($proc.HasExited) {
            $result.reason = 'Revit exited by itself'
            break
        }
        if (Get-Process LogonUI -ErrorAction SilentlyContinue) {
            $result.locked = $true
            $result.reason = 'screen locked during the run'
            break
        }
        # A window that closes while it is read raises an error, that pass is skipped and the next one looks again
        $found = @()
        try { $found = @(Candidates $proc.Id) } catch { continue }
        foreach ($win in $found) {
            try {
                $key = $win.GetRuntimeId() -join '.'
                if (-not $seen.ContainsKey($key)) {
                    $seen[$key] = $true
                    $result.windowsSeen += [ordered]@{
                        at    = [math]::Round(((Get-Date) - $start).TotalSeconds, 1)
                        name  = $win.Current.Name
                        class = $win.Current.ClassName
                    }
                }
                if ($handled.ContainsKey($key)) { continue }
                $load = Find-Button $win 'Load Once'
                if (-not $load -and $win.Current.Name -like 'Security*') {
                    # Another security box, for example Invalid Signature. Not the tool's, so only Do Not Load by name, else it stays open and is logged
                    $handled[$key] = $true
                    Add-Prompt $win (Texts $win) $false 'Do Not Load'
                    $lastEvent = Get-Date
                    continue
                }
                if (-not $load) {
                    # The splash is also named Autodesk Revit 2024.3, so the main window counts as up once it holds ribbon tabs
                    if ($Model -and -not $mainSeen) {
                        $proc.Refresh()
                        if ($proc.MainWindowTitle -like ('*' + [IO.Path]::GetFileNameWithoutExtension($Model) + '*')) {
                            $mainSeen = $true
                            $lastEvent = Get-Date
                            $result.mainSeenAt = [math]::Round(((Get-Date) - $start).TotalSeconds, 1)
                        }
                    }
                    if (-not $mainSeen -and @($win.FindAll($Scope::Descendants, (Prop $UIA::ControlTypeProperty $CT::TabItem))).Count -gt 0) {
                        $mainSeen = $true
                        $lastEvent = Get-Date
                        $result.mainSeenAt = [math]::Round(((Get-Date) - $start).TotalSeconds, 1)
                    }
                    continue
                }
                $handled[$key] = $true
                $texts = Texts $win
                $detail = $texts | Where-Object { $_ -like 'Name:*' } | Select-Object -First 1
                $ours = [bool]($detail -match 'SiteAdaptation')
                Add-Prompt $win $texts $ours $(if ($ours) { 'Load Once' } else { 'Do Not Load' })
                $lastEvent = Get-Date
                if ($Answer -eq 'none') {
                    $result.reason = 'first prompt left unanswered'
                    break
                }
            }
            catch {
                $result.skippedReads = 1 + [int]$result.skippedReads
            }
        }
        if ($result.reason) { break }
        if ($mainSeen -and (((Get-Date) - $lastEvent).TotalSeconds -ge $IdleSeconds)) {
            $result.reason = "ribbon up and $IdleSeconds s with no new prompt"
            break
        }
    }
    if (-not $result.reason) {
        $result.reason = "time limit $MaxSeconds s"
    }

    # The main window by its process handle, maximised so every ribbon tab shows and none hides under the overflow arrow
    $mainProc = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    if ($mainProc -and $mainProc.MainWindowHandle -ne 0) {
        $h = $mainProc.MainWindowHandle
        $result.mainWindow = $mainProc.MainWindowTitle
        [void][Sa.Win]::ShowWindow($h, 3)
        Start-Sleep -Seconds 3
        $root = $UIA::FromHandle($h)
        # The tab of this tool is found by its name and selected by a pattern when it offers one, so the shot shows its buttons
        $tab = $root.FindFirst($Scope::Descendants, (Prop $UIA::NameProperty 'Site Adaptation'))
        $result.toolTabFound = [bool]$tab
        if ($tab) {
            $selection = $null
            $invoke = $null
            if ($tab.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selection)) {
                $selection.Select()
                $result.toolTabSelected = 'select pattern'
            }
            elseif ($tab.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) {
                $invoke.Invoke()
                $result.toolTabSelected = 'invoke pattern'
            }
            else {
                $result.toolTabOffscreen = $tab.Current.IsOffscreen
                $result.toolTabSelected = 'no pattern, not selected'
            }
            Start-Sleep -Seconds 2
        }
        if (Shot-Window $h (Join-Path $OutDir 'main.png')) { $result.mainShot = 'main.png' }
        if (Shot-Window $h (Join-Path $OutDir 'ribbon.png') -RibbonOnly) { $result.ribbonShot = 'ribbon.png' }
    }
}
finally {
    # Only the process this script started. A Revit the person opened is never closed or killed here
    if (-not $proc.HasExited) {
        try { [void]$proc.CloseMainWindow() } catch { }
        if (-not $proc.WaitForExit(15000)) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            [void]$proc.WaitForExit(10000)
        }
    }
    $result.revitLeftOpen = -not $proc.HasExited
    Save-Result
}
