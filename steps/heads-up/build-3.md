# build-3, FAST-13-build-3

F33, villa files in Revit 2024, item 1 of Bader's order of 8 Nov 06:45. Updated 8 Nov 2026. PR 283.

## What changed

- R810. dotnet/SiteAdaptation.Common/PlaceVillas/PrototypeCopies.cs, no Revit type. Find gives, per prototype file of the Prototypes folder, the copy of the same name in a folder beside it whose name holds the running Revit year, taken only when RevitFormat.Of reads that year. Most copies wins, by name on a tie.
- R811. VillaInputs.Copies and PrototypeFile in Common/PlaceVillas/IVillaDocument.cs. PlaceVillasRun.Decide and LinkUpgrade.NewFiles link the copy, else the old file as before. LinkUpgrade.WithCopies puts "links the Revit 2024 copies in <folder> for k of n prototype files (R811)" first in the box of step 7.
- R812. PaneViewModel.CopiesLook and CopiesNote, PaneText.PrototypeCopies, CodeNoCopy, YearNotRead. The line under the row Prototypes names the folder of copies and the year per code, else the R496 warning as before.
- docs/rules/29-fast13-r810-up.md, dotnet/SiteAdaptation.Tests/FAST13/PrototypeCopiesTests.cs, 3 lines in docs/design/14-ui-labels.md, the CHOSEN count of UiLabelTests.cs 526 to 529.

## What calls what

- PaneHost.Register sets PlaceVillasBody.Load through PlaceVillasBody.WithCopies, which calls PrototypeCopies.Find with the running Revit version. PlaceVillasRun reads VillaInputs.PrototypeFile, which calls PrototypeCopies.FileFor.
- PlaceVillasBody.Upgrades wraps the Preview with LinkUpgrade.Add, as in FAST-12, then LinkUpgrade.WithCopies.
- PaneHost.Register sets PaneViewModel.CopiesLook, a thread that calls PrototypeCopies.Find and posts back. RefreshFormat calls it after FormatLook and shows CopiesNote.

## Next

- Item 5 of revit-test on demo-S1: the row line, the box of step 7, step 7 time for 263 links against 28 minutes, the reopen with no upgrade box.
- steps/seam.md, 8 Nov 08:10: the test host reads the prototype loop from the plan's folder, and a model that already links the 2021 files gets new link types.

## Final statement

FAST-13 is built in one PR, R810 to R812, F33, status done. Step 7 now links the Revit 2024 copy of each villa file when a folder beside the Prototypes folder, whose name holds the year of the running Revit, holds a file of the same name that reads as 2024. A file with no copy is linked from the Prototypes folder as before and is named in the box of step 7 and in the row. The box of step 7 says first "links the Revit 2024 copies in Prototypes_2024 for 13 of 13 prototype files (R811)". The line under the row Prototypes names the folder of copies and the year per code, and with no copies it keeps the R496 warning. So the saved model links the 2024 files, and the reopen should show no upgrade box for them. Proof: on demo-S1, read in place, headers only, the built finder found Prototypes_2024 and 13 copies of 13 in 0.08 s. python/rvt_info.py, which shares no code with it, read 13 files of 2021 and 13 of 2024 with the same names. 7 tests in FAST13/PrototypeCopiesTests.cs. Putting the step 7 line back made the R811 test fail. Quick tests PASS, 154 pytest and 2774 net8.0. Acceptance 45 passed, 5 xpassed. Reviewer: PASS, no REAL fault, two latent points left as they are: the backup pattern is copied from PrototypeFormats.cs, and a prototype whose name ends in four digits is skipped as a backup, as in R496. Step 7 time: 28 minutes before, after UNKNOWN until revit-test runs item 5. Not seen in Revit, and Revit was not opened by this lane. Lead added docs/design/14-ui-labels.md (PR 282) and UiLabelTests.cs (PR 285) to the allowed list for the 3 new pane texts. Left in steps/seam.md: the test host still reads the 2021 files, and a model that already links the 2021 files gets new link types. Whether Revit takes a second link type of the same file name is UNKNOWN.
