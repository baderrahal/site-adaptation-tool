---
id: FAST-2-build-2
lane: build-2
stage: 2
status: open
allowed:
  - dotnet/SiteAdaptation.Common/AddinRun.cs
  - dotnet/SiteAdaptation.Common/AddinRunGate.cs
  - dotnet/SiteAdaptation.Common/BoundaryWalls/
  - dotnet/SiteAdaptation.Common/ButtonShots.cs
  - dotnet/SiteAdaptation.Common/Check/
  - dotnet/SiteAdaptation.Common/CheckTable/
  - dotnet/SiteAdaptation.Common/CountText.cs
  - dotnet/SiteAdaptation.Common/DesignValues.cs
  - dotnet/SiteAdaptation.Common/ErrorLines.cs
  - dotnet/SiteAdaptation.Common/FileIdentity.cs
  - dotnet/SiteAdaptation.Common/FloorMath.cs
  - dotnet/SiteAdaptation.Common/FloorPlacer.cs
  - dotnet/SiteAdaptation.Common/FloorTarget.cs
  - dotnet/SiteAdaptation.Common/Floors/
  - dotnet/SiteAdaptation.Common/Flow/
  - dotnet/SiteAdaptation.Common/FreezeWindows.cs
  - dotnet/SiteAdaptation.Common/Help/
  - dotnet/SiteAdaptation.Common/HostLoops.cs
  - dotnet/SiteAdaptation.Common/InputGuard.cs
  - dotnet/SiteAdaptation.Common/JsonOut.cs
  - dotnet/SiteAdaptation.Common/KeyParameters.cs
  - dotnet/SiteAdaptation.Common/LinkFields.cs
  - dotnet/SiteAdaptation.Common/LinkMath.cs
  - dotnet/SiteAdaptation.Common/ManualRules/
  - dotnet/SiteAdaptation.Common/ModelReader.cs
  - dotnet/SiteAdaptation.Common/ModelSource.cs
  - dotnet/SiteAdaptation.Common/OutlineLoops.cs
  - dotnet/SiteAdaptation.Common/ParameterBag.cs
  - dotnet/SiteAdaptation.Common/ParameterFormat.cs
  - dotnet/SiteAdaptation.Common/PlaceVillas/
  - dotnet/SiteAdaptation.Common/PlacementRotation.cs
  - dotnet/SiteAdaptation.Common/Placer.cs
  - dotnet/SiteAdaptation.Common/PlacerReport.cs
  - dotnet/SiteAdaptation.Common/PlacerTarget.cs
  - dotnet/SiteAdaptation.Common/Plan/
  - dotnet/SiteAdaptation.Common/PlanFile.cs
  - dotnet/SiteAdaptation.Common/PlotParts.cs
  - dotnet/SiteAdaptation.Common/ProjectRules/
  - dotnet/SiteAdaptation.Common/PrototypeFormats.cs
  - dotnet/SiteAdaptation.Common/PrototypeModels.cs
  - dotnet/SiteAdaptation.Common/Read/
  - dotnet/SiteAdaptation.Common/ReadData/
  - dotnet/SiteAdaptation.Common/ReaderOptions.cs
  - dotnet/SiteAdaptation.Common/Report/
  - dotnet/SiteAdaptation.Common/Review/
  - dotnet/SiteAdaptation.Common/RevitFormat.cs
  - dotnet/SiteAdaptation.Common/RevitListener.cs
  - dotnet/SiteAdaptation.Common/Rounding.cs
  - dotnet/SiteAdaptation.Common/SavedPath.cs
  - dotnet/SiteAdaptation.Common/Scan/
  - dotnet/SiteAdaptation.Common/Scope/
  - dotnet/SiteAdaptation.Common/SecurityDialogs.cs
  - dotnet/SiteAdaptation.Common/SetUp/
  - dotnet/SiteAdaptation.Common/Settings/
  - dotnet/SiteAdaptation.Common/SharedCoordinates.cs
  - dotnet/SiteAdaptation.Common/SharedParameterFile.cs
  - dotnet/SiteAdaptation.Common/SiteAdaptation.Common.csproj
  - dotnet/SiteAdaptation.Common/SiteBuild/
  - dotnet/SiteAdaptation.Common/SiteFacts.cs
  - dotnet/SiteAdaptation.Common/SiteSpec.cs
  - dotnet/SiteAdaptation.Common/SpotValues.cs
  - dotnet/SiteAdaptation.Common/Steps/
  - dotnet/SiteAdaptation.Common/Tags/
  - dotnet/SiteAdaptation.Common/TestHostFiles.cs
  - dotnet/SiteAdaptation.Common/TestHostJob.cs
  - dotnet/SiteAdaptation.Common/TestHostRun.cs
  - dotnet/SiteAdaptation.Common/UnitMath.cs
  - dotnet/SiteAdaptation.Common/Update/
  - dotnet/SiteAdaptation.Common/UtmMath.cs
  - dotnet/SiteAdaptation.Common/WatcherStart.cs
  - dotnet/SiteAdaptation.Common/WorkRoot.cs
  - dotnet/SiteAdaptation.Common/Worker/
  - dotnet/SiteAdaptation.Common/Fast2/
  - dotnet/CadReader/CadReader.csproj
  - dotnet/CadReader/CoordinateSystemRead.cs
  - dotnet/CadReader/LayerConfig.cs
  - dotnet/CadReader/LevelWorkbook.cs
  - dotnet/CadReader/MTextCleaner.cs
  - dotnet/CadReader/PdfPage.cs
  - dotnet/CadReader/PlotLabels.cs
  - dotnet/CadReader/PlotShapes.cs
  - dotnet/CadReader/Program.cs
  - dotnet/CadReader/ReportWorkbook.cs
  - dotnet/CadReader/ScanSources.cs
  - dotnet/CadReader/UpdateFromReportWorker.cs
  - dotnet/CadReader/UpdateWorkbook.cs
  - dotnet/CadReader/WorkerReads.cs
  - dotnet/CadReader/XlsxCells.cs
  - dotnet/CadReader/XlsxSheet.cs
  - dotnet/CadReader/Fast2/
  - dotnet/SiteAdaptation.Tests/FAST2/
  - dotnet/SiteAdaptation.Tests/ParsonsModelTests.cs
  - dotnet/SiteAdaptation.Tests/WorkerRunTests.cs
  - dotnet/SiteAdaptation.Tests/R448_RevitListenerTests.cs
  - dotnet/SiteAdaptation.Tests/TestHostTests.cs
  - python/
  - config/
  - rules/
  - docs/rules/
  - steps/heads-up/build-2.md
  - steps/seam.md
  - steps/log.md
