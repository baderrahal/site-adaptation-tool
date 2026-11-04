# Notification. Writes the lane and "waiting for you" with the time to steps/STATUS.md and shows a Windows alert.
# The alert runs in its own hidden process, so the hook never waits on it.
. (Join-Path $PSScriptRoot 'lane.ps1')

$payload = Read-HookInput
$root = Get-RepoRoot (Get-HookFolder $payload)
if (-not $root) { exit 0 }
$lane = Get-LaneName $root
$time = Get-Date -Format 'yyyy-MM-dd HH:mm'
$line = "- $lane waiting for you, $time"
$status = Get-StatusPath $root

$heading = '## Waiting for you'
$lines = @()
if (Test-Path -LiteralPath $status) { $lines = @(Get-Content -LiteralPath $status -Encoding UTF8) }
$escaped = [regex]::Escape($lane)
$lines = @($lines | Where-Object { $_ -notmatch "^- $escaped waiting for you, " })
$at = [array]::IndexOf($lines, $heading)
if ($at -lt 0) {
    $lines = @($heading, $line, '') + $lines
}
else {
    $before = @($lines[0..$at])
    $after = @()
    if ($at + 1 -lt $lines.Count) { $after = @($lines[($at + 1)..($lines.Count - 1)]) }
    $lines = $before + @($line) + $after
}
$folder = Split-Path -Parent $status
if (Test-Path -LiteralPath $folder) { [IO.File]::WriteAllLines($status, [string[]]$lines, (New-Object Text.UTF8Encoding $false)) }

if ($env:SITE_ADAPTATION_HOOK_NO_ALERT) { exit 0 }

$alert = @"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
`$icon = New-Object System.Windows.Forms.NotifyIcon
`$icon.Icon = [System.Drawing.SystemIcons]::Information
`$icon.Visible = `$true
`$icon.ShowBalloonTip(10000, 'Site Adaptation Tool', '$lane waiting for you, $time', [System.Windows.Forms.ToolTipIcon]::Info)
Start-Sleep -Seconds 11
`$icon.Dispose()
"@
$encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($alert))
Start-Process powershell -WindowStyle Hidden -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encoded) | Out-Null
exit 0
