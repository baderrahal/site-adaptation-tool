---
id: SETUP-sign
lane: revit-test
stage: rules
status: open
allowed:
  - scripts/sign/
  - dotnet/SiteAdaptation.RevitTests/
  - dotnet/SiteAdaptation.sln
  - dotnet/SiteAdaptation.AddIn/Install-SiteAdaptation.ps1
  - dotnet/SiteAdaptation.AddIn/SiteAdaptation.AddIn.csproj
  - docs/licences.md
  - steps/revit-results/
---

# Scope

Stop Revit's Load Once prompt, it froze runs with no one watching, then add one real Revit test.

1. Make a self-signed code signing certificate on this PC. Sign the add-in DLLs with it after the Release build. Add the certificate to Windows Trusted Publishers, so Revit 2024 loads the add-in with no prompt.
2. Add one ricaun.RevitTest test in a new project dotnet/SiteAdaptation.RevitTests. It opens a copy of a demo model and counts floors.

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

- Revit 2024 starts with the signed add-in and shows no Load Once prompt, seen on the screen by a person or in a screenshot taken by the run.
- Remove the certificate from Trusted Publishers, start again and see the prompt return, so the test proves the certificate is what stopped it. Put it back.
- The ricaun test passes and its floor count matches a second count taken another way, such as the add-in's own report or a schedule. Give both numbers.

# Revit check needed

All of the above. Write it to steps/revit-results/SETUP-sign.md.
