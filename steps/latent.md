# Latent findings

A finding no real input reaches, kept as a known limit with no fix (Q095, Bader, 2026-10-27). Parity and review run on real inputs only. The reviewer and the breaker mark each finding REAL or LATENT, and a LATENT one is added here with its PR, file and line. A real input that reaches one later makes it a bug again, fixed with a test.

Older parts are in steps/history/latent-older-2026-11-05.md.

## FIX-1NOV-A, review 1 and breaker pass 1

- Review 1, check_rows.ramp_joints and CheckRows.RampJoints: R471 groups the plan corners at 3 decimals, R470 the raw corners, so a pair 8.6 to 10 mm apart could join in one and not the other, a missing row. 0 of 4,113 joints over 1 mm after R470.
- Review 1, test gap: no test puts two corners exactly floors.rampJoint.nearM apart, both languages use the same float test.
- Review 1, JointLimits and joint_limits take an inf or NaN nearM, joint_near refuses it and JointNear does not test finite. config holds 0.01.
- Breaker 1, grouping: R470 groups raw corners, R471 plan corners at 3 decimals, a pair at 0.0099 m raw could flip. 0 real pairs between 0.0015 and 0.0197 m.
- Breaker 1, joint_levels and JointLevels: no limit on the length of a chain, and two corners of one plot can meet through a third. Every real joint holds 2 corners of 2 plots, the widest 1.4 mm.
- Breaker 1, NoSiteLevel: C# joins and CheckRows.RampCorners lists ramps BuildPlan does not write, Python writes none. Every real site holds the ramp level.
- Breaker 1, grid cell index at exactly nearM in all four joint finders. No real pair near 0.01 m.
- Breaker 1, S5: 169 back pairs of a shared side sit 19.7 to 20 mm apart, outside nearM, with steps of up to 229 mm that were in the base too. R470 does not join back corners, R471 lists none of them.- Review 2, check_rows.ramp_joints and CheckRows.RampJoints group all four corners and R470 joins only the front pair, so a back corner within nearM of another ramp's front corner would list as a joint row. No real pair seen.

## Lane B FIX-1NOV-B, review and breaker pass of Task 2

No real input reaches these on the ROSHN config, whose linkFolder is masses, massFolder {workRoot}\masses and drawings in metres.

- PlaceVillasRun.cs:30, the constructor of 5 arguments is used by the tests alone, the add-in passes the mass files. A caller of it with linkFolder masses stops with no mass files wired (R450).
- MassFiles.cs:56 and Make (:205), massFolder is held only to be a text and not the prototype folder. A massFolder inside an input root is not refused as D19 asks, the guard WorkFolder.Inside sits in the add-in and Common holds none.
- MassFiles.cs:137, Stale casts readerVersion by (int?), a sidecar edited by hand to a text or a decimal there throws in place of making the mass again.
- CadExtractor.cs:46 JoinedRing drops the bulges of a polyline, so an arc side of a house outline is taken as its chord.
- OutlineDistance.cs:138, houseMinAreaM2 is held against the ring area in drawing units, right while the drawing is in metres.
- PlaceVillasRun.cs:242, linkFolder prototypes or a folder writes footprint_<NBH>.json with no rows, those links give no base to measure, the counts say so.
- UNKNOWN, the mass file takes the name of its prototype (R450). After a Links run of part of the plots, a type of massFolder\VD4.rvt and one of the prototype folder VD4.rvt stand in one model. Whether Revit 2024 holds two link types of one file name from two folders is UNKNOWN, the runner probes it. Naming unchanged until then, with the mass base question of Q279.

## Lane B FIX-1NOV-B fix pass on task 3, R454 to R456

No real input reaches these: coordinates.source is positionFile (Q328), and the demo copies state UTM84-37N or no system, with no named position on the documents.

- AddIn/Revit/RevitSiteDocument.cs:153 SetNamedPosition with SetUpPlanner.cs:128 Changes: a document that already holds an inactive position of coordinates.positionName gets it made active, while Changes and the dry run were computed against the position active before. The read back after Apply shows the gap, the dry run does not.
- Common/SetUp/SitePosition.cs:52 and CoordinateSystems.cs:186: a stated geographic code such as EPSG 4326 that coordinates.systems maps, or a coordinates.expectedEpsg of no UTM zone, throws in UtmMath.ZoneOf before the expected code stop of R454 is listed.
- CadReader/CoordinateSystemRead.cs:141 OfLandXml: a LandXML that names its system only by horizontalCoordinateSystemName, with no epsgCode, is read as stating none.
- CoordinateSystemRead.cs NameOfDefinition: a well known text whose name coordinates.systems does not hold stops button 1 as a name of no known code, even when it is the same system as UTM84-37N.
- RevitSiteDocument.cs:153 SetNamedPosition: the position name is matched with case, so a position of the name in another case is not found and the active position is renamed beside it. Whether Revit refuses two names that differ by case is UNKNOWN.

## Lane D FIX-1NOV-D, review 1 and breaker 1

