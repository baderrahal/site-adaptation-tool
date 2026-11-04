# SETUP-sign, docs/design/08-install.md. The one self-made code signing certificate of the tool and the functions that sign with it.
# Dot-sourced by Sign-Installed.ps1, which is optional and off by default. It uses the certificate cmdlets only and reads and writes no
# registry key (R106). The private key stays in the certificate store of the current user as a key that cannot be exported, so no .pfx
# exists. Only the public .cer is written, under the work root, never into the repo.
# Revit does not trust this certificate until it is also in Trusted Root, see the header of Sign-Installed.ps1.

$script:SigningSubject = 'CN=Site Adaptation Tool self-signed code signing'

# The certificate of the tool in the personal store of the current user, the newest valid one, or $null
function Get-SigningCertificate {
    Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
        Where-Object { $_.Subject -eq $script:SigningSubject -and $_.NotAfter -gt (Get-Date).AddDays(30) -and $_.HasPrivateKey } |
        Sort-Object NotAfter -Descending | Select-Object -First 1
}

# Makes the certificate when none is valid, and returns it
function New-SigningCertificate {
    $cert = Get-SigningCertificate
    if ($cert) { return $cert }
    New-SelfSignedCertificate -Type CodeSigningCert -Subject $script:SigningSubject -FriendlyName 'Site Adaptation Tool' `
        -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -KeyUsage DigitalSignature `
        -NotAfter (Get-Date).AddYears(5) -CertStoreLocation Cert:\CurrentUser\My
}

# Trusted Publishers of the current user holds the certificate with its public part only. Returns $true when it was added now
function Add-SigningTrust([Parameter(Mandatory)]$Certificate, [Parameter(Mandatory)][string]$CerFolder) {
    New-Item -ItemType Directory -Force $CerFolder | Out-Null
    $cer = Join-Path $CerFolder 'SiteAdaptationSigning.cer'
    [void](Export-Certificate -Cert $Certificate -FilePath $cer -Type CERT -Force)
    if (Test-Path (Join-Path 'Cert:\CurrentUser\TrustedPublisher' $Certificate.Thumbprint)) { return $false }
    [void](Import-Certificate -FilePath $cer -CertStoreLocation Cert:\CurrentUser\TrustedPublisher)
    return $true
}

function Remove-SigningTrust([Parameter(Mandatory)]$Certificate) {
    $path = Join-Path 'Cert:\CurrentUser\TrustedPublisher' $Certificate.Thumbprint
    if (Test-Path $path) { Remove-Item $path -Force }
}

# Signs each file with the certificate. A file that carries a signature of another maker, with any status, is never signed over. A file that
# carries a signature of this certificate is left as it is unless the signature no longer fits the file. Returns one row per file
function Sign-Files([Parameter(Mandatory)]$Certificate, [Parameter(Mandatory)][string[]]$Path) {
    foreach ($file in $Path) {
        $before = Get-AuthenticodeSignature -LiteralPath $file
        $signer = $before.SignerCertificate
        if ($signer -and $signer.Thumbprint -ne $Certificate.Thumbprint) {
            $state = 'left, signed by another maker'
        }
        elseif ($signer -and $before.Status -ne 'HashMismatch') {
            # A self-signed certificate outside Trusted Root reports UnknownError or NotTrusted, the file is still signed by it
            $state = 'already signed'
        }
        else {
            $after = Set-AuthenticodeSignature -LiteralPath $file -Certificate $Certificate -HashAlgorithm SHA256
            $state = if ($after.SignerCertificate) { 'signed' } else { 'FAILED ' + $after.StatusMessage }
        }
        [pscustomobject]@{
            File  = $file
            State = $state
        }
    }
}
