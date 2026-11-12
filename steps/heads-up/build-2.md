# build-2, OLD-1d1-build-2

Round 1 step d part 1, the data book reads. Updated 12 Nov 2026. Earlier notes are in the history of this file.

## This PR

- R840. dotnet/SiteAdaptation.Common/ReadData/ReadInputs.cs, Plots: the read for the build takes plotsCsv and labels from the plot table of the latest read of card 1 (ProjectPlots.Write), never from a data book.
- R843. dotnet/SiteAdaptation.Common/ReadData/FolderRoles.cs, Causes: a Build stop names why the folder gave no file only for a row still empty.
- Tests in dotnet/SiteAdaptation.Tests/OLD1/R840_BuildWithNoDataBookTests.cs, R740_BuildReadTests turned to feed a made up read of card 1.
- Not seen in Revit.

## What remains, for Lead

- The old read of step 2 (ReadInputs.Make, past job mode) still names plotsCsv, labels and border of the data book, and the fit neighbourhood. Taking them out failed 38 tests on net8.0 and leaves a read with no plot table: it goes whole, with past job mode.
- The config keys addin.readData.dataBookFolder and dataBookFiles, the data book lines of Run-TestHost.ps1, help.json and NewProjectCheck.cs stay until then.
- UNKNOWN until step e: whether the road levels find a face for each plot of the plot table.
- The task file stays open.