- Breaker S1, VillaInputs.Of and PlaceVillasRun.WriteParameters: a sheet that gives RRE_ZZ_SP_Plot-Number another button, none or Keep No turns off the R85 guard, button 7 places a link with no plot number and an untagged link is no longer known as placed. The sheet of the repo gives it 7.
- Breaker S4 and L4, python/parameter_sheet.py: openpyxl read only stops at the stated dimension and takes row 1 as the header, ParameterBook reads every row and skips an empty first row. A dimension smaller than the data or a header below row 1 parts the two. The sheet of the repo states A1:H12 with the header on row 1.
- Breaker S5, python/parameter_sheet.py: a GUID of 36 characters with its hyphens moved or in braces passes Python and is refused by C#. The sheet of the repo holds 11 GUIDs of the D shape.
- Breaker S6, key_parameters.against_sheet: a name the keys give two GUIDs or none gets no fault line. Plot is the only noGuid name of the keys, by Q156, and no conflict is in the 7 keys.
- Breaker L3, new_project_check.parameter_line: a file of no xlsx raises BadZipFile, not caught, where the C# side gives a does not fit line.
- Breaker L5, ParameterBook: a shared string index of no number and a relationship with no Target throw outside the R462 message. openpyxl and Excel write neither.
- Breaker L6, ParameterSheet.PathOf: a config in a drive root throws, C:foo.xlsx counts as rooted in C# and not in Python. The config is under config of the repo.
- Breaker L7 and Q330, RevitSiteDocument.BindParameters: the group labels follow the Revit language and one built in group with no label would stop every bind. Not run in Revit.
- Review 1 F6 and breaker L8, SetUpPlanner.Stops: a Plot row with no GUID skips the check that Floors fills it. The sheet of the repo gives Plot its Q156 GUID and Filled by 5.
- Review 1 F8: the data type is written as given with no check, every row of the sheet of the repo is TEXT.
- R439, ScanStep hash cache: two writes of one size in one tick give a stale hash, no real input reaches it. R438_ScanStepTests gives each rewrite its own write time (Lane C, PR 126).

## Lane B Task 6, review of R457

No real input reaches these: the plot table of the demo holds whole numbers and clean area keys.

- ButtonScope.NbhCodes keeps any plot with a number and an area key, Resolve skips a number that is not all digits and trims the key. An NBH whose only units are numbered 12A is offered, then stops with holds no unit plot.
- PaneRunState.RefreshNbhCodes catches no InvalidDataException or ArgumentException. A work folder path with illegal characters would escape into the view activated handler.

## FIX-1NOV-A2, review 1

- ReportRun.cs Find and PlanOf type the stop of no 4 Check run twice, one text, no input reaches a difference.
- UpdateFromReportRun.Read gives PlanOf WorkFiles.ReadText and reads the plan again after the worker, a short window after its SHA-256 check.
- ReadPicks.Resolve repeats the proposal rule of ScanView (Lane C) and falls back to the proposal on a stale pick where the window asks again. One helper in ScanView, a Lane C change.
- ReadPicks.FilesOf reads addin.workbookRoles itself, ReadSettings.WorkbookRole is the rule of the worker, the same role on a valid config.
- ReadPicks.LatestScan reads a broken scan.json as no scan with no line. The scan writes by a temporary name and a rename.
- ReadPicks compares file names ordinally, both from one GetFileName. A plot outline source splits on ' + ', a layer named with it splits wrongly. picks.json is indented with the line end of the system.
- Breaker 1, R474: read.json sources record the resolved picks, a sheet pick whose header the worker does not find or a conflict pick ApplyConflicts skips still reads pick. No real scan offers a sheet villa type, 104 of 104 conflict plots are plots of the read.
- Breaker 1, a saved pick no longer a row of the newest scan falls back to the proposal with no line, and ScanStep.Latest may be a newer scan than the window showed. No real changed drawing reaches it.
- Breaker 2: Take gives a line and Resolve none for a refused proposal, only with picks.json resolved on other files than the request. The folder hash holds picks.json.

## FIX-1NOV-C, R445 Save setup

- Breaker 3: a workshared model synced by another user keeps the old settings in the pane, Save setup and button 1 then write them over. The test host runs one user.
- Breaker 5: a session made without LoadFrom (OnCreated, Seen) holds the defaults, so a template that carries the entity would be written over. No template carries it.
- Breaker 7: Save setup writes and adds an undo entry even with no change.
- Breaker 6: New project now waits for Revit to be idle, a second click while it waits is dropped. Loud, no wrong value.
- Breaker 8: a commit that returns Pending would roll back in the finally, failure handling is never delayed.
- Review F7: SettingsStore.Save repeats the read back of Record. Review F8: Schema and Entity of SettingsEntity are not disposed. Review F9: the result of ExternalEvent.Raise is not read.

## FIX-2NOV-C, R446, review

- PaneHost: Process.Start of Open folder has no catch. A Win32Exception would leave the click. explorer.exe is on every PC of the tool.
- ScanResultsSession.InsideInputs refuses the folder of every input. An input directly in Documents would refuse the default work root. Real inputs lie under the input roots.
- NewProjectViewModel.CanChange is always false, so the two Change buttons have no Click and stay collapsed. The sweep skips a collapsed button.
- No test holds Closing += OnClosing or the IsVisible branch, a shown window cannot be made in a test. The sweep passes any handler, an empty one too. Part 13 of steps/bader-fix-1nov.md checks the ask in Revit.
- WorkFolder.Inside compares by StartsWith, so inputs2 counts as inside inputs. The refusal only errs to the safe side.

## Lane D D3, R467, R468, R405, review

- ButtonsRunHost.Finish saves before it writes the result. A hung save past the step limit of 10 or 20 minutes ends Revit before the entry of that button is written. D3L ran 5.4 minutes in all.
- The test host log says ok for a button whose save failed, the failure is only in savedAfter. Write has its own unguarded Save after it. No save failed in D3L or D3S.
- villaLinkFolder with a full path is read in place with no hash before and after, only the prototypes input is hashed. The D3 specs name prototypes and masses only. prototypes with no prototypes input fails late, inside Revit.
- The R467 and R468 tests hold the text of the host and the script, the usual style for the test host. No test reaches the catch or the value of savedAfter. The runs D3L and D3S do reach the save.
- result saved of Write now seldom appears, since R468 saves first. Nothing reads it.
- The two R405 tests of AddInTests.cs set and read DesignValues.Current outside the alone collection, while other tests call UseConfigOf. No failure seen there.

