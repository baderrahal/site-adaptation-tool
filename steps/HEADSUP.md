# Heads-up

## Live status

### Lane A

- Lane: A, round FIX-3NOV-A, branch fix/3nov-a. Time: 13:25. Blocked: none
- Done: R484 to R487, parity, gate, Revit on 84e1e69 to 4 Check. Records written
- Now: the PR, merge only if green by 14:00
- Next: a short Revit pass of the window fixes after the restart, not seen in Revit yet

### Lane B, C and D

- Lane B 20:55, 3 Nov. Undo proven in Revit (R499), steps/investigations/undo-3nov.md, lock free. Next: known issues, REPORT-3NOV-B, 6b. Ids R550 to R569, Q450 to Q469. Blocked: none
- Lane C, 3 Nov, branch feat/help-3nov-c: PR 160, 161, 162 merged, HELP PR going up, next RULES-3NOV-C. Ids R540 to R549, Q416 to Q419, Q440 to Q449. New callers: HelpBook by HelpFile, HelpFile by the pane and New project rows, HelpBox by OnStepHelp and OnRowHelp. A guide change needs python/build_help.py. I own the pane file tonight. Blocked: none
- Lane D PARAM-3NOV-D, 3 Nov 21:30, branch param/3nov-d: R506 to R508, Q411 closed. Gate green on a15d58c. Now: the past job half of parity, the PR, then the Revit check on a copy. Next: known issues, MASS-3NOV-D, TOPO-3NOV-D. Ids R530 to R539, R570 to R579, Q470 to Q479. No lock. Blocked: none
  New callers: SetupCheck.ConfigSheet by SetupCheck.Run and DiskProbe.ConfigSheet. IInputProbe.ConfigSheet, ReadSheet by PaneViewModel.RefreshSheet by RefreshOnce. ParameterSheet.Counts by Source and ReadSheet. ParameterSpec.LinksOnly by BoundOn, SetUpPlanner.Changes, RevitSiteDocument.BindParameters. BoundOn by SetUpPlanner.Changes, BindParameters. SetUpChanges.RemovalAlone, RunContext.KeepsLastRun by SetUpRun.Apply, ButtonFlow. SetUpTexts.TakesOff by SetUpPlanner, SetUpRun. NoRuleId by RefreshSheet.

Rewritten by each round, never appended, the other lanes' lines kept. Last: Lane D PARAM-3NOV-D, 3 Nov 2026.

## Repo map

- .claude and .github: agents, hooks, the AI MAX skill, the gate workflow. config and rules: project.json with the files beside it, shared_parameters.xlsx, the override list, the rules profile
- docs: rules (index.md finds a rule by id), design parts and boards, the mock, the repo map. dotnet/CadReader: the worker, reads CAD, PDF and xlsx, writes the Excel files and the scan
- dotnet/SiteAdaptation.AddIn: the Revit add-in, ribbon, pane, windows and the Revit side of each button. dotnet/SiteAdaptation.Common: the logic of the add-in and the worker, no Revit type
- dotnet/SiteAdaptation.Tests and UiShots: the gate tests, the offscreen renders of the windows. dotnet, others: the placer, the test host, the prototype reader, the Civil 3D probe and watchers
- python and tools: the readers and the plan build, the run scripts. steps: progress, log, plan, questions, latent findings, ui-shots, bader-fix-1nov.md, cleanup-1nov.md, the history of the rounds

## What changed on 1 and 2 Nov

- Lane C DEMO-FIX, PR 124 (R444): step rows of the pane run steps, a loader on every button, Confirm all, idle buttons hidden. AddIn/Loader, Pane, ReviewData
- Lane D FIX-1NOV-D, PR 126 (R460 to R465): config/shared_parameters.xlsx is the one parameter source of button 1. Common/SetUp/ParameterSheet.cs, ParameterBook.cs, SetUpPlanner.cs, RevitSiteDocument.cs
- Lane B FIX-1NOV-B, PR 127: masses as one linked file per prototype (R450 to R453), coordinates from CAD with three control points (R454 to R456). Common/PlaceVillas/MassFiles.cs, RevitMassFileMaker.cs, Common/SetUp/SitePosition.cs, CoordinateSystems.cs, ControlPoints.cs
- Lane A FIX-1NOV-A, PR 128 (R470, R471): shared ramp corners behind floors.rampJoint.join, false, and joint rows in 4 Check. No code builds a curb, curbs are read only (R355). Common/Plan/CheckRows.cs
- Lane D D2, PR 129 (R466): a test host job takes the design values from the repo config, the sheet takes group Text (Q336)
- Lane C FIX-1NOV-C (R445): Save setup writes the model at once in its own transaction, an undo or redo reloads the pane settings, Q280 closed. Review data column fix. Folder cleanup, steps/cleanup-1nov.md
- Lane A FIX-1NOV-A2, PR 130 (R472 to R474): button 2 takes the scan picks (Q292), Update from report finds the plan of 4 Check by its run log (Q278)
- Lane B Task 6, PR 131 (R457): the demo by hand, two passes, steps/demo-check.md, a neighbourhood kept when the list refills
- Lane C FIX-2NOV-C (R446): Review data Close and ask, no idle button, Run complete in the pane, the export refused in the input folder, Dry run off before 4 Check
- Lane D D3, PR 134 (R467, R468): Links mode proven by the test host on NBH 012, Q275 closed. Through the pane the Prototypes row names the link folder (part 11)
- Lane D FIX-2NOV-D (R490 to R493): designBasis.roadLevel.surfaces, ProjectSettings.RoadSurfaces, WorkerRequest.RoadSurfaces, the window Road surfaces. A new function and its caller: design_basis.surfaces_of, stop_share_of, not_taken_why, named_surfaces by basis_of, no_road_stop and road_gaps by plan_from_book.run_build, road_order by level_excel_check.expected. DesignBasis.SurfacesOf, StopShareOf by Of, TakePick by BuildSteps.Run, NoRoadStop, NoRoadLine by BuildPlan.NoRoadCheck by BuildPlan.Write, IsNoRoad by CheckRun. RoadSurfaceChoices.Named, IsRev6, OrderText, ForRequest, NotTakenWhy by ReadInputs.Make, CheckRun.Plan, ReadDataRun.Start, PaneRunState.InputsOf, ReadSettings.ReadRoads. ReadSettings.ReadRoads by WorkerReads.Surfaces, LevelWorkbook.Command. RoadSurfacesDialog.Show by NewProjectDialog.Show and PaneHost.Register, RoadSurfacesViewModel by the dialog, RoadPickState by both view models, PickSurfaces by NewProjectWindow and SitePanePage
- Lane D MASS-2NOV-D (R500 to R504): the mass base is a ring of the outer wall faces, corners by intersection. A new function and its caller: WallRingBuilder.Build and WallRing.Choose by MassFileSet.Make, DesignOptions.Shown by WallRingBuilder.Build and RevitBaseShapeReader.GroundFloors, WallProfiles.GroundStretches by WallRingBuilder.OnTheGround, RingEdges.Points by WallRing.Choose, RingEdges.CounterClockwise and Middle by RevitVillaDocument.Extrusion(edges), that by RevitMassFileMaker.Write, RevitBaseShapeReader.RingWalls by RevitMassFileMaker.ReadBase, ProfileLines and SideFace by RingWalls, MassFiles.EdgeJson by Sidecar, EdgeOf by Base, MassFileConfig.Limits by MassFileSet.Make and SourceKey. Removed: WallStrips, WallLoops, wallCutHeightM. FootprintCheck.Row takes the centre of the area. Mass files remade once, reader version 5

