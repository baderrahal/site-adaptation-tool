# SETUP-sign, Revit check

Date 2026-11-04 and 05. Lane revit-test, Revit 2024.3 (build 20250918_1515), all runs under python/window_lock.py, a copy of a demo model, Revit closed after each run.
Screenshots and result files are under the work root, folder `setup-sign`, never in git: the dialogs show the Windows user name in the add-in path.

## Not proven, wait on the trust decision

- ZERO PROMPTS IS NOT PROVEN. The tool still shows 1 prompt at a cold start. No signing is part of the install.
- A signed add-in with no prompt, and the test that removes the certificate and sees the prompt return, were NOT done. They need the certificate in Trusted Root, a step the sandbox refused ("[Security Weaken]") and that Bader decides.
- The ricaun.RevitTest test does NOT run. The project builds, the test fails before it reaches Revit. See the last section.
- The add-in tab of the ribbon was found in the ribbon by UIA but is hidden under the overflow arrow, so no screenshot shows its buttons. A person must look at it once.

## 1. Before the change, measured

`.addin` files in `%APPDATA%\Autodesk\Revit\Addins\2024`, 4 of this tool, each with one entry DLL, the same as Lead read:

| File | Entry DLL | Type |
|---|---|---|
| SiteAdaptation_Revit2024.addin | RevitPrototypeReader.dll | Command |
| SiteAdaptationAddIn_Revit2024.addin | SiteAdaptation.AddIn.dll | Application, the tool |
| SiteAdaptationPlacer_Revit2024.addin | SiteAdaptation.Placer.dll | Command |
| SiteAdaptationTestHost_Revit2024.addin | SiteAdaptation.TestHost.dll | Application |

Other makers, never touched: `%APPDATA%` holds one other add-in, disabled. `C:\ProgramData\Autodesk\Revit\Addins\2024` holds 33 `.addin` files of Autodesk, DiRoots and other makers, none of this tool.

Cold start with no document and one answer per box (before5\result.json, scripts/sign/Measure-ColdStart.ps1): 4 prompts "Security - Unsigned Add-In", one per entry DLL above, at 53.4, 55.3, 56.3 and 57.3 s, all 4 of this tool, none of another maker. Screenshots before5\prompt-01.png to prompt-04.png. Each was answered Load Once, never Always Load.

