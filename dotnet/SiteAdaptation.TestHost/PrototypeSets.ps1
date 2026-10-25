# R313. The prototype sets of config/project.json an upgrade of Run-TestHost.ps1 may take besides the ARC prototypes of the plan,
# such as the STR prototypes the SE models link. A set names its source folder and its target folder under inputRoot, the
# discipline its file names hold and the letter its job codes start with. The steps of the upgrade job of R109 do not change.

$SetFields = @('source', 'target', 'discipline', 'codePrefix')

# The parts of a path under inputRoot, refused with a drive, a parent step, an empty part or a blank around a part
function Get-PlainParts([string]$Text, [string]$What) {
    $parts = @($Text -split '[\\/]')
    $bad = @($parts | Where-Object { $_ -eq '' -or $_ -eq '.' -or $_ -eq '..' -or $_ -ne $_.Trim() })
    if (-not $Text -or $Text.Contains(':') -or $bad.Count -gt 0) {
        throw "$What is no plain path under inputRoot: $Text"
    }
    return , ($parts | ForEach-Object { $_.ToLowerInvariant() })
}

function Test-SamePath([string]$One, [string]$Two) {
    return ((Get-PlainParts $One 'a folder') -join '\') -eq ((Get-PlainParts $Two 'a folder') -join '\')
}

# The set of that name, its name matched in its case, every field there and nothing else. Its target is a folder of its own, never
# its source and never a prototype folder of the past job, so the ARC prototypes the placer takes by code stay alone
function Get-PrototypeSet($Config, [string]$Name) {
    $sets = $Config.prototypeSets
    if (-not $sets -or -not (@($sets.PSObject.Properties.Name) -ccontains $Name)) {
        throw "config/project.json has no prototype set $Name in prototypeSets"
    }
    $set = $sets.$Name
    $fields = @($set.PSObject.Properties.Name)
    foreach ($field in $SetFields) {
        if (-not ($fields -ccontains $field) -or -not [string]$set.$field) {
            throw "The prototype set $Name names no $field"
        }
    }
    if ($fields.Count -ne $SetFields.Count) {
        throw "The prototype set $Name holds exactly $($SetFields -join ', ')"
    }
    foreach ($field in 'source', 'target') {
        Get-PlainParts $set.$field "The $field of the prototype set $Name" | Out-Null
    }
    if ([string]$set.discipline -cnotmatch '^[A-Z]{3}$' -or [string]$set.discipline -ceq 'ARC') {
        throw "The discipline of the prototype set $Name is three capitals other than ARC: $($set.discipline)"
    }
    if ([string]$set.codePrefix -cnotmatch '^[A-Z]$') {
        throw "The code prefix of the prototype set $Name is one capital letter: $($set.codePrefix)"
    }
    if (Test-SamePath $set.target $set.source) {
        throw "The target of the prototype set $Name is its source folder, the source is never overwritten"
    }
    foreach ($past in @($Config.pastJob.prototypeFolder, $Config.pastJob.upgradedPrototypeFolder)) {
        if (Test-SamePath $set.target $past) {
            throw "The target of the prototype set $Name is a prototype folder of the past job, it needs a folder of its own: $($set.target)"
        }
    }
    return $set
}

# The job code of a set, its prefix and the prototype code, so the outputs of a set never move those of the ARC upgrade of that code
function Get-SetJobCode($Set, [string]$Code) {
    $job = [string]$Set.codePrefix + $Code
    if ($job -cnotmatch '^[A-Z][A-Z0-9]{1,3}$') {
        throw "The job code $job is not 2 to 4 capitals and digits, the job file names of R110 take no other"
    }
    return $job
}

# The one .rvt file right in the source folder named RRE-WP-NSP-KEO-<discipline>-<code>-ZZ-MOD-<n>.rvt, the name the guard of the
# test host checks. None or two are refused, nothing is guessed
function Get-SetPrototype([string]$Folder, [string]$Discipline, [string]$Code) {
    $pattern = '^RRE-WP-NSP-KEO-' + [regex]::Escape($Discipline) + '-' + [regex]::Escape($Code) + '-ZZ-MOD-\d+\.rvt$'
    $files = @(Get-ChildItem -LiteralPath $Folder -File | Where-Object { $_.Name -match $pattern })
    if ($files.Count -ne 1) {
        throw "The prototype folder $Folder holds $($files.Count) files for $Code, one is needed: $(($files | ForEach-Object Name) -join ', ')"
    }
    return $files[0].Name
}
