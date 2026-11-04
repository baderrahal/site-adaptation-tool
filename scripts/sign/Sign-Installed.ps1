#Requires -Version 7
# SETUP-sign. OPTIONAL and OFF BY DEFAULT. No install script calls this, nothing signs unless a person runs it by hand.
#
# What it does: makes the self-signed code signing certificate of the tool when none exists (key not exportable, in the personal store of
# the current user, no .pfx) and signs every DLL and EXE of the tool under the add-ins root with it, a file of another maker is left alone.
#
# THE TRUST STEP, read before you run it. Revit does NOT accept a self-made certificate by Trusted Publishers alone. Measured on this PC
# (steps/revit-results/SETUP-sign.md): with the certificate in Trusted Publishers of the current user and every file signed, Revit 2024 showed
# "Security - Invalid Signature" at the start and no Load Once box. That is WORSE than the plain Load Once prompt. The certificate must also
# be trusted as a root, in Trusted Root Certification Authorities. This script never does that. It is a decision of the person who owns the PC,
# the sandbox of Claude Code refused it ([Security Weaken]) and company policy may refuse it. Until that is decided leave this script alone.
# If the certificate was put in Trusted Publishers and the root step is not wanted, take it away again with -Untrust.
#
# Never commit the output, the private key stays in the store and the .cer goes under the work root
param(
    [string]$AddinsRoot = (Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024'),
    [string]$CerFolder = '',
    # Puts the public certificate into Trusted Publishers of the current user. Needed with the root step above and not enough without it
    [switch]$TrustPublishers,
    [switch]$Untrust,
    # The person says they read the trust step in the header
    [switch]$IUnderstandInvalidSignatureWithoutRootTrust
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SigningCertificate.ps1')

if ($Untrust) {
    $cert = Get-SigningCertificate
    if ($cert) { Remove-SigningTrust $cert; 'Taken out of Trusted Publishers: ' + $cert.Thumbprint } else { 'No certificate of the tool in the personal store.' }
    return
}
if (-not $IUnderstandInvalidSignatureWithoutRootTrust) {
    throw 'Signing without the root trust step makes Revit show Invalid Signature. Read the header of this script, then pass -IUnderstandInvalidSignatureWithoutRootTrust.'
}
if (Get-Process -Name Revit -ErrorAction SilentlyContinue) { throw 'Revit is running. Close it first.' }

$cert = New-SigningCertificate
if ($TrustPublishers) {
    if (-not $CerFolder) { throw '-TrustPublishers needs -CerFolder, a folder under the work root for the public .cer' }
    'Added to Trusted Publishers now: ' + (Add-SigningTrust $cert $CerFolder)
}
$files = foreach ($folder in Get-ChildItem -LiteralPath $AddinsRoot -Directory | Where-Object { $_.Name -like 'SiteAdaptation*' }) {
    Get-ChildItem -LiteralPath $folder.FullName -Recurse -File | Where-Object { $_.Extension -in '.dll', '.exe' } | ForEach-Object FullName
}
Sign-Files $cert @($files) | Group-Object State | Select-Object Name, Count | Format-Table | Out-String | Write-Output
