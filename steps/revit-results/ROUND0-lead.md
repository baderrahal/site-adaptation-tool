# Round 0, Lead, 12 Nov 2026

Not seen in Revit. No Revit was opened for this PR.

What changed in Revit add-in files:

- dotnet/SiteAdaptation.TestHost/Run-TestHost.ps1: with -Upgrade the file name of a code comes from the prototype folder, by Get-SetPrototype with the discipline of prototypeNames, and no longer from plan_imc_past.json. Proven by PrototypeSetTests, the script text and a folder read of made up file names. The check in Revit is owed to the next upgrade run, none is planned: the build links the 13 Revit 2024 villa files of the input folder.
- dotnet/SiteAdaptation.AddIn/Help/help.json: 3 lines of text name the Prototypes_2024 folder of the inputs. Text only.

Counts: build Release 0 errors, quick tests pytest and dotnet passed, 2,774 dotnet tests on net8.0.
