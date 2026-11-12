# revit-test heads-up, OLD-1b-revit-test

12 Nov 2026, 12:45. The PR is open and not merged, Lead merges. Detail and proof in steps/revit-results/OLD-1-revit-test.md.

## Final statement

- Built: 0 of 9 neighbourhoods. Stopped: 9 of 9, 01A (278 plots read), 010 (16), 012 (342), 013 (416), 01B (2), 02 (325), 021 (435), 022 (305), 03 (493), each at the read for the build, before 4 Check.
- The exact words of card 2, the same on all 9: "Build stopped at Read data: labels of the data book is not on disk: C:\SA-work\SiteAdaptation\data_book\location_plan\labels.csv. plotsCsv of the data book is not on disk: C:\SA-work\SiteAdaptation\data_book\plots.csv. roadSurface: no folder holds a road surface named in config or picked. prototypes: two folders hold 13 each, C:\02-Site Adaptation\00_Revit Models\Zone 02\ARC\Prototypes and C:\02-Site Adaptation\00_Revit Models\Zone 02\ARC\Prototypes_2024"
- The cause is one, not of a neighbourhood: the read for the build needs labels.csv and plots.csv of data_book of the past job (ReadInputs.cs lines 201 and 365 to 386). This PC holds no data_book and no window sets the two files. The road surface and prototype sentences are notes, both rows were set in a window.
- Card 1 ran once: Set up site 5 of 5 check points, 2 reads of 61 and about 90 s, 2,615 villa plots of 2,964, 9 neighbourhoods in the Scope list, the skip settings of PR 299 seen working (3 folders and 8 files left out). The road surfaces: the tool does not find landxml of the work root by itself, the row Road levels of Settings takes it by Browse and offers 6 of the 7 surfaces.
- The read is never whole on this PC: 9 drawings need 2.73 to 53.15 GiB of free memory.
- Card 3 Report: not run, the button was off, "Waits for stage 2". So the Done when line "the Report ran on the last model" is not met.
- Not as the task says: all 9 neighbourhoods ran in one Revit window with one save at its end, not one per part, CHOSEN by this lane since each Build stopped in under 4 minutes and wrote nothing to the model.
- Total time: one Revit window, 11:46:46 to 12:31:40, 44.9 minutes. The whole task 11:40 to 12:45.
- Input folder: 837 files, gone 0, added 0, changed 0 by SHA-256, before and after.
- Opened in Revit: Revit 2024.3 once, a new project from DefaultMetric.rte saved as old-1b\OLD1-site.rvt under the work root. No file of the input folder was opened in Revit.
- The driver: UI Automation patterns and window messages to the Revit it started, no click, no key, no shot. Always Load pressed once at 11:48:18 on the box of Site Adaptation.
- No code changed, no JSON edited by hand. status of the task file is still open, the file is not in the allowed list.
