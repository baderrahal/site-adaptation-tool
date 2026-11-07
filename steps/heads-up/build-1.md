# build-1 heads-up, FAST-8-build-1

7 Nov 2026. F26 and F27 of the short check of 01A. Task branch task/FAST-8-build-1.

## Final statement

F26 is fixed and F27 is fixed in code, but the FAST-8 done when cannot pass on 01A of S1.
- F26: Check no longer takes plots.csv or labels.csv from the data book of the work root. The plot table of the read stands in for them, and a read with no plot table stops as missing (R780).
- F27: Floors and ramps takes, of the road surfaces the build read, the one with the most ramp road edge points, and says which (R781). Highlighted floors overlap of two plots no longer stops on a Revit box, it is listed with its floors in the issues (R782).
- Measured with a script that shares no code with the C#: no road surface of S1 covers 01A. The 6 surfaces that cover its 807 points are ground or grading, and config excludes them. So no road toposolid of 01A can be made, Q600 in steps/seam.md for Bader. The recommended answer is to run the demo on 012.
- Yard floors 0 is R410, not a fault.
- Not seen in Revit, label revit-later.
- The task stays open, its done when waits for Q600 and the Revit check.
- Review 1 of PR 269 found 4 REAL faults, all fixed.

## What changed, where

- dotnet/SiteAdaptation.Common/CheckTable/ProjectPlots.cs, new: Write, Rows and InDataBook, the plot table as plots.csv and labels.csv in plots_<read folder> of the work folder.
- dotnet/SiteAdaptation.Common/CheckTable/CheckRun.cs: DataBookRoles. Plan skips a data book file of the read under rev6 and adds the files of ProjectPlots.Write.
- dotnet/SiteAdaptation.Common/Floors/RoadTopography.cs: RoadTopographySettings.For, RoadSurfaceRead.Others and Why, RoadTopography.Covering.
- dotnet/SiteAdaptation.Common/Floors/FloorLoad.cs: RoadSurface gives the named surface, else the first read, with every other road surface of the build as a lazy read, Surface reads one.
- dotnet/SiteAdaptation.Common/Floors/FloorsRun.cs: PlanToposolid calls Covering. The preview and the after commit lines add Why.
- dotnet/SiteAdaptation.Common/Floors/KnownWordsWarning.cs, new: TakeAndList and Line.
- dotnet/SiteAdaptation.AddIn/Revit/WarningCollector.cs: a warning of known words that Decide refuses is deleted and its line goes to Warnings, so to the issues by ButtonFlow.
- docs/rules/24-fast8-r780-up.md R780 to R782. Tests dotnet/SiteAdaptation.Tests/FAST8/, 16.

## What calls what

4 Check: CheckRun.Plan calls ProjectPlots.InDataBook per read input, then ProjectPlots.Write, which calls Rows. 5 Floors and ramps: FloorLoad.Floors gives RoadTopography, which calls FloorLoad.RoadSurface, which calls Surface. FloorsRun.PlanToposolid calls RoadTopography.Covering, which reads Others only when the named surface misses points. Revit commit: WarningCollector.Collect calls KnownWordsWarning.TakeAndList and Line.

## Next

- Bader answers Q600, then the second short check by revit-test.
- F28 line on card 2, after that check.
