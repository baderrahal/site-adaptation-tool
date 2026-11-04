---
id: SETUP-sign
lane: revit-test
stage: rules
status: done
allowed:
  - scripts/sign/
  - dotnet/SiteAdaptation.RevitTests/
  - dotnet/SiteAdaptation.sln
  - dotnet/SiteAdaptation.AddIn/Install-SiteAdaptation.ps1
  - dotnet/SiteAdaptation.AddIn/InstallChecks.ps1
  - dotnet/SiteAdaptation.AddIn/SiteAdaptation.AddIn.csproj
  - dotnet/SiteAdaptation.TestHost/Install-TestHost.ps1
  - dotnet/SiteAdaptation.TestHost/Run-TestHost.ps1
  - dotnet/SiteAdaptation.TestHost/Start-RevitListener.ps1
  - dotnet/SiteAdaptation.Tests/R448_RevitListenerTests.cs
  - dotnet/SiteAdaptation.TestHost/TestHostPaths.ps1
  - dotnet/SiteAdaptation.Tests/InstallScriptTests.cs
  - dotnet/SiteAdaptation.Tests/AddInTests.cs
  - dotnet/SiteAdaptation.Tests/RevitRunTests.cs
  - docs/design/08-install.md
  - docs/licences.md
  - steps/revit-results/
---

# Scope

Stop Revit's Load Once prompts, they froze runs with no one watching, then add one real Revit test.

Bader sees many Load Once clicks for one tool. Find out why, then make it one tool and one click at most, ideally none.

1. Find out why. List every .addin file in the Revit 2024 add-in folders, %APPDATA% and ProgramData, and every entry and DLL each names. Mark which the install of this tool puts there. Start Revit cold, with no document open, count the prompts and take a screenshot of each.
2. Make it one tool: one .addin file, one entry DLL, every DLL the tool ships signed with the same self-made certificate, the certificate in Windows Trusted Publishers. Update the install script to do this, and to take away what an older install left.
3. Add one ricaun.RevitTest test in a new project dotnet/SiteAdaptation.RevitTests. It opens a copy of a demo model and counts floors.

What Lead read before this task, measure it again: %APPDATA%\Autodesk\Revit\Addins\2024 holds 4 .addin files of this tool, SiteAdaptation_Revit2024 (RevitPrototypeReader.dll), SiteAdaptationAddIn_Revit2024 (SiteAdaptation.AddIn.dll), SiteAdaptationPlacer_Revit2024 (SiteAdaptation.Placer.dll) and SiteAdaptationTestHost_Revit2024 (SiteAdaptation.TestHost.dll), each its own assembly and so its own prompt. Only the second is the tool. The other 3 are for developers, so the test host, the placer and the reader must install for a run and be gone after it, and their runs must still work. dotnet/SiteAdaptation.DialogWatcher answers Load Once for them today and stays as a guard. dotnet/RevitPrototypeReader itself stays unchanged. Add-ins of other makers in those folders are not this tool's, list them and never touch them.

Out of scope: any change to what the add-in does. Signing of a build for another PC.

# Facts to measure first

- UNKNOWN: whether this company PC lets a user add to Trusted Publishers, and whether a self-signed certificate must also sit in Trusted Root for Revit to trust it. Measure on the certificate store of the current user first. If policy stops it, stop and report the exact error.
- The registry hook blocks any registry command. Use the certificate cmdlets, never a registry edit.
- Check the licence of ricaun.RevitTest and record it in docs/licences.md before adding it.
- The new test project opens Revit, so keep it out of the plain `dotnet test` of the solution and of GitHub. Say in the result how that was done.

# Rules

- The private key and any .pfx stay outside the repo, under the work root. Never commit one.
- Take the shared lock through python/window_lock.py. At most 3 Revit or Civil 3D windows.
- Run only on a copy of a demo model, under the work root. C:\SA-demo\SA-dryrun3nov.rvt is the model with no floors, and its copy with floors is SA-dryrun3nov-done.rvt. Use the second for a count above zero. Never commit a model.
- A locked screen stops the check, say so if it happens.

# Acceptance tests

None in tests/acceptance, this needs Revit and GitHub has none. The check below is the acceptance.

# Done when

- The list of .addin files and the count of prompts before the change, with its screenshots.
- After the install script ran: one .addin file of this tool, one entry DLL, every DLL it ships signed by the one certificate, checked by Get-AuthenticodeSignature on each, a count of signed and unsigned.
- A cold Revit 2024 start with the signed add-in: a screenshot showing zero prompts and a screenshot of the add-in ribbon loaded, taken by the run. A person seeing it counts too. Add-ins of other makers may still prompt, name each one and say so.
- Remove the certificate from Trusted Publishers, start again and see the prompt return, so the test proves the certificate is what stopped it. Put it back.
- The ricaun test passes and its floor count matches a second count taken another way, such as the add-in's own report or a schedule. Give both numbers.

# Revit check needed

All of the above. Write it to steps/revit-results/SETUP-sign.md.