All 34 DLL and EXE files under the 4 tool folders were unsigned by the tool before the change, 11 of them (the worker's libraries) already carry a valid signature of other makers.

## 2. After the install script of this change, measured

Install-SiteAdaptation.ps1 -NoBuild and Install-TestHost.ps1 -NoBuild ran, output:
`Manifests of developer add-ins taken away: SiteAdaptation_Revit2024.addin, SiteAdaptationPlacer_Revit2024.addin, SiteAdaptationTestHost_Revit2024.addin`
`Manifests of this tool in ...: SiteAdaptationAddIn_Revit2024.addin`
`one SiteAdaptation.Common.dll in 5 folders of the tool`

- `.addin` files of this tool: 1 (SiteAdaptationAddIn_Revit2024.addin), entry DLL 1 (SiteAdaptation.AddIn.dll).
- Cold start, no document (after\result.json): 1 prompt, "Security - Unsigned Add-In", Name Site Adaptation, at 49.6 s, answered Load Once. Screenshot after\prompt-01.png. No prompt of another maker.
- Cold start with a copy of a demo model (after5\result.json): again 1 prompt, answered Load Once, after5\prompt-01.png. Model window up at 125 s, Floor Plan: Level 1. after5\ribbon.png and after5\main.png show the ribbon with the tabs of the other makers. The add-in tab "Site Adaptation" was found by UIA under the name (toolTabFound true, toolTabOffscreen true), it hides under the overflow arrow. 3 attempts to select it by the overflow arrow failed, so the buttons are not on a screenshot.
- Signatures: 34 DLL and EXE files, 0 signed by our certificate, 23 unsigned, 11 valid of other makers (Get-AuthenticodeSignature on each).

4 prompts became 1. The one left is the tool itself.

## 3. Signing experiment, measured and undone

A certificate `CN=Site Adaptation Tool self-signed code signing` was made in the personal store of the current user (key not exportable, no .pfx) and put in Trusted Publishers of the current user, which the PC allowed. All 23 unsigned installed files were signed with it. Get-AuthenticodeSignature then gave UnknownError on each (chain ends in a root that is not trusted). A cold start then showed no Load Once box and instead "Security - Invalid Signature" (exp-tp-only2\result.json, windowsSeen at 52.2 s). So Trusted Publishers alone is not enough and a signed file shows a worse box than the plain prompt.

The step to Trusted Root of the current user was refused: "Permission for this action was denied by the Claude Code auto mode classifier. Reason: [Security Weaken]." Not tried another way. The signed files were replaced by a reinstall, a check after it found 0 files signed by the certificate. The certificate stays in the personal store and in Trusted Publishers, it does nothing to an unsigned file.

UNKNOWN: whether Revit accepts the certificate once it is in Trusted Root, whether company policy lets that step through, and whether the Invalid Signature box has a button that still loads the add-in.

## 4. Developer add-ins still run

`Run-TestHost.ps1 -DryStart` under the lock, with only the tool manifest installed, run twice: before the review fixes (3.6 minutes) and again with the stamp code of the fixes (3.2 minutes, exit code 0). Each time the run wrote the three developer manifests, started Revit, the watcher answered 4 boxes Load Once (reader, tool, placer, test host), and afterwards `Developer add-in manifests after the run: removed SiteAdaptation_Revit2024.addin, SiteAdaptationPlacer_Revit2024.addin, SiteAdaptationTestHost_Revit2024.addin`. After the second run only SiteAdaptationAddIn_Revit2024.addin was left, the stamp folder was empty and no Revit process was left. Log: testhost_dry_start_log.json under the work root. A shutdown error of another maker's add-in printed at the end, it does not come from this tool.

How the manifests are held (review of PR 195): a run or the listener adds a stamp file with its process id and start time under the work root, and removes it when it ends or on any error after the write. The manifests go when no stamp of a live process is left. A Revit opened by hand holds nothing. Tests in dotnet/SiteAdaptation.Tests/InstallScriptTests.cs hold the stamp logic (two runs, a dead stamp, a taken over id), the throw after the write, and the order of the write, the trap and the finally in the run script and the listener.

Not run in Revit: Start-RevitListener.ps1 and a real prototype job of Run-TestHost.ps1. The listener uses the same functions as the run script, held by the tests above.

## 5. The ricaun.RevitTest test, not working

Licence: ricaun.RevitTest.TestAdapter 1.11.1 is MIT, read from the LICENSE file in the package, recorded in docs/licences.md with NUnit 3.14.0 and NUnit3TestAdapter 4.6.0 (python/tests/test_licences.py passes, 8 tests).

Project dotnet/SiteAdaptation.RevitTests, one test FloorCountTests, copies a model under the work root, opens the copy, counts floors by a collector and by the rows of a floor schedule, closes without saving. It is kept out of the plain test by not being in dotnet/SiteAdaptation.sln, and so out of `dotnet build` and `dotnet test` of the sln and of .github/workflows/tests.yml. It builds with 0 errors.

Run with scripts/sign/Run-RevitTests.ps1 under the lock, 3 attempts, all failed before Revit started:
1. Without the Revit DLLs next to the test: "Could not load file or assembly 'RevitAPIUI, Version=24.3.60.0'".
2. With RevitAPI.dll and RevitAPIUI.dll of the installed Revit copied next to the test: "Failure building Test ... Could not load file or assembly 'RevitAPIUI.dll' or one of its dependencies. The specified module could not be found." and then "Installed Revit with version 2024 or greater not found."
3. Stopped. The third attempt on this is where it goes back to measuring. No Revit started, no prompt of ricaun was seen, no floor count was taken.

So the two floor counts the task asks for are UNKNOWN. UNKNOWN too: why the adapter's console does not find the installed Revit 2024, and whether it would ask about its own add-in at the start.
