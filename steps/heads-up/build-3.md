# build-3, FAST-12-build-3

F31, step 7 links prototypes of an older Revit. Updated 8 Nov 2026 02:10.

## What changed

- R805. dotnet/SiteAdaptation.AddIn/PlaceVillas/UpgradingVillaDocument.cs, an IVillaDocument around RevitVillaDocument. LoadType, ReloadType and CreateType turn on a DialogBoxShowing handler for the load alone. It closes the box TaskDialog_Views_Related_To_Analytical_Changed, "Structural Analytical Model Upgrade", with Close (8) and counts it. Any other box stays for the person.
- R806. Before each load it reads the Revit version of the file with BasicFileInfo. TakeIssues gives a line per file of an older Revit and one for the boxes closed.
- R807. dotnet/SiteAdaptation.AddIn/PlaceVillas/LinkUpgrade.cs, no Revit type. NewFiles gives the prototype files of the plan in filter and scope with no usable loaded type, Links mode only. Add puts "link types new to the model N, of an older Revit M" on the counts of the box and a line per older file first.
- docs/rules/28-fast12-r805-up.md, dotnet/SiteAdaptation.Tests/FAST12/LinkUpgradeTests.cs.

## What calls what

- Buttons.cs calls PlaceVillasBody.For, which builds UpgradingVillaDocument and hands it to PlaceVillasRun, then calls Upgrades on the body.
- Upgrades wraps Preview with LinkUpgrade.NewFiles and LinkUpgrade.Add, and Apply with UpgradingVillaDocument.TakeIssues, which calls LinkUpgrade.IssueLines.
- PlaceVillasRun calls LoadType and ReloadType in Outside, CreateType in Apply through TypeKey. Each calls Watch, which calls VersionOf, LinkUpgrade.Older and, in OnDialog, LinkUpgrade.Knows.

## Next

- The Revit check is owed by run 2 of revit-test: step 7 on 2021 prototypes, no stop on the upgrade box, the issue lines and the box count (steps/revit-results/FAST-12-build-3.md).

## Final statement

FAST-12 is built in one PR, R805 to R807, F31. While step 7 loads, reloads or makes a link type, the tool closes Revit's box "Structural Analytical Model Upgrade" by its id, TaskDialog_Views_Related_To_Analytical_Changed, with Close, so an unwatched Build no longer waits on it. Any other box still stops as before. The issues of the step name each file of an older Revit that was upgraded and how many boxes were closed. The box of step 7 now says "link types new to the model N, of an older Revit M" and names each older file, "Revit upgrades it to 2024 as it loads, which takes minutes". The id is measured, not guessed: the buttons run LB6E logged it in step 7 with Close taken, and the driver of short check 2 pressed Close on that title 4 times. Proof: 8 tests in FAST12/LinkUpgradeTests.cs, a mutation of Close and the year compare failed 3 of them, quick tests PASS, net48 3307 passed, acceptance 45 passed 5 xpassed, and a Python check over the run logs and a Revit file header that shares no code with the C#. Not seen in Revit: Revit was not opened by this lane, since step 7 needs the plan of a real 4 Check on prototype copies, about 1 hour 20 of driving, and a new install would change the add-in build-1 and the 06:00 run use. Label revit-later, owed by run 2 of revit-test. Left in steps/seam.md: "link types to load" of PlaceVillasRun still counts only unloaded types, Common is outside this task.