---

# Scope

The logic behind the face, for any project. Bader's order of 6 Nov, fast build, 6 to 8 Nov. In this order:

1. Shared parameters for any project. config/shared_parameters.xlsx is the one source, a new project edits only that Excel. Parameters go on the villa links only, never on floors, apart from Plot, which stays on floors as the tool's own tag by Q560, item 11. Never write RRE_ZZ_SP_Prototype-FFL. The data check marks sharedParameters strong when the Excel is found and says in plain words what is missing when it is not.
2. The source mark per value: computed, rule, person entered, missing, carried through the plan, the check table and the report. The report on a new project prints no past shares, each sheet says the source of its values.
3. Update from report on a new project, Q394.
4. Moved to FAST-4-build-3 on 6 Nov, Bader's order.
5. Moved to FAST-4-build-3 on 6 Nov, Bader's order.
6. No RAM floor: remove siteBuild.minFreeGB from config/project.json and every reader, python/window_lock.py, python/c3d_window.py and the worker checks. Bader's order of 6 Nov: WorkerRunTests.cs, R448_RevitListenerTests.cs and TestHostTests.cs are on this list by exact path, AddinConfig.cs, WorkerMemory.cs and RevitListener.cs of Common are in its Common entries. The floor lines of Run-TestHost.ps1, Start-RevitListener.ps1 and Run-Parallel.ps1 in dotnet/SiteAdaptation.TestHost/ are FAST-1's item 8. The order, so the gate stays green: first FAST-2 makes ParsonsModelTests.cs:360 and TestHostTests.cs:392 and :397 accept the script lines with or without the floor, then FAST-1 removes the floor lines from the scripts, then FAST-2 asserts the lines without the floor.
7. Boundary wall thickness from the rules file, Q149. The link parameter group, Q336.
8. Q504, Bader's answer 5A of 6 Nov: a plot whose ffl is missing is marked, steps 6 to 8 skip it, and the plan lists every such plot in the issues list with the words no road level, so the card shows the count.
9. Bader's answer 4A of 6 Nov: the R310 tests of ParsonsModelTests.cs fail at random on this PC. Measure the time of a CIM call on this PC and take the limit of the tests from it. dotnet/SiteAdaptation.TestHost/WindowSamples.ps1 is FAST-1's, a change there is a line in steps/seam.md.
10. The seam of build-1, 6 Nov: AddinRunNames.Page in dotnet/SiteAdaptation.Common still names SitePanePage, the page the Revit test host looks for. The docked page is now StagesPage of FAST-1, with SitePanePage folded under Advanced, so name StagesPage, or both, and keep the tests of the host green.
11. Q560, Bader's answer A of 6 Nov: Plot stays on floors as the tool's own tag, every other shared parameter on villa links only.
12. Bader's order of 6 Nov: the Plot row of config/shared_parameters.xlsx gets the note tool tag, floors, so a new project understands it. Item 1 is complete on Plot: it stays on floors as the tool's own tag, Q411 and Q560, the RRE_ZZ_SP_ set and CASE are links only.

