# R313. The prototype sets of config/project.json an upgrade of Run-TestHost.ps1 may take besides the ARC prototypes of the plan,
# such as the STR prototypes the SE models link. A set names its source folder and its target folder under inputRoot, the
# discipline its file names hold and the letter its job codes start with. The steps of the upgrade job of R109 do not change.

$SetFields = @('source', 'target', 'discipline', 'codePrefix')
$NameFields = @('prefix', 'discipline', 'suffix')

# prototypeNames of config/project.json, every field there and nothing else: the parts every prototype file name is made of,
# <prefix><discipline>-<code><suffix><n>.rvt, with the discipline of the prototypes of the plan. No part of a name sits in the code
function Get-PrototypeNames($Config) {
    $names = $Config.prototypeNames
    if (-not $names) {
        throw 'config/project.json has no prototypeNames'
    }
    $fields = @($names.PSObject.Properties.Name)
    if ($fields.Count -ne $NameFields.Count -or @($NameFields | Where-Object { $fields -cnotcontains $_ }).Count -gt 0) {
        throw "prototypeNames holds exactly $($NameFields -join ', ')"
    }
    if ([string]$names.prefix -cnotmatch '^[A-Z0-9]+(-[A-Z0-9]+)*-$') {
        throw "The prefix of prototypeNames is capitals and digits in parts that each end with -: $($names.prefix)"
    }
    if ([string]$names.discipline -cnotmatch '^[A-Z]{3}$') {
        throw "The discipline of prototypeNames is three capitals: $($names.discipline)"
    }
    if ([string]$names.suffix -cnotmatch '^-[A-Z0-9]+(-[A-Z0-9]+)*-$') {
        throw "The suffix of prototypeNames starts and ends with - with capitals and digits between: $($names.suffix)"
    }
    return [pscustomobject][ordered]@{
        prefix     = [string]$names.prefix
        discipline = [string]$names.discipline
        suffix     = [string]$names.suffix
    }
}

# The parts of a path under inputRoot, refused with a drive, a parent step, an empty part or a blank around a part
function Get-PlainParts([string]$Text, [string]$What) {
    $parts = @($Text -split '[\\/]')
    $bad = @($parts | Where-Object { $_ -eq '' -or $_ -eq '.' -or $_ -eq '..' -or $_ -ne $_.Trim() })
    if (-not $Text -or $Text.Contains(':') -or $bad.Count -gt 0) {
        throw "$What is no plain path under inputRoot: $Text"
    }
    return , $parts
}

# A plain path under inputRoot as Windows opens it, resolved by Windows itself under a root that is never read, so a trailing dot
# or blank or either slash never hides one place behind two texts. The case stays as written, a part Windows takes away whole is refused
function Get-OpenedPath([string]$Text, [string]$What) {
    $parts = Get-PlainParts $Text $What
    $root = 'C:\opened-root\'
    $full = [IO.Path]::GetFullPath($root + ($parts -join '\'))
    $opened = if ($full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { $full.Substring($root.Length).TrimEnd('\') } else { '' }
    if (@($opened -split '\\').Count -ne $parts.Count -or @($opened -split '\\' | Where-Object { $_ -eq '' }).Count -gt 0) {
        throw "$What names the folder above its last part, Windows takes that part away: $Text"
    }
    return $opened
}

# The place a path opens, in any case as Windows matches names
function Get-PlaceKey([string]$Text, [string]$What) {
    return (Get-OpenedPath $Text $What).ToUpperInvariant()
}

# True when the place Inner is the place Outer or a folder inside it
function Test-Within([string]$Inner, [string]$Outer) {
    return $Inner -eq $Outer -or $Inner.StartsWith($Outer + '\', [StringComparison]::Ordinal)
}

# The set of that name, its name matched in its case, every field there and nothing else. Its target is a folder of its own, compared
# by the place Windows opens: never its source, a folder above it or inside it, never the ARC prototype folder of the past job, a folder
# above it or inside it, and never the upgraded ARC prototype folder or a folder above it, so the ARC prototypes the placer takes by
# code stay alone. A folder inside the upgraded one is a folder of its own, since nothing reads the folders inside it. The set comes
# back with its folders as Windows opens them, so the run script writes no trailing dot into a job
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
    $source = Get-PlaceKey $set.source "The source of the prototype set $Name"
    $target = Get-PlaceKey $set.target "The target of the prototype set $Name"
    $names = Get-PrototypeNames -Config $Config
    if ([string]$set.discipline -cnotmatch '^[A-Z]{3}$' -or [string]$set.discipline -ceq $names.discipline) {
        throw "The discipline of the prototype set $Name is three capitals other than $($names.discipline): $($set.discipline)"
    }
    if ([string]$set.codePrefix -cnotmatch '^[A-Z]$') {
        throw "The code prefix of the prototype set $Name is one capital letter: $($set.codePrefix)"
    }
    if ((Test-Within $target $source) -or (Test-Within $source $target)) {
        throw "The target of the prototype set $Name is its source folder, a folder above it or inside it, the source is never overwritten: $($set.target)"
    }
    $past = Get-PlaceKey $Config.pastJob.prototypeFolder 'pastJob.prototypeFolder'
    $upgraded = Get-PlaceKey $Config.pastJob.upgradedPrototypeFolder 'pastJob.upgradedPrototypeFolder'
    if ((Test-Within $target $past) -or (Test-Within $past $target) -or (Test-Within $upgraded $target)) {
        throw "The target of the prototype set $Name is a prototype folder of the past job or a folder above it, it needs a folder of its own: $($set.target)"
    }
    return [pscustomobject]@{
        source     = Get-OpenedPath $set.source "The source of the prototype set $Name"
        target     = Get-OpenedPath $set.target "The target of the prototype set $Name"
        discipline = [string]$set.discipline
        codePrefix = [string]$set.codePrefix
    }
}

# The job code of a set, its prefix and the prototype code, so the outputs of a set never move those of the ARC upgrade of that code
function Get-SetJobCode($Set, [string]$Code) {
    $job = [string]$Set.codePrefix + $Code
    if ($job -cnotmatch '^[A-Z][A-Z0-9]{1,3}$') {
        throw "The job code $job is not 2 to 4 capitals and digits, the job file names of R110 take no other"
    }
    return $job
}

# The one .rvt file right in the source folder named <prefix><discipline>-<code><suffix><n>.rvt by the parts of Get-PrototypeNames,
# the name the guard of the test host checks. None or two are refused, nothing is guessed
function Get-SetPrototype([string]$Folder, $Names, [string]$Discipline, [string]$Code) {
    $pattern = '^' + [regex]::Escape($Names.prefix) + [regex]::Escape($Discipline) + '-' + [regex]::Escape($Code) + [regex]::Escape($Names.suffix) + '\d+\.rvt$'
    $files = @(Get-ChildItem -LiteralPath $Folder -File | Where-Object { $_.Name -match $pattern })
    if ($files.Count -ne 1) {
        throw "The prototype folder $Folder holds $($files.Count) files for $Code, one is needed: $(($files | ForEach-Object Name) -join ', ')"
    }
    return $files[0].Name
}