## Lane D 6B-DATA, R469, review

- zone_compare.py stops the whole zone on a ramp corner list with no points, max and min of nothing. No plan of the 7 zones holds one.
- zone_compare.py reads each field through plan_compare.value_of, which swallows a missing key, so a renamed plan field would show only as toolNone counts. The 7 zones show none.

## Lane D 6B-DATA-REV6, R469, review

- zone_compare.py checks only the plan of --rev6-plan: a rev6 plan handed by hand as --without-overrides would pass under that title. The 5 zones hand the past job plans.
- check_rev6 shows a plan is rev6, not that it holds no override list, so a rev6 build with a list would read as no override list. The rev6 builds of the round name no list.

## FIX-2NOV-A, review 1 and breaker 1

- Review 1, the step floors cuts a yard by the ramp corners of a plot with no ramp type, the build does not. Only the native2022 profile reaches it, Q050 keeps it off.
- Review 1, a yard the cut empties with its level also missing gets another line in Python and C#. Breaker 1: 0 yards of the whole project are emptied.
- Review 1, FloorPlacer.EdgeNoise 1e-6 is a constant beside designValues.floors.shortestEdgeM. Clipper2 2.0.0 and pyclipr's 2.0.1 are one patch apart, parity held on real plots.
- Review 1, ReviewTable.cs and ReadDataRun.cs each hold a rule of which plots are units. Install-TestHost.ps1 gives the watcher Common with no Clipper2Lib, it does not reach the cut.
- Breaker 1, review_012.json keeps 42 checks of plots R476 leaves out, all OK, a Wrong on one could not be cleared. No real Wrong.
- Breaker 1, R477: no unit or no block name reads as other type and passes. Every real plan plot has a unit, the read names every block.
- Breaker 2, the Assumed row Q350 is 68 yards short: a yard with a link override reads from override, RECOMMENDED, with no R475 in its detail. Its level is right.
- Breaker 2, a yard cut reads RECOMMENDED by Q350, now closed, as Q053 and Q057 do, so Assumed names a closed question.
- Breaker 2, the rows R476 keeps on 01A, 02, 021, 022, 03 no pane path shows, the pick list holds 012 and 013, 11 of them hold many numbers (Q355).
- Breaker 2, a new DLL with a config of before FIX-2NOV-A reads a cut yard UNKNOWN, as with any config not refreshed (Q309).
- Yards still reach neighbours by under 2 mm of rounding on slanted edges, 286 and 86 pairs over 1 cm2 on NBH 012, so overlap warnings across plots remain (L2NOV-1).

## FIX-2NOV-A2, the helper's notes

- R480: two C10 on one edge leave open from the first start to the last end, 0 such edges on the 5 keys. An edge shorter than its span end stays whole, the lengths are 25 and 28 m.
- R479: a build with no 4 Check request gives no keo_file map, the footprint check compares by the own name with no line.
- Breaker 1, a mirror edit in Update from report on a C10 now moves its wall gaps, the apply line names no button 8. No real edit yet. A number in a type name such as BW 1800 is held out by the digit limits now. Near span rests on one length class, all side edges are 25 m but 2965.

## Q158 review and breaker, 2 Nov 2026

- A press on a step with DoneWithIssues writes 0 issues over the earlier record, so the step reads Done. Whether button 1 gives warnings on a real model is UNKNOWN.
- A sheet with no GUID for Plot gives a Waiting line and a press makes the step Done with 0 issues. R461 puts Plot in the sheet.
- A level outside the position file, moved by hand, stays old in site.json. floors.levels names only Level 1 and Level 2.
- An entity the reader refuses, as from a newer build, is overwritten by a press with the settings of the pane, as a real run already did.
- A check point over the pass limit with nothing differing rolls back at the read back with its reason, and the window gives no warning before the press. A model set by the tool matches exactly.
- While the pane has no input hash yet it shows Ready and the flow has hashed, so a press in that moment can hold and cancel. It lasts until the hash returns, not measured.
- An extra field added to site.json by hand counts as disagreeing and the file is written again.
- A level type with no base parameter gives a null in the facts, the file parses back as a null of another token type, so each press commits and steps 2 to 8 go back (RAN the compare). A Revit level type has that parameter.
- A level added or moved by hand, of no use to the build, fails the compare, so one press commits and steps 2 to 8 run again. Loud, no wrong number.
- A rules path that differs only by case commits once, then holds.

## R459 review 1 and breaker 1, 2 Nov 2026

- An AfterCommit that throws, or a move of floors_<NBH>_accepted.json that fails, comes after the warnings left the model. The line says the failure, the next step 5 raises none for floors left alone, so chip and sheet stay short. Needs a disk or lock failure.
- An unreadable earlier floors_<NBH>_accepted.json is taken as no earlier pairs and overwritten, while 4 Check shows no chip and 9 Report refuses the same file. Needs a hand edit or a read lock.
- FloorLoad.KnownWarnings reads the config on disk with no SHA hold, as button 6 has. No block or no file gives no list, so every warning shows.
- python accepted_rows raises on a file with a missing field where C# refuses it by name. No real file is malformed.
- A type named in two roles of floors.types takes the last. Today's config holds none.
- A floor moved by hand keeps its pair, the counts read floor ids, not positions. A later Move by step 5 does take it off.
- 9 Report with a floor reader that returns null lists every pair unfiltered. The document is active on the real path.
- ReportRun and CheckRun name the file of step 5 and one input role. A second step's list, as Lane C may add, needs both changed to show in the report and the chip.

## R483 review, 2 Nov 2026

