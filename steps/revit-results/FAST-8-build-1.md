# FAST-8-build-1, Revit check

7 Nov 2026, build-1.

## What was opened

Nothing. Revit was not opened by this lane for FAST-8. The PR carries the label revit-later, the check is owed by the second short check of revit-test on 01A.

Why not tonight:
- The toposolid of 01A cannot be made from a road surface of S1, by measurement (below), so a test host run of step 5 on a copy of the 01A model would end with no toposolid whatever the code does. That is a question for Bader, not a fault a Revit run finds.
- The delete of a warning by WarningCollector (R782) is the one part only Revit shows. It is a small change of the add-in, the same DeleteWarning call R459 makes for an accepted pair.

## What was measured instead, off Revit, read only

A Python script of the scratch folder that shares no code with the C# read the short check outputs of the work folder of FAST3-short-01A and the 18 LandXML files of the road folder of S1. Each real file was marked by name, size, write time and SHA-256 before and after, 18 of 18 unchanged.

- F26: the request of the last Check named plotsCsv and labels inside data_book of the work root, true for both. The plot table of the read holds 2,615 plots, 278 of them in 01A, the count Review data showed. Check showed 297.
- F27: of the 807 ramp road edge points of plan_01A.json, the 6 road surfaces the build read hold 0, 0, 12, 4, 0 and 0. Of the other 12 files of the folder, 6 hold all 807: Grading - North, Grading - North (1), New Existing, New Existing (1), EXISTING_11.5 Sqkm and Phase - 01 Grading. config names each of them ground or grading, designBasis.roadLevel.notRoad (R485) and siteBuild.roadFolder.notTaken. The rest hold at most 29.

Not seen in Revit: R780, R781, R782.
