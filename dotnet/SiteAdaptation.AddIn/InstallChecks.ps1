# R370, docs/design/08-install.md. The checks Install-SiteAdaptation.ps1 and Install-TestHost.ps1 share, dot-sourced by both, so one
# Common is checked the same way in every folder of the tool whichever script ran last. It reads and writes no registry key (R106)

# Every SiteAdaptation.Common.dll in a folder of the tool, a folder under the add-ins root whose name starts with SiteAdaptation
function Get-ToolCommons([string]$AddinsRoot) {
    @(Get-ChildItem -LiteralPath $AddinsRoot -Directory | Where-Object { $_.Name -like 'SiteAdaptation*' } |
        ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Recurse -File -Filter 'SiteAdaptation.Common.dll' })
}

# The paths of the Commons of the tool whose SHA-256 is not the one of the build
function Get-StaleCommons([string]$AddinsRoot, [string]$Built) {
    @(Get-ToolCommons $AddinsRoot | Where-Object { (Get-FileHash -LiteralPath $_.FullName).Hash -ne $Built } | ForEach-Object { $_.FullName })
}

# Each running process named Revit with its path and the major part of its product version. Revit 2023, 2024 and 2025 all run as
# Revit.exe, so the name alone does not tell them apart
function Get-RevitProcesses {
    @(Get-Process -Name Revit -ErrorAction SilentlyContinue | ForEach-Object {
            $path = $_.Path
            $major = $null
            if ($path -and (Test-Path -LiteralPath $path)) {
                $major = (Get-Item -LiteralPath $path).VersionInfo.ProductMajorPart
            }
            [pscustomobject]@{ Path = $path; Major = $major }
        })
}

# The Revit 2024 processes of a list: version 24, as Run-TestHost.ps1 finds Revit 2024 by its own version, or a path under a
# Revit 2024 folder. A process Windows gives neither of counts as Revit 2024, since nothing shows it is another one. An empty list
# reaches the function as null, which is no process
function Select-Revit2024($Processes) {
    @($Processes | Where-Object { $null -ne $_ } | Where-Object { $_.Major -eq 24 -or ($_.Path -and $_.Path -like '*\Revit 2024\*') -or (-not $_.Path -and $null -eq $_.Major) })
}