## New rules

- R444 to R446 (Lane C). R450 to R456 (Lane B). R460 to R466 (Lane D). R457 (Lane B). R470 to R474 (Lane A). R467, R468 (D3). R458, R459 (Lane B)

## Wiring

- Project mode, R484: ProjectModes.Apply called by BuildSteps, ReadSettings, ReadInputs, CheckRun, LevelWorkbook, ReportWorkbook, PaneRunState, RoadPickState. take_mode by plan_from_book, level_excel_check
- Shipped set, R486: ShippedSet.Take and Preview called by NewProjectViewModel, SetFor by NewProjectDialog, TakeNewProject by SetupSave. Install lands SiteAdaptationAddIn\ShippedSet. notRoad, R485: RoadSurfaceChoices.NotRoadWhy by DesignBasis, ReadInputs, ReadSettings
- Known warnings, R459: a step calls RunContext.AcceptKnownWarnings with its KnownWarningList (KnownWarningList.Parse takes its config block), WarningCollector deletes each accepted one at commit. FloorsRun.Apply is the first caller.
- Pane to a button: a step row or the box raises RunRequested, RunButtonHandler calls Buttons.Run, the path of the ribbon. Buttons.Run starts RunLoader
- Closing Review data: the X and Close, Window.Closing, ReviewDataSession.CanClose, ReviewCloseWindow.Ask. Run complete: PaneViewModel.RefreshBox, ReportFiles, OpenFolderRequested, PaneHost starts Explorer. Dry run: Buttons.Run, PaneRunState.BeforeRun, PaneViewModel.UntickDryRunFor. Export: ScanResultsSession.Export, InsideInputs, ScanGate.PickExportFor
- New project: NewProjectRequested raises NewProjectHandler, PaneHost.OpenNewProject opens NewProjectDialog.Show, then SetupWrite.Write, SettingsStore.Save and SettingsEntity in one transaction, then PaneViewModel.LoadSettings. PaneHost.OnChanged calls PaneSessions.Undone on an undo or redo
- Button 1: SetUpSiteBody calls SetUpRun, ReadingCad runs CoordinateRequest.Run with the worker command crs, ParameterSheet reads the sheet through ParameterBook inside Revit
- Button 7: PlaceVillasBody makes MassFileSet with RevitMassFileMaker, MassFiles keeps one mass file per prototype in the mass folder, WallRing.Choose takes the base. VillaLoad.Load gives the plan
- 4 Check: CheckRows gives the joint rows. Button 2: ReadDataBody.For, ScanStep.Wrap runs the scan, ScanGate.Ask opens ScanResultsDialog. 9 Report: ReportRun.CheckRun.Find reads run_4_<run id>.json
- Test host: ButtonsRunHost runs Buttons.Run and answers each Dialog seam with no click, WindowShots draws each window

## Open risks

- R445 has not run in Revit: the window from an external event, the commit and the undo. R446 neither. Parts 10 and 13 of steps/bader-fix-1nov.md check them
- C:\SA-demo\config lacks the keys of today, a config with no floors.rampJoint stops the build (part 2 of the sheet).
- Lane D: no villa type source reaches the strong share on ROSHN (Q254). Lane A: joints wait on Q340, Q341, R414 43 of 82, stays off
- The Parsons shutdown notice runs with higher rights, no session can press Postpone, only Bader. The ASK table of steps/cleanup-1nov.md waits for Bader

## Next

- Bader: the steps of steps/bader-fix-1nov.md, the 144 questions at its end, of them Q286 and Q293 after steps/ui-shots/drafts-for-bader.md. Then Part 2 after his check of Part 1