- RevitCounts.ElementId casts each id to long and RevitCounts.Refusal does not check ids, so a hand edited revitCounts file with a non integer id throws rather than being refused. The add-in writes that file itself in the same run, so no real input reaches it.
- RevitCounts.FromTags counts an element tagged under two schema versions twice where FromElements gives one id. PlacedTag holds one version and PlacedTagStore.Write deletes older entities, so no real input reaches it until a version 2 exists.

## FIX-2NOV-A3 review and breaker, 2 Nov 2026

- valueStatus walls.ends wallOpen holds weak inline, rules/roshn-makkah.json holds the share 0.7409. If that share turns strong the inline entry stays weak.
- majority.front_edges_of takes every frontEdge line with no source filter or two entries refusal, the build filters and refuses. The list holds 6 lines, each plot once, all parsons.
- majority.py --keep-from an older list with no frontEdge, or --dry with a new list path, measures on the notch front. No current use found.
- corner_front.named_frame does not check the edge is the same edge after a re-read baseplan with another first vertex. The read is pinned by SHA-256, R388.
- BuildPlan.PlanWalls: a step with no endsSource reads keyRules and loses manualInside. A build remakes its steps.
- BuildSteps: a request with no overrides role takes the notch front. 4 Check asks every step.
- plan_from_book --override-fields without frontEdge drops the line, its insert line is gone too.
- Check table Use Rule on the frontEdge of 469: 469 falls out, 470 keeps -0.004 and stands about 55 mm off its key (worked out, not run).
- Lines left stale on the 5 notched plots, not applied by any build: rampOutline gaps measured on the old front, the reason no value of stepsFromHeight on steps lines.

## Lane D FIX-2NOV-D, review and breaker, 2 Nov 2026

- python/read_steps.py asks plan_rules.road_names with no basis, so the Python read steps take siteBuild.roadSurfaces under rev6 where the C# takes designBasis.roadLevel. They agree while the two lists are one, test_r490 holds that.
- python/level_excel_check.py takes the road order from config alone, so a level Excel of a build with a pick reads as a mismatch on the line Road surface order. No test host job names a pick.
- RoadPickState.Of reads and parses the config on each refresh of the pane and New project.
- PaneRunState.ConfigOf caches by path, time and size in four static fields with no lock.
- plan_from_book.py leaves an older plan at --out beside new step files after a stop. No script calls it so and it takes a person ignoring exit 2.
- Two files with one short name, A__Top and B__Top, show as two like rows and one name in the stop line. ["Road", "road"] passes the unique check and reads one file twice. A hand edited ..\..\x in a pick leaves the folder, read only. None of the real names do this.
- RoadSurfaceChoices.NotTakenWhy finds a short name as a whole name in the prose reasons of notTaken: a surface named as a phrase of a reason, Grading, is refused loudly with the reason, and Typical Cross Sections, written with an underscore before it, is not found.
- A saved pick naming a not taken file shows in the window as Not in the folder any more, the wrong words for it. It stays unticked and 2 Read data refuses it with the reason.
- noRoadStopShare 1.0 never stops, not even at 100 percent, a config value set that way by hand.

Round FIX-2NOV-C-CHECK, review 1 and the gate, 2 Nov 2026:
- WorkerRunTests R347 tests are load sensitive: 7 of 60 runs failed under 12 parallel test processes (RAN), a 1 s time limit fires before the worker answers, or the launch takes over 1 s. One CI run failed with status failed, the message is now printed, its cause UNKNOWN.
- Seven WPF tests failed once in a full net48 run while Revit started on the machine, all passed alone (RAN, 66 of 66).
- Plural by count stays open where no real input reaches it: Review data title, New project plots and types, ScanView plots and files, 0 of 1 rows.
- CheckTableState.Filter of two arguments has only test callers.

Round SPEED-2NOV-C, review 1 and breaker 1 of R447, R448, R449, 3 Nov 2026:
- test_window_lock.py leaves SITE_ADAPTATION_WINDOW_SLOT set in the test process, no other test reads it.
- A crashed holder of slot 2 is not cleaned while any window process runs, it clears when all windows end. It takes a killed terminal.
- With SITEADAPT_LISTENER_INPUT_ROOTS unset the C# side of the listener refuses no input root, only Start-RevitListener.ps1 sets it, and it always does.
- If every result write fails, the listener logs to Trace and retries each second, and the queue stands behind that job. Disk full or a read-only out folder.
- A failed SaveAs of listener_home.rvt leaves the small project open.
- AddinRunSession reads the start time of its process with no guard, test host only.
- A worker thread that ends before NextStep(WorkerStep) gets a wrong category in the timing, never a wrong outcome.
- A refusal the out folder blocks for one moment leaves the job in claimed with no result, the send exits 2 (RAN). A job that passed its check retries.
- A hand-made job naming a used run folder reports the old run_timing as its own (RAN). Send always makes a new folder.
- InsideRoot of the listener compares text only: forward slashes, a parent step, the long prefix and an 8.3 name pass it (RAN). IsPlainFullPath and CheckPlace stop them for a model. prototypeLoops and junctions as in or out folders are hand-made holes.
- Revit does not wait for Civil 3D on the slot side, Civil 3D waits for Revit. python/c3d_probe.json names a lock of another machine, so no Civil 3D run takes it today.
- A new process with the pid of a hard killed listener reads as a listener already there, until that process ends.

Round SPEED-2NOV-C, review 1 of the delta, 3 Nov 2026:
- A step past its limit that returns later overwrites its timeout line with ok in the job log. The result file keeps timeout.
- A result file removed by hand during the second marks stops the queue until Revit restarts. No script removes it.
- A heartbeat file held open makes that look of the listener skip its work, retried each second.
- The second marks of a job past its limit can repeat the D19 line in the error.
- A steps entry of buttons_result.json that is no JSON object would throw out of the look each second, ButtonsRunHost writes objects only.
- RevitFormat.cs is a port of python/rvt_info.py with no parity test. Two small drifts in how it reads the Format key, neither on a real prototype read.
- Three copies of the input root list: Start-RevitListener.ps1, Get-RunInputRoots, Run-TestHost.ps1. They agree today.
- Timing labels only: the second Decide runs under villaMassFiles, a wait on the load dialog is billed to the step before.
- The pane lists the Prototypes folder twice per refresh. RevitFormat walks a long chain only on a corrupt file.

