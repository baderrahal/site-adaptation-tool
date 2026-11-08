# build-3, FAST-13-build-3

F33, villa files in Revit 2024, item 1 of Bader's order of 8 Nov 06:45. Updated 8 Nov 2026. PR 283.

## What changed

- R810. dotnet/SiteAdaptation.Common/PlaceVillas/PrototypeCopies.cs, no Revit type. Find gives, per prototype file of the Prototypes folder, the copy of the same name in a folder beside it whose name holds the running Revit year, taken only when RevitFormat.Of reads that year. Most copies wins, by name on a tie.
- R811. VillaInputs.Copies and PrototypeFile in Common/PlaceVillas/IVillaDocument.cs. PlaceVillasRun.Decide and LinkUpgrade.NewFiles link the copy, else the old file as before. LinkUpgrade.WithCopies puts "links the Revit 2024 copies in <folder> for k of n prototype files (R811)" first in the box of step 7.
- R812. PaneViewModel.CopiesLook and CopiesNote, PaneText.PrototypeCopies, CodeNoCopy, YearNotRead. The line under the row Prototypes names the folder of copies and the year per code, else the R496 warning as before.
- docs/rules/29-fast13-r810-up.md, dotnet/SiteAdaptation.Tests/FAST13/PrototypeCopiesTests.cs, 3 lines in docs/design/14-ui-labels.md.

## What calls what

- PaneHost.Register sets PlaceVillasBody.Load through PlaceVillasBody.WithCopies, which calls PrototypeCopies.Find with the running Revit version. PlaceVillasRun reads VillaInputs.PrototypeFile, which calls PrototypeCopies.FileFor.
- PlaceVillasBody.Upgrades wraps the Preview with LinkUpgrade.Add, as in FAST-12, then LinkUpgrade.WithCopies.
- PaneHost.Register sets PaneViewModel.CopiesLook, a thread that calls PrototypeCopies.Find and posts back. RefreshFormat calls it after FormatLook and shows CopiesNote.

## Next

- Item 5 of revit-test on demo-S1: the row line, the box of step 7, step 7 time for 263 links against 28 minutes, the reopen with no upgrade box.
- steps/seam.md, 8 Nov 08:10: the test host reads the prototype loop from the plan's folder, and a model that already links the 2021 files gets new link types.