Tests in dotnet/SiteAdaptation.Tests/FAST2/ and python/tests/. Common/DataCheck/, CadExtractor.cs, CadModels.cs, Exploder.cs, DataCheckCommand.cs, CadScan.cs, scripts/scenarios/ and steps/scenarios.md are FAST-4-build-3's since 6 Nov, a new file of FAST-2 goes in Common/Fast2/ or CadReader/Fast2/ or a folder of its list. tests/acceptance/ stays Lead's, the required check fails a lane that changes it, and scripts/ is held to scripts/scenarios/, as scripts/checks/ is the gate and scripts/lanes/ the lane starter. CHOSEN by Lead.

Rule ids: R700 to R719. Question ids: Q540 to Q549. Take no other id.

# Done when

The S1 folder scans with sharedParameters strong, the plan carries a source on every value, the report says it. The villa plot count is FAST-4-build-3's since 6 Nov.

# Build rules

- No vibe code. Add only what the task lists. A change not in the task goes in steps/seam.md as a line for Lead, not in the code.
- Wiring. Every new function has a caller named in the PR. A part nothing calls is a failure.
- Logic. Every rule has its id, input, output and test. Where the task is silent, take the smallest version, mark it CHOSEN in the PR, go on. Do not stop.
- Folder structure. New files go only in the folders of the allowed list. No new folder without a line in the PR saying why.
- Any project. No ROSHN value in code. Every project value comes from config, rules or the Excel. No hand editing of JSON by a user.
- Each PR body: plan items done, skipped and extra with the reason, callers, what was opened in Revit, the log entry.
- Push at least every 2 hours. Open the PR when one card or one function works, not at the end. Merge main before every push.
- Keep steps/heads-up/<lane>.md current: what changed, where, what calls what, what is next. One page.

At the end, put your final statement in the PR body and in steps/heads-up/<lane>.md, then push.

# Rules for Lead

The rules of this task are in docs/rules/19-fast2-r700-up.md, written by build-2 at Lead's answer of 6 Nov.