Round SPEED-2NOV-C, breaker 1 of the delta, 3 Nov 2026:
- A person's Cancel on the load dialog of button 7 ends as RolledBack with the failed text, the catch of ButtonFlow is older than this round. No recorded run reached it.
- The long prefix and a UNC path of an input root read as outside it in the check of a .slog (RAN, strings only). The real specs name plain drive paths.
- A config with no input root leaves every .slog of a backup folder out of the check. The real config gives 3 entries (RAN).
- A file removed during an own run stops the after check with Get-Item's error, loud with the wrong cause. The listener says could not be marked.
- The scope of RevitListenerJobVariable disposed twice would clear the variable of the next job (RAN). Only Finish disposes it, once per job.
- A person's click on button 2 during a listener job skips the scan gate and its worker counts in that job. The lanes only send.
- R496 stays silent: a 2021 file past the 20 files it reads, a file from before Revit wrote Format, a file locked with no share, an empty row Prototypes where the build falls back to config. Real folders hold 13 files, 2021 or 2024 (RAN).

Round SPEED-2NOV-C records, 3 Nov 2026:
- R347_the_launch_returns_at_once_and_a_worker_past_its_time_limit failed once on the Windows CI runner on net8.0, status failed, the worker ended with code 0, on PR 152, records only. Same flake as the R347 lines above, net48 passed in that run. Test left as it is.

## Lane D MASS-2NOV-D, review and breakers, 3 Nov 2026

- Arcs: no arc wall in the 13. Not tested: a clockwise arc wall, a concave one, a far face arc, arcs meeting at an angle, a full circle, a radius under half the width. What Wall.Orientation gives on a curved wall is UNKNOWN.
- WallRingBuilder.Walk is greedy and stops on its first edge. A wrong early close that skips a part passes when the share stays at 0.90. None of the 13 does it.
- A join 1 to 5 degrees off parallel crosses beyond the corner gap, the ring is refused. Real angles are 90 and 180. At an acute corner the end of one wall pokes through its neighbour and splits that edge.
- Start corner of the points moves with the order of walls on a tie of two equal sides, TH1 and TH3, same shape. Only the point order of the sidecar changes.
- A rejected ring keeps its build, so a sidecar could list wallsUsed under source slab. A null slab throws a bare null error, the reader throws first. None of the 13 reaches them.
- MassFiles.Stale tests the reader version by not equal on one shared folder, so a build of another version and this one remake each other's masses. It ends at the merge.
- faces_check2.py takes the whole face of a wall at all heights, so an edited profile counts as covering above its ground stretch. Breaker 2 read the profiles and found no fault.

Round FIX-3NOV-B, R498 and R499, review 1 and breaker 1, 3 Nov 2026:
- A failed move puts back the run before while the model keeps the new one, so a rerun of 5 to 8 with an output held open shows the old run Done. The HeldOpen message says so. No real output of 5 to 8 was found with a name another program holds.
- An exception other than an IO or access fault between the commit and Keep disposes the group, which rolls back a committed run, loud. The status of Assimilate is not read. No real input known.
- The settle write failing after a failed move says the step is done though it is not, and the first write stands. Needs a held file and a refused transaction at once.
- The record can roll back a step: a workshared model with ProjectInformation held by another user, whether SetEntity throws is UNKNOWN, the host runs one user. An empty roadSurfaces pick, whether the window can give one is UNKNOWN. Issue text with no cap, the size limit of a Revit string field is UNKNOWN. All loud.
- Clocks of two PCs: a rerun of 3 on a slow clock stays older than 4, and 4 stays Done. An entity of a later formatVersion is overwritten by any button 2 to 9. A session made by Seen with no LoadFrom holds defaults, the flow stops first with no config chosen.
- The pane stays live while a worker runs, with no busy guard. A pick of another NBH during 4 reads Done for it, in the session and after a reopen, and step 5 then stops loud on no plan. A press of 1 during a worker: the end is later, so the worker reads Done on the site before. Older in the session (PaneRunState.RunOf), read only.
- Button 1 keeps no outputs: site.json deleted by hand shows row 1 Ready in the session, Done after a reopen, row 2 holds by its own. Next to Q382.
- The test host keeps its work folder apart from WorkRoot, so after an undo or reload every row with outputs reads Ready in a host run. A false fail, no job reads the rows today, a Revit proof of a reopen goes through the windows.
- A hand edited entity with output keys that differ by case or hold an invalid path character throws from StepRun.FromSettings. A machine file broken while Revit runs now throws from a pane refresh. A work folder copied by hand keeps absolute names in read.json.
FIX-3NOV-A, breaker and review, no real input reaches these:
- CheckRun never adds the role civilFile, so under rev6 with doorLevel.source civilFile button 4 stops as floorOutlines did. The shipped source is roadPlusSlope.
- python/zone_compare.py:205 reads the mode with no project mode, an offline compare of the past job (Q224).
- CheckRun.cs:476 wraps a worker refusal of R485 as the build ended refused, R493 passes plain. The read and the plan stop it first.
- design_basis.py:159 lower against OrdinalIgnoreCase of RoadSurfaceChoices.cs:47 differ on odd Unicode, the stems are ASCII.
- A settings file with a mode its config does not list is refused whole, SavedStarts leaves such a file out. It needs a second project whose past mode has another name.
- A config with no notRoad reads as an empty list under rev6. All 4 older copies of Saved projects lack it, none reaches rev6 since a project that names a config stays on its mode.
- A reinstall leaves every project copy of R486 as it was, a later rule fix in config does not reach it, and nothing records the install that made the copy.
- Settings hold the absolute path of the copy, a work root that moves keeps writing under the old root while that folder stands.
- A model name of about 92 characters or more can reach the name limit of R486, not tested.
- PickConfig has no production caller, the pane never picks a settings file.

