# Trusted Root, by hand, for Bader

For task SETUP-sign-2. This is your choice and only you do it. No session of Claude will run these steps, a security check refused it and that refusal stands.

## What this does and what it risks

Revit takes a self-made certificate only if Windows trusts it as a root. A signed add-in then loads with no prompt. After this your Windows user trusts anything signed by this certificate, for any purpose, not only Revit. The private key cannot be exported and sits only on this PC under your user. A company PC may forbid it, and Windows may refuse. Whether Revit then takes the certificate is not yet known, it has not been tried.

The other two routes need none of this: press Always Load once by hand, or ask IT for a certificate the company already trusts.

## Add it

1. In VS Code, open the folder of the repo.
2. Click Terminal, then New Terminal.
3. Type this and press Enter, it starts Windows PowerShell:

```
powershell
```

4. Type this and press Enter, it shows the certificate file is there:

```
Test-Path "$HOME\Documents\SiteAdaptation\setup-sign\cer\SiteAdaptationSigning.cer"
```

5. Check it printed `True`. If it printed `False`, stop and tell Lead.
6. Type this and press Enter:

```
Import-Certificate -FilePath "$HOME\Documents\SiteAdaptation\setup-sign\cer\SiteAdaptationSigning.cer" -CertStoreLocation Cert:\CurrentUser\Root
```

7. A Windows box titled Security Warning asks if you want to install the certificate. Read it, then click Yes to go on, or No to stop here with nothing changed.
8. Type this and press Enter, it shows what is now trusted:

```
Get-ChildItem Cert:\CurrentUser\Root | Where-Object Subject -like '*Site Adaptation*' | Select-Object Subject, Thumbprint
```

9. Check it printed one row with the subject Site Adaptation Tool self-signed code signing. If it printed nothing, policy stopped it, tell Lead the exact error.
10. Tell Lead the certificate is in. The revit-test lane then signs the add-in and measures a cold start. Until then nothing changes, the files are still unsigned.

## What you should see after the lane has signed

- Revit 2024 starts with no Load Once box for Site Adaptation, and the ribbon tab Site Adaptation is there.
- Add-ins of other makers may still ask. They are not this tool.
- If a box says Invalid Signature, it did not work, press Do Not Load and tell Lead.

## Take it out again

1. In VS Code, click Terminal, then New Terminal.
2. Type this and press Enter:

```
powershell
```

3. Type this and press Enter, then click Yes in the Windows box:

```
Get-ChildItem Cert:\CurrentUser\Root | Where-Object Subject -like '*Site Adaptation*' | Remove-Item
```
