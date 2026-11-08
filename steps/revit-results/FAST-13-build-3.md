# FAST-13-build-3, Revit check

8 Nov 2026. Not run in Revit by build-3.

- Revit was not opened by this lane. Step 7 needs the plan of a real 4 Check on a model copy, about an hour of driving before step 7 starts, and revit-test needs Revit for item 5 of Bader's order of 06:45 once FAST-13 to FAST-15 merge. No revit-wanted-build-3.txt was written, since build-3 wants no Revit time.
- Owed by item 5 of revit-test on demo-S1: the line under the row Prototypes names Prototypes_2024 and 2024 for each of the 13 codes, the box of step 7 says "links the Revit 2024 copies in Prototypes_2024 for 13 of 13 prototype files (R811)" and "of an older Revit 0", step 7 time for 263 links against 28 minutes in run 2, and the reopen of the saved model with no upgrade box.
- Measured off Revit on demo-S1, read in place, headers only: PrototypeCopies.Find of the built SiteAdaptation.Common.dll found Prototypes_2024 and 13 copies of 13 in 0.08 s. python/rvt_info.py, which shares no code with it, read 13 files of format 2021 in Prototypes and 13 of format 2024 in Prototypes_2024, same names.