Round 6C-3NOV-C, review 1, 3 Nov 2026:
- ScanView.cs about line 170, StartsWith |xref repeats the form CadScan.cs line 190 writes, with no shared constant. A rename shows the file Not read, the safe side.
- ReviewDataWindow.xaml, Mirror column 72: an unread mirror shows not read yet, about 71 against 64, cut with an ellipsis. All 337 plots of the run read Yes or No.
- Review data, the head Team check sits about 10 px left of the chip text, cosmetic.

Round RULES-3NOV-C, review 1 and breaker 1, 3 Nov 2026:
- RuleValues.Read stops the table when a rules file holds no number at a path of the catalog, a loud box, and Manual rules can not be reached from it. The two rules files on disk hold them all.
- RuleValues.Text writes 3 decimals, a value typed with more shows rounded in an export. All real values hold under 3.
KNOWN-3NOV-A, review and breaker, no real input reaches these:
- CheckTableBuild.cs:33 reads overrideDefaultSources with no check. A config with no such key would grey every Past, PlanOverrides.Load stops such a run first with the line of R330.
- A key listed in two groups of majority.groups: OverrideEdits takes the first group, OtherJobKeys the last. The groups of config are apart.
- REPORT-3NOV-B: ReportFields has no writer until Lane C's New project section. A volume with no <nbh> passes on one NBH and Update then stops. A report left under a name of fields since removed is not named. A plot of two numbers has no old number in Plot schedule (0 such plots on 7 NBH, RAN). Old revit counts give no Walls line.

## Lane D PARAM-3NOV-D, review 1 and breaker 1, 3 Nov 2026

- The unfilled template of TemplateBook at the config path gives a green 0 parameters. A sheet that does not read is Found in Check files of New project and amber in the pane. None is in use.
- An xlsx with a relationship lacking Target throws a null error in ParameterBook that DiskProbe.ReadSheet does not catch. No real sheet has one.
- A Filled by 7 row of a hand edited sheet that names Floors and no RVT Links stays on Floors, button 7 finds no parameter on the link. No such sheet exists. ParameterSheet.Counts says 1 parameters for one row. The real sheet has 11.
- RefreshSheet reads the config and the xlsx on the UI thread on each refresh, uncached, not measured. A swallowed config failure reads Not chosen with no reason.
- docs/design/07-mock.md still says Plot on floors and walls. R507 prevails.
- A rebind of a links row that also lacks the link category counts as a removal alone. No model holds a row off the links and on Floors.

## Lane D MASS-3NOV-D, 3 Nov 2026

- R530 builds no cut across an arc, no prototype has an arc wall. A concave side crossed twice stays whole. Neither is in the 13. A new prototype needs its own party lines in config, none means no cut.
- MassCut.NoShortEdge drops the next corner and keeps no longer neighbour's line, unlike R503, so a long edge would slant by up to 4 mm. No corner goes on any of the 9 cut rings, shortest 20 mm.
- After a cut wallRingMinShare and the slab middle are not tested again, real shares stay over 0.9. A read back drops NotCut and UncutAreaM2, no run line lists a refused cut, none of the 9 is refused. A misspelt maxX cuts nothing and says nothing. A revised prototype is remade against the old line.

COORD-3NOV-A, review and breaker, no real input reaches these:
- newProjectCheck has no sender in the add-in. WorkerRequest.CoordinateSource and PositionFile wait for one, until then the check reads coordinates.source of config.
- ProjectElevationM once coordinates.originElevationM is not 0: ReadRules adds that height to every level, no test holds it against Revit. Config holds 0.
- A corrupt position file under CAD: the check line covers a JSON, format or IO failure, a parser failure of another type fails the run.
- A picked position file of another job: any file of the format is accepted. Under CAD the row File origin to the plots shows the distance, under the file nothing does.
- A baseplan revision on a held CAD model: a new plot box moves the rounded centre, only Will change says so, unless R523 stops it.
- More than one named location: the read and the check take the active one.
- Under CAD the levels come from the profile of config, not the rules file picked in the window.
- A baseplan in local coordinates whose box falls inside the UTM range passes OutsideZone.

## Lane B 6b, breaker 1 of R557, 4 Nov

- A unit number with a space or letter: IsUnit true, Resolve skips it, NbhCodes offers the NBH. 0 of 2,558 real rows.
- A unit row with no nbhId is filed under an empty NBH and counted in Whole project. 0 real rows.
- A number held as a JSON number reads as no unit. PlotTable writes strings.
- A label plus shift that hits a schedule of another NBH gives a wrong official number, which UnitNumber prefers. 1 of 747 differs from the location number, 2312, which is right.
- noTemplate with a keyCopy opens the key copy, [bool] of the string false reads true. No spec does either. The guard still asks for the template file of a no template job, harmless.

RAMP-3NOV-A, review and breaker, no real input reaches these:
- A civil file door source, path null in config: the door stays the file's, a back joint takes its first corner, a ramp can stand off El @ Entry.
- join true with joinBack false: no config holds it, only tests run it.
- A joint mixing front and back corners, 0 of 4,121. Chains of 3 plots or more, 0.
- A 2% unit whose only back partner lies outside the build: 0 plots differ on NBH 012 and 5 TU zones against the whole, ima and imb not compared.
- A partner the plan never writes sets a written plot's slope: 14 back joints, no level moves today.
- A corner past maxMitreM stays unpulled, largest real move 3.162 mm against 10 mm.

## Lane D TOPO-3NOV-D, breaker 1, 4 Nov 2026

- RoadTopography: a scope with no ramp passes with no row, nothing proven. RoadTopographySettings.Number takes any range, a gap of 0 or a margin under 0 needs a hand edit. Grow mitres a sharp hull corner far out, NBH 012 matches shapely's limited mitre with no difference.
- FloorsRun: a surface delegate giving null ends in a null error line, and a plan with no NBH runs as all. FloorLoad.RoadSurface: a config path gone from disk ends in the plain file error, as an issue line.
- RevitFloorDocument.CreateToposolid duplicates the first type whatever its thickness, the report names it (R572).

## Build-1 R1-build-1, stage 1 data check, second review, 5 Nov 2026

- DataCheckFind.cs:166 BreakTies takes the candidate with the fewest layers when candidates tie and never checks it is a subset of the others. 0 of the 5 runs broke a tie.
- DataCheckFind.cs DecideWords, a sheet header that parts the plots as the groups do. Narrowed in R2-C1-build-1 by R634, the column must name two groups and wordShare of the groups, so a one valued column no longer fits a level of one big group and a lone plot, as it did on the real inputs. A 2 group level still fits a true 2 valued column.
- config/project.json dataCheck.scan.labelPattern has no status entry, the past profile holds the same name, so the status tool treats it as shared.
- No unit header gives wrong groups with a weak mark and a line: DataCheckSettings.cs:131 and :144, DataCheckFind.cs:74. All five real runs state metres.
- An insert in a hole is credited to the plot for villa type, DataCheckFind.cs:252. Only the mirror leaves the hole out, HoleWhy says so.
- A scale of 0 reads not mirrored: CadScan.cs:97.
- Numbers are compared as exact text: DataCheckFind.cs:465 and ScanLogic.cs:318. Only a risk if a drawing mixes case or leading zeros.
- A killed run or a Ctrl+C can leave the developer add-ins until the next run or install, TestHostPaths.ps1. Two runs a few milliseconds apart can race on them. Developer PCs only.
- Measure-ColdStart.ps1 treats the Invalid Signature box on the tool's own files as another maker's, a matter for SETUP-sign-2.
- Run-RevitTests.ps1, the finally builds a new process tree, a Revit left by a dotnet that has ended stays open. The test does not reach Revit today.
- Flaky in job tests, cause not measured: R104 FreezeGuardTests, R347 worker time limits, R405 AddInTests on net48, a value came back null in 12 ms, which reads as shared state between tests.

## Build-1 R2-C1-build-1, stage 1 gaps, 5 Nov 2026

- DataCheckCodes.cs ColumnCells: a plot listed twice on a sheet takes the first row, a second row with another code is not said. The proposed column of the real run lists each plot once, not measured for every candidate.
- DataCheckCodes.cs TableAt: a code table is two columns side by side, a column between them hides it, and the person types it. Its header reads as a column letter when a title row sits above it, only the source text shows this.
- DataCheckCodes.cs TableAt: the old side is the one the drawings use more, a table whose drawings use neither reads left to right.
- DataCheckCodes.cs Typology: a column of code shape that runs across the groups and differs from the drawing qualifies though it holds no type, it ranks last with no plot of a prototype. The window shows the first and the rest.
- config/project.json dataCheck.codePattern and groupNamePattern have no status entry, rule_status.py reads them as an input, as dataCheck.scan.labelPattern.
- ReadInputs.cs FolderInput: a work folder inside the input root would change the hash of the folder on each run, so no read is reused. Runs write under the work root, D19, and no project has it so.

## R2-C1-build-1, second review, 5 Nov 2026

- ReadDataRun.cs:305 Reusable ignores files not read and a stopped check, so a raw read made while a file was locked is reused on the next press with no new run. Revit-test's wiring of NoPastFiles reaches it, so look again then.
- DataCheckAreas.cs:154 ApplyArea takes the first area of a plot listed twice with two areas, with no word.
- DataCheckCodes.cs:58 KeyColumn takes the first column on a tie, so a serial number column could join codes to the wrong plots. The scored runs joined well.
- DataCheckCodes.cs:348 FindColumn with no workbook takes the first sheet of that name.
- ReadInputs.cs:283 FolderInput throws on a locked subfolder where the worker notes it and carries on. It is said, not silent.
- DataCheckMarks.cs:366 the mirror line says the folder does not give the hand even when hands are confirmed, if every block is of another type.
- DataCheckCodes.cs:674 typologyColumn reads usable, gave 0 plots a code, when a confirmed column holds only words. The villaType need does read missing.
- DataCheckLogic.cs:93 the 160 character cut can drop the tail of a long villaType line. The mark stays weak.
- DataCheckMarks.cs:485 areaM2 under an assumed unit carries no weak of its own. All five real runs state metres.
- Plots a confirmed area column does not list have no neighbourhood and cannot be picked in Review. The count is said in the line.
- codePattern and groupNamePattern have no status entry.

## PR 208, review, 5 Nov 2026

- make_scenarios.py:91-93 styles only the kept plots, as answers and labels leave out the held areas. The S5 folder on disk, made earlier, holds 2956 of 2964 numbers in the other style with all 610 held plots found by --styled. A fresh S5 would leave held plots plain, and --styled would then read a loud 0, never a false pass.
- No test calls main of score_holdout.py, so test_scenarios.py:228 proves in_style and not the --styled flag. main reads the answers under the work root.

## PR 209, second review and the lane, 5 Nov 2026

- No test reaches PaneViewModel.cs:697 or PaneRunState.cs:70-75 on a raw project, the test calls InputsInFolder alone. By reading the code does what is said.
- A changed file in the input folder leaving step 2 not done in the pane has no shot. Step 21 and 22 show the confirmed file side only.
- R636_ConfirmWindowTests.cs:242-248, Cancel_saves_nothing starts with no earlier file. ConfirmProfileSession.cs:59-63 does not delete, so the behaviour holds. The guess help and the map doubt are tested on the model, the Visibility lines at ConfirmProfileWindow.xaml.cs:28-29 are not, and no Revit pass showed a guess or a doubt.
- InputHash.cs:31 against ReadInputs.cs:291. The flow walks skipped folders and follows links, Raw and the worker do neither. A link loop or an unreadable folder in a skipped folder would stop every press at ButtonFlow.cs:99. S1 has none.
- PaneRunState.cs:87, ConfigOf can return null if the config locks between two reads, a race. The comment at :77 says Settings only and the code adds two more.
- R2-C1-fix-build-1, review of PR 216, no current input reaches these four. ReadDataRun.cs:385 NotWhole counts every row of files with a status other than read, and that includes the confirmed column notes of DataCheckFind.cs (:201, :209) and the (plot read) note (:240), which are no file. A column that could not be applied makes the read never reused and the line says files or folders not read, the first is a sheet and column. The confirmed S1 run applied its column and lists 7 files, 0 not read.
- DataCheckAreas.cs:204 says Two columns of sheet carry the name even when three or more do, the count after and is of the other names.
- ReadInputs.cs:278 now catches a locked or broken settings file inside Schedules and DataBook too. In past job mode a locked config used to throw from NoPastFiles at ReadDataBody.cs:77 and now goes to the old read, which throws from Make at ReadInputs.cs:109 with its own message. Only a locked or broken settings file reaches it.
- ReadDataRun.cs:372 NotWhole catches only a JsonException. A data_check.json another program holds open raises an IOException from Reusable or Preview.
- The folder input costs about 10 ms a refresh on S1 of 37 files, on the UI thread. On a real root of many thousand files it is not measured, and it grows with the count.

## PR 211, the lane's list, 5 Nov 2026

- A second rulesSetup has no way past but deleting the file, RulesSetup.cs:43.
- A project set up from past-job.json can never fill M01 to M15, RulesSetup.cs:43 and RulesChange.cs:73.
- RulesForBuild.cs:240, :246 and :255 name a rules table no window has yet.
- ProjectRulesFile.cs:214 copies InsideRoot of RevitListener.cs:87 and leaves out the input root variable, InputRootsVariable at Common/RevitListener.cs:21, read at :67, which NewProjectCheck.cs:717 reads too. Corrected by review of PR 214, the lane gave :114.
- Rules beside the project profile are not enforced, the request names the folder.
- Values have no declared type or unit. Every manual value is a text, so build code that casts one to a number will meet a text. R2-C2-build-1 reads this.
- An id with a trailing space in the file can never be changed, RulesChange.cs:15 trims the id of the request.
- A log line per change is never trimmed.
- python/rule_status.py:247 reads the rule text of docs/rules/0*.md only, so a CHOSEN mark in a rule line of the parts 10 to 16 gives no status.
- A config or a template that changes during a run gives exit 4 from the runner after the command already wrote project_rules.json and the log. Not tested, a run takes under 2 s.
- A stop between the log line and the rename leaves a temporary file beside the rules, which no reader takes. Not tested by a real kill.

## PR 211, second review, 5 Nov 2026

- RulesForBuild.cs, a hand edited log line whose who is not text makes Check throw instead of giving a line.
- RulesLog.cs reads a line with failed set to null as a failed line.
- A value of the JSON word undefined passes NoValueReason.
- ChangeLog.Open catches only IOException, an UnauthorizedAccessException gives the raw text.
- ProjectRulesFile.Config uses Read and not ReadOrRefuse, the runner reads the config first.
- A person who edits both the rules file and the log is not caught. A source edited from past to rule on a row not confirmed passes the past gate under warn.
- A rules file away from its log reads as log gone, every row not confirmed, under block a stop. siteBuild of R2-C2-build-1 must pass the project's own path.

## PR 213, review, 5 Nov 2026

- Clauses of R639 with no test of their own: a folder not read, past job mode unchanged, which R635_past_job_mode_keeps_the_old_read_and_its_stop holds, and no file to edit. The finished read wording is tested on a table built by hand, not through the worker.

## Gate, R310 under load, 5 Nov 2026

- ParsonsModelTests.cs:126, R310_a_sample_keeps_the_process_memory, Expected Integer, Actual Null: a CIM call of the sample comes back empty when two workers run. 3 of 3 failed while two scenario workers ran at 22:06, 1 of 1 passes alone in 23 s. It failed the quick set twice on 5 Nov, both under load.

## Gate, R310 at idle as well, 6 Nov 2026

- The line of 5 Nov above says under load. On 6 Nov at 01:00, an idle PC, CPU 17 percent, 39 of 64 GB free, no worker or Revit, the R310 tests of ParsonsModelTests run alone twice: run 1 failed R310_helpers_running_before_the_window_are_listed_by_name_and_private_memory_and_never_counted_as_its_children at ParsonsModelTests.cs:204, run 2 passed all 12. The stop hook at 00:55 failed R310_a_sample_keeps_the_process_memory_when_a_cim_call_fails at :126 and R310_a_sample_counts_the_processes_the_window_started_and_their_memory. A sample that passes takes 21 to 31 s. So three R310 tests fail at random on this PC, load makes it likelier. steps/history/latent-older-2026-11-05.md:159 says they pass on an idle machine, that no longer holds.
