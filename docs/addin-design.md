# Site Adaptation add-in, the design

Round ADDIN Phase 3b, 2026-10-26. Written before any add-in code (R343). Phases 4 and 5 build what this file says, or change it first in the same change. The rules live in docs/rules.md, this file names their ids. The approved mock is docs/addin-mock.md, the libraries are in docs/licences.md.

How each fact is known is written beside it: READ with the file, RAN with the count, SEARCHED with the link, CHOSEN for a choice of this design with its reason, UNKNOWN for what no run has shown yet. A coordinate is written [coordinate] and a level [level]. The same pages fully filled with the real IMC values are in Documents\SiteAdaptation\round_addin\design\mock_filled.md, outside the repo.

Contents:

1. Option B and the two parts
2. Folder structure
3. The eight buttons
4. From each input file to Revit
5. The plan builder steps Phase 4 moves to C#
6. Settings
7. Every screen and file of the mock
8. Install and the unattended runs
9. Risks
10. Planned rule ids, conflicts and questions

## 1. Option B and the two parts

Option B (Bader): the whole tool runs inside Revit 2024, in C# on .NET Framework 4.8. No Python on the user's PC. Python stays in the repo as the checker of the C# and as the builder of the data book, nothing of it ships (READ, the round prompt and CLAUDE.md).

Every new build takes the level profile parsons2024Rev6 (Q050), whose keyRules hold the rules the 7 Rev 6 Parsons answer keys follow (R337, R338, R340), then the parsons overrides of config/overrides/roshn-makkah.json (R280, R330). python/build_proof.py holds that Python build against every key plot (R340). The C# of Phase 4 must equal that Python build, plot by plot (section 5).

Part 1, this round, is the add-in: the ribbon tab, the dockable pane, the eight buttons, the Set up site dialog, the check table, the level Excel and the report Excel of the mock.

Part 2, after Bader's check of Part 1, is the output sheets. It is not started. Where it attaches:

- Ribbon: new buttons in the Output panel after 8 Report. Part 1 adds no placeholder button.
- Logic: Part 2 reuses the plan of R80 that button 3 writes for each neighbourhood (`plan_<NBH>.json` in the work folder), the plot table of button 2 (section 4), the element tags of section 3 that find each placed link, floor and wall again, and the level rows of button 6.
- Data the plan files keep for it, per plot: the official number and the number of the model, the link origin in shared coordinates (the setting out point), the rotation and mirror, the generic and yard outlines with their arcs, the four ramp corners with their levels, FFL, SSL, El @ Entry, height and steps, the unit and the prototype file, CASE and the parameters, and per wall its id, type, length, height, base and top, location line ends and plots. R325 keeps the same fields in the key plans of the answer keys (READ, round-addin.md Phase 2 part 2), so a sheet can be tested against a key.

## 2. Folder structure

```text
dotnet/
  SiteAdaptation.Common/     the logic, netstandard2.0, no Revit reference
    (today's files)          placer and floor decisions, plan file, site coordinates, site facts, JSON, units
    Read/                    LandXML surfaces, the plot table, the location plan pairs and fit, settings
    Plan/                    clusters, fronts, link frame, hands, outlines, levels, walls, overrides, parameters
    Check/                   check rows, step state, scope, element tag matching, dry run summaries
    Output/                  the rows and sheets of the level Excel and the report Excel
  CadReader/                 the worker, an exe, net48 for the user and net8.0 for the Linux tests
                             DWG by ACadSharp, PDF by PdfPig, xlsx read and write by DocumentFormat.OpenXml
  SiteAdaptation.AddIn/      Revit 2024, net48, WPF: App, Commands, Pane, Dialogs, Revit, Storage, Icons
  SiteAdaptation.Tests/      the one test project, net48 for everything, net8.0 for Common and CadReader
  RevitPrototypeReader/      kept, the reader of the answer keys
  SiteAdaptation.TestHost/   kept, gains the jobs of the buttons (section 8)
  SiteAdaptation.Placer/     kept until nothing uses it, then removed
  SiteAdaptation.DialogWatcher/, SiteAdaptation.C3DProbe/, SiteAdaptation.C3DWatcher/   kept as they are
```

| Project | Target | References | Holds |
|---|---|---|---|
| SiteAdaptation.Common | netstandard2.0 | Newtonsoft.Json 13.0.1 only | every decision and every computation, tested without Revit |
| CadReader, the worker | net48 and net8.0, an exe | Common, ACadSharp, PdfPig, DocumentFormat.OpenXml | reads the input files and runs the Common steps of buttons 2 and 3, writes the two Excel files |
| SiteAdaptation.AddIn | net48 | Common, the Revit 2024 API, WPF | the ribbon, the commands, the pane, the dialogs, the Revit targets, Extensible Storage |
| SiteAdaptation.Tests | net48 and net8.0 | Common, CadReader, C3DProbe on net48 | every test, the net8.0 leg runs the tests of Common and CadReader on the Linux runner |

Why Common targets netstandard2.0 (CHOSEN): the round asks that the tests pass on the Linux runner too, and a netstandard2.0 library loads in the net48 process of Revit, which holds netstandard 2.0 support in box. A copy of the 31 source files of today's Common built for netstandard2.0 with Newtonsoft.Json 13.0.1 with 0 errors and 0 warnings (RAN, outside the repo). Phase 4a adds a dotnet job on ubuntu-latest that runs the net8.0 leg, the Windows job keeps running every test.

Why Common keeps its name (CHOSEN): the reader, the placer, the test host and both watchers load it by that name, the resolver of the test host names it and Install-TestHost.ps1 checks one hash of it across every installed folder (READ, TestHostApp.cs and Install-TestHost.ps1). A new name would move every reference for no gain.

Why a worker process beside Revit (CHOSEN, Q077): the Road Layout drawing alone took 2,576 MiB of private memory and 20.2 s in CadReader.exe, the baseplan 99 MiB and the footprint drawing 80 MiB (RAN on copies, each source equal by hash before and after). PdfPig asks for System.Memory 4.0.2.0 while Revit 2024 holds 4.0.1.1 with no redirect, so it fails in a Revit-like host with no resolver (RAN, docs/licences.md). In the worker the readers have their own memory, their own binding redirects and their own crash, and Revit loads only Common and the add-in. The worker is CadReader because it is already the net48 exe of the DWG reader with its tests, and the data book names it (python/c3d_probe.json dataBook.cadReader).

What is reused, what moves, what goes:

| From | What | Fate |
|---|---|---|
| Common | Placer, FloorPlacer, IPlacerTarget, IFloorTarget, PlanFile, SiteCoordinates, SiteSpec, SiteFacts, SharedCoordinates, LinkMath, FloorMath, OutlineLoops, PlacementRotation, PlotParts, UnitMath, JsonOut, ErrorLines | reused by buttons 1, 4, 5 and 6 as they are, retargeted to netstandard2.0 in Phase 4a |
| CadReader | Exploder, PlotShapes, PlotLabels, LayerConfig, MTextCleaner, CadExtractor, CadModels | reused by the worker, its old command line stays for the data book |
| SiteAdaptation.Placer | RevitPlacerTarget, RevitFloorTarget, WarningCollector | move to SiteAdaptation.AddIn/Revit in Phase 5a, the old Place from plan button stays until the test host jobs call the add-in, then the project is removed |
| SiteAdaptation.TestHost | SiteSteps (new project, coordinates, levels, floor types, parameters, image) | the Revit side of button 1 moves to SiteAdaptation.AddIn/Revit/SiteTarget in Phase 5b, the test host calls the add-in entry |
| python | plan_from_book.py and the modules it calls | stay, the reference of every parity test |
| rules, config | keyRules of parsons2024Rev6, the override list, config/project.json | read at run time by the worker and the add-in, nothing copied into code |

Nothing is removed while anything uses it. RevitPrototypeReader stays, it reads the answer keys (CLAUDE.md, do not change it unless the round says so).

## 3. The eight buttons

### What every button shares

- One transaction per button. The model changes of a real run happen in one Revit transaction named by the button, such as "Site Adaptation 4 Place villas". A failure preprocessor keeps warnings as issues (WarningCollector of R86) and an error, an exception, a read back that does not match or a commit that does not end committed rolls the whole transaction back. A TaskDialog then shows the reason and the step line of the pane stays as it was. Buttons 2, 3 and 8 change no model element, only the work folder and the settings record, which is written in a transaction of its own after the work (R382, planned).
- Dry run. With "Dry run first, change nothing" ticked, which is the default, a button first runs its dry run: no transaction, no file written but its own log, no link loaded (R86), and shows what it would do, counts and the list per plot, with Cancel and Apply. Apply then runs the real run on the same plan. Button 1 has the buttons Dry run and Apply in its own dialog. Unticked, the button runs for real at once (R382, planned).
- Scope. The pane chooses Whole project (every unit plot of the plot table inside the border), Neighbourhood (the unit plots of one NBH of the location plan, the area key of R256, never an IM letter) or Plot list (official numbers, typed). Every button takes that scope. A plot list that names a plot the plot table does not hold stops the button before any change, the plot named (R113, R366 planned).
- No duplicates. Every element a button places carries an Extensible Storage entity of the schema SiteAdaptation.Placed: plot (the official number), kind (link, generic, yard, ramp, boundaryWall), part (the edge of a wall, else empty), run (the run id) and profile. A second run collects the elements of that schema with an ExtensibleStorageFilter, keys them by plot, kind and part, and for each item of the plan skips it when it is there and equal to the plan, moves or updates it in place when the plan changed it, and places it only when it is missing. An element of the same plot and kind without the entity, placed by hand or by a past job, is never changed: the item is skipped with the reason already placed, not by this tool, and listed, as R87 and R114 skip a plot whose number the parameter holds (R368, R383 planned).
- Stops. A button whose earlier step is not done, or whose input file is not chosen or not on disk, says which one and stops before any change: for example "5 Floors and ramps needs step 4" or "2 Read data needs Curbs, the file is not on disk". It never guesses an input (R382 planned).
- Steps. Each button has a line in the pane: Done, Done with [n] issues, Ready or Needs step [n] (R366 planned). A step is Done when its last real run committed with no issue on the inputs and the scope it has now, by the SHA-256 of every input file and of the rules and override files in the settings record, Done with [n] issues when that run left n issues, Ready when the steps it needs are done, and Needs step [n] names the first step it needs that is not. The steps each button needs (CHOSEN, the order of the mock): 2 needs 1, 3 needs 2, 4 needs 3, 5 needs 4, 6 needs 5, 7 needs 6, 8 needs 3. Button 1 needs only the settings. An input changed since a step makes that step and every step after it need running again.
- The log. Each run writes `run_<button>_<time>.json` to the work folder: the rules applied by id (CLAUDE.md, a line per rule applied), the inputs and their hashes, counts, issues, the library line (the version and file of Common and Newtonsoft.Json loaded) and free memory before and after (R383 planned).

### 1 Set up site

- Inputs: Settings (config/project.json), Rules (rules/roshn-makkah.json, keyRules.site of the level profile: angle 0, site Internal, Level 1 and Level 2 with Level 2 4.0 m over Level 1, metric meters), the project position file that config names (outside git, Q068), Baseplan and Plot numbers for the check points, which the worker reads for this button alone (the extreme plots and the three control points of the sheet), floors.types, floors.levels and floors.plotParameter of config, pastJob.parameters, and the wall types and shared parameter ids that Phase 4 adds to config (READ, config/project.json and the profile).
- Outputs: on an empty project the units, the project position, the survey point and the project base point, Level 1 and Level 2 with a floor plan each, the floor types Generic 150mm, yard, Ramps 0.5%, Ramps 2% and Flat Ramps, the wall types BW and Generic Wall-150mm_ Garden (the most used garden type of the IMC key, READ key_reads.json), and the 11 shared parameters bound as text instance parameters: Plot on floors and walls, and RRE_ZZ_SP_Plot-Number, RRE_ZZ_SP_Prototype, RRE_ZZ_SP_Number-of-Steps, RRE_ZZ_SP_Façade-Color, RRE_ZZ_SP_Façade-Design-Option, RRE_ZZ_SP_Prototype-Foundation-Type, RRE_ZZ_SP_Prototype-HVAC-Schedule-Type, RRE_ZZ_SP_RVT-Name, RRE_ZZ_SP_Prototype-FFL and CASE on Revit links, the ten the keys hold (READ, the RC and RD reads). The settings record of the project.
- Checks: the check points from CAD within 1 mm, the pass limit in config (R211 holds 1 mm today): the three control points the location plan prints (E = and N = texts), the corners of the extreme plots of the baseplan in scope (plots min, plots max), the survey point and the project base point. Each CAD point goes to its internal point by the SiteCoordinates math of R211 and back to shared by the transform Revit holds (ProjectLocation.GetTotalTransform), and the gap is the Diff. After a real run the site facts of R311 are read again and held against keyRules.site.
- Rules: R211, R212, R311, R331 now, R353, R373 and R374 planned.
- Empty project from the default metric template: the template holds Level 1, Level 2 and Generic 150mm, kept by name, the other four floor types are made as copies of it (READ, site_report_i18.json of Round 17: levels kept, Generic 150mm kept, 4 made). The units are set to meters with accuracy 0.01, area square meters, angle degrees, slope degrees, as on the TU keys (READ, the RC read). The project position is set by ProjectLocation.SetProjectPosition, then the survey point and the project base point are moved to their internal points of the position file. Whether Revit 2024 moves both points by MoveElement as the keys hold them is UNKNOWN until Phase 5b, which proves it on one new project by the reader against the IMC read, the 14 fields of R331 within 0.5 mm.
- Empty project from no template, imperial: the same, the length unit set from feet to meters by Units.SetFormatOptions (Document.DisplayUnitSystem stays IMPERIAL, R311 reads both). Levels made by Level.Create with a floor plan, floor types copied from the first FloorType the project holds, wall types from the first basic WallType, as the round asks. Whether a project from no template holds a floor type at all is UNKNOWN, a project with none stops naming the missing system type, never guessing one.
- Existing site file: the badge says Existing site file. The dialog reads what the file holds and changes nothing until Apply. Apply changes only the rows marked to change, and the position only when it differs and the user ticks it. On an existing file the dialog can also save that file's position as the project position file named in config, which is how the file is made for a new project from a past site model (R374 planned).
- Dry run: the Dry run button of the dialog fills the right side and the What the tool will set column without a transaction.
- Scope: the check points take the plots of the scope, the rest is project wide.
- Second run: every item is kept by name or by value and only a difference is set, so a second run changes nothing and says so.
- Stop: a missing settings file, rules file, position file, Baseplan or Plot numbers is named and the button stops.

Under the recommended answer of Q068 button 1 sets the Parsons position: the one project position, survey point, project base point, angle 0, Level 1 and Level 2 the 7 keys share (RAN Phase 1, every gap 0.0 mm against the IMC key on 8 reads), from the position file outside git. Under the other answer it puts the internal origin at the plot centre of the NBH as R211 does, and the rest of the button is the same.

### 2 Read data

- Inputs: Baseplan, Villa blocks, Curbs, Road surface, DLUS and sales, Plot numbers, Prototypes (the seven Input files of the mock), cad_layers.json, analysis and drawings of config, the dataBook.join layers and the locationPlan, rev9 and areaKey settings of python/c3d_probe.json that Phase 4a moves into config/project.json.
- Outputs: the worker writes into the work folder, each file named by the hash of its inputs: faces and labels, blocks, road edges, setback lines, the LandXML surfaces as triangles, the location plan labels with the page to site fit, the DLUS and sales rows, the prototype file per code, and the plot table: one row per baseplan face with every column the build reads (section 4). No model element.
- Checks: each reader's counts against the data book of the same input (section 4 parity), the referenceOnly drawings refused (R300), the free memory floor before each drawing, every file read only and its hash before and after (R1).
- Rules: R5, R20, R22, R40, R42, R68, R166, R177, R179 to R183, R186, R256, R300 now, R347 to R352 and R375 planned.
- Empty project or existing site file: the same, it reads files, not the model.
- Dry run: lists the files it would read, their sizes and hashes, and those it can reuse because their hash has not changed.
- Scope: the files are read whole, the plot table is kept whole, the scope applies from button 3 on.
- Second run: a read whose input hashes are unchanged is reused, never read twice.
- Stop: an input not chosen or not on disk, a drawing of drawings.referenceOnly, a worker that ends with an error or runs past its time limit, each named.

### 3 Check

- Inputs: the plot table of button 2, Rules and Level profile, the override list, keyRules of the profile, the prototype files.
- Outputs: the worker builds `plan_<NBH>.json` in the format of R80 for every NBH of the scope by the C# of section 5, and `checks_<NBH>.json`, the rows of the check table. The check table opens on them. Save overrides writes the override list (below). No model element.
- Checks: the rows of the check table, one per plot and check, of seven kinds of source: an override of the list for the plot (its field gives the check type), a plot and field an open question decides (majority decidedByQuestion), a key the plan lists as missing for the plot, the join status of the key (cut, no generic floor, R326), the prototype file of the key against the file of Typology_Rev9, an entry of the cannot list (R272), and on an existing site file the elements the file holds for the plot. The check types: Plot cut, Floor, Hand, Unit code, Position, Level, then any other type found, such as Parameter.
- Rules: R272, R280, R326, R330, R337, R340 now, R359 to R363 and R376 planned.
- Use: each row has Use set to Past when the build applies an override of the list for it, else Rule. Past on a row with no override writes the past value to the list with source parsons, the model of the key and "byHand": true, so a run of majority.py never drops it (R330). Rule on a row with an override marks the entry "use": "rule" with "byHand": true, the build leaves it and says why. Skip writes the plot to skip of the list with "byHand": true, the build leaves the plot out and the report lists it. Save overrides writes the list by a temporary file and a rename, keeps every other entry cell for cell, then the worker builds the plan again (R376 planned, the list format of R330 grows the fields use and skip).
- Empty project: the past values come from the override list and the key reads, a plot no key holds has an empty Past job. Existing site file: the elements the file holds for each plot, found by plot number as a key read finds them (R322), fill Past job as well.
- Dry run: the plan and the rows are built and shown, Save overrides is greyed out.
- Scope: the check table shows the scope, one NBH per title.
- Second run: the plan and the rows are rebuilt from the same inputs and are equal, the list changes only on Save overrides.
- Stop: step 2 not done or done on other inputs, the override list or the rules file missing, a level profile the rules file does not hold (R277), an override list a build refuses (R280), each named.

### 4 Place villas

- Inputs: `plan_<NBH>.json` of button 3, the Prototypes folder.
- Outputs: one Revit link instance per plot, origin to origin at the insert of the plan, turned by rotationDeg, mirrored, Z at the FFL, the link instance Name of R107 and the parameters of the plan written (RRE_ZZ_SP_Prototype-FFL by button 6, see the conflicts). One link type per prototype file, a usable one of the same file name reused (R84).
- Checks: the coordinates of the plan against the document (R83), each read back within 1 mm, 0.01 degree and the mirror flag (R84), the plot number parameter written or the run rolls back (R85), free memory against the floor before each link type is loaded.
- Rules: R83 to R87, R102, R107 now, R377 planned.
- Empty project: needs step 3, so step 1 set the coordinates and parameters. Existing site file: a plot holding a link of another hand, not tagged, is skipped and listed, never moved.
- Dry run: R86 as it is, the list of what it would place, move or skip, no transaction, no link loaded.
- Scope: the plots of the scope, filtered as R102 filters by prototype file when the test host asks.
- Second run: tagged links equal to the plan are skipped, those the plan moved are moved in place, the count of each is reported.
- Stop: step 3 not done, the prototype file of a code not in the folder, the coordinates of the plan off the document (R83).

### 5 Floors and ramps

- Inputs: `plan_<NBH>.json`, floorSettings of the plan.
- Outputs: per plot the Generic floor on the whole face, the yard floor on the fitted outline at FFL less the yard depth of its zone, none in Zone 4, Zone 1 taken from Zones 2 and 3 pending Q066, and the ramp of R127 with its type and its four corners set by slab shape editing (R114), each with the Plot parameter.
- Checks: R113 to R116 as they are: types, levels and level elevations within 1 mm, every floor read back, top and ramp corners within 1 mm.
- Rules: R111 to R116, R334, R337 now, R378 planned.
- Empty project and existing site file: as button 4, a floor of a plot and kind not tagged is skipped and listed.
- Dry run: R116 as it is.
- Second run: tagged floors equal to the plan are skipped, those the plan changed are changed in place, a floor of the same plot and kind is never placed twice (R114).
- Stop: step 4 not done, a floor type or level the document lacks (R113).

### 6 Levels

- Inputs: `plan_<NBH>.json`, the links and floors placed by buttons 4 and 5.
- Outputs: the level of every tagged link and floor set to the plan: the link Z at the FFL, the height offset of the Generic and yard floors, the ramp corners, and the parameters that read the levels, RRE_ZZ_SP_Number-of-Steps, RRE_ZZ_SP_RVT-Name and RRE_ZZ_SP_Prototype-FFL by keyRules (R340). The level Excel `<NBH>_Levels_<profile>.xlsx` of the mock, written by the worker from the plan and the counts the add-in read back.
- Checks: every level read back within 1 mm, the rows of the Excel held against the plan by a second route (R364 planned), the counts of Revit and Excel reconciled for the report.
- Rules: R67, R278, R339, R340 now, R359, R364 and R379 planned.
- Empty project: needs step 5. Existing site file: untagged elements are listed, not changed.
- Dry run: lists every element it would move and by how much, and the Excel it would write.
- Second run: after buttons 4 and 5 placed the same plan it moves nothing and says 0 moved, then writes the Excel again.
- Stop: step 5 not done, a plot of the scope with no FFL in the plan named with its reason (such as no road level at a front corner, Q073).

### 7 Walls

- Inputs: `plan_<NBH>.json`, the faces, keyRules wallBase, wallLine and frontOpen.
- Outputs: the boundary walls of type BW along the plot edges by the rules the keys follow: the base at the yard level of the higher plot (0.983), the centre on the edge (0.921), no wall along the front, which is the gate (0.988), one wall per edge shared by two plots (READ, keyRules of the profile). The top at the FFL of the higher plot plus 1.7 m of Rev 6 is coded but weak (0.346), every wall top is an issue until Q071. Garden walls have no measured rule, the 28 garden walls of the IMC key are counted only (READ, key_reads.json), so button 7 places none and the report says so.
- Checks: each wall read back within 1 mm on its line, base and top, and the walls held against the key walls by the route of R335 in Phase 6.
- Rules: R335 now, R362 and R380 planned.
- Empty project, existing site file, dry run, second run: as button 5, the wall key being plot pair and edge.
- Stop: step 6 not done, the wall type BW missing (button 1 makes it).

### 8 Report

- Inputs: the plan, the run logs of buttons 1 to 7, the counts the add-in reads from the model.
- Outputs: `<NBH>_Report.xlsx` of the mock per NBH of the scope, written by the worker.
- Checks: the counts of Links, Floors and Levels in Revit against the Excel files, each mismatch an issue.
- Rules: R365 and R381 planned.
- Empty project or existing site file: it reports what the file holds by its tags.
- Dry run: lists the file it would write.
- Second run: writes the file again, the earlier one moved to an earlier folder with its time, never overwritten silently.
- Stop: step 3 not done.

## 4. From each input file to Revit

The add-in never reads an input file itself. The worker reads it read only (R1) and writes JSON into the work folder, the add-in reads that JSON, Revit takes only what the buttons place.

```text
input files -> worker read (button 2) -> plot table -> worker build (button 3) -> plan_<NBH>.json, checks_<NBH>.json
            -> add-in buttons 4 to 7 -> links, floors, walls in Revit -> counts -> worker -> level and report Excel
```

| Input of the mock | File for IMC | Worker reader and library | Python module the C# must equal in Phase 4 | Parity tolerance |
|---|---|---|---|---|
| Baseplan | ROSHN Makkah Baseplan.dwg | CadReader Exploder, PlotShapes, PlotLabels, ACadSharp 3.8.0 MIT | plots.json of CadReader (R5, R22) through yard_rule.face_shapes | the same code moved, every face vertex within 0.000001 m, label exact |
| Villa blocks | Makkah Building Footprint.dwg | CadReader blocks (R68), ACadSharp | blocks.json, project_profile.read_villa_blocks, footprint_check.block_shapes | outline vertex within 0.000001 m, name, mirror and rotation exact, referenceOnly refused (R300) |
| Curbs | Makkah Phase 1_Road Layout.dwg, layers RD-CURB-LINE and ROW | CadReader pieces, ACadSharp | cad_analysis.read_road_edges | every edge within 0.000001 m |
| Setbacks | the footprint drawing, layer MP_Setback Line | CadReader pieces | wiring.setback_shapes and the bestShare of data_book.py | setback from the front within 0.001 m, a check of button 3 only, the build takes the Rev 6 setback of the profile (R127) |
| Road surface | the six LandXML surfaces of siteBuild.roadSurfaces, Top Surface-02 first | Common Read run by the worker, System.Xml | landxml_reader.read_landxml, surface_lookup.Surface, c3d_connect.snap_z | the level at a point within 0.00005 m (half the 4th decimal of R2), the surface name exact |
| DLUS and sales | the Rev 9 DLUS workbook (PH_1-DLUS) and the accommodation workbook (JN Sales Data_Base_Rev09) | CadReader xlsx reader, DocumentFormat.OpenXml 3.5.1 MIT, read row by row | area_key.area_rows, rev9_workbooks.setbacks and widths, corner_front.sales_rows, location_plan.read_table | text exact, numbers exact |
| Plot numbers | 01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf, the location plan | CadReader PDF reader, PdfPig 0.1.16 Apache-2.0 | location_plan.py labels.csv (R179 to R183) | the face of every number exact, page points within 0.01 pt, site points within 0.5 m (the fit differs, conflicts) |
| Prototypes | Prototypes_2024, 13 ARC files | a folder listing by the worker | plan_from_book.folder_files, plan_from_cad.prototype_files | the file of each code exact |

The plot table is the plots.csv of R166 for the add-in: per baseplan face its label, the location plan number, its code and Typology_Rev9, zone and NBH by the area key, the footprint block and its mirror flag, the front frame and the road level at its two front corners. Its reference is the snapshot plots.csv of Round 17 (round_17\snapshot\plots.csv), column by column with the tolerance of the column, on every face of the border (R352 planned). data_book.py also takes blocks from the Civil 3D read where it has one, the worker reads only CadReader outputs, which data_book.py takes for the footprint blocks today (READ, footprintSource of plots.csv).

The data book of NBH 012, the IMC area, as the build reads it (RAN on the snapshot plots.csv and labels.csv): 380 faces, 337 unit plots with Typology_Rev9, 348 with a footprint block, 345 with a road level, 337 client code labels, all in Zone 2. The location plan holds 3,181 pairs, 2,704 of them client codes, 41 codes and 1 number alone (READ, locationPlan.json of the snapshot).

## 5. The plan builder steps Phase 4 moves to C#

The Python reference is plan_from_book.py with the modules it calls, run as the build route of R340 runs it. Phase 4a adds --steps to it, which writes each step's output per plot beside the plan, and python/parity.py, which compares the C# output of the same step field by field with the tolerance its test names, on every unit plot of the data book, 2,704 client code labels of the location plan, 2,591 of them in the 7 NBH and 113 with no sales row (RAN, labels.csv of the snapshot). Real data stays out of git, so the parity runs are local and their counts go to the log, and every step also has tests on small dummy data built in code that run on both runners (R10, R345 planned). A step is done only when parity holds.

| Order | Step | Python source | Rules | Parity test, planned | Tolerance |
|---|---|---|---|---|---|
| 1 | Coordinates from CAD | key_rules.py site facts of the reads (R331), site_spec.py and SiteCoordinates of R211 | R211, R331, R353 | test_r353_parity_position: the position file against the IMC read, and the check points of button 1 against SiteCoordinates | 0.5 mm, angle 0.000001 degree |
| 2 | Plot faces | CadReader plots.json, yard_rule.face_shapes | R5, R22, R348 | test_r348_parity_faces | 0.000001 m per vertex, area 0.000001 m2 |
| 3 | Labels and official numbers | location_plan.run, wiring.point_inside, data_book.build, area_key.area_rows, plan_from_book.book_rows and nbh_plots | R177, R179 to R183, R256, R350, R351, R352 | test_r351_parity_numbers, test_r352_parity_plot_table | number, code, Typology_Rev9, zone and NBH exact per face |
| 4 | Footprint clusters | plan_from_book.footprint_clusters, joined_clusters, numbered_clusters, cad_analysis clusters | R64, R70, R236, R354 | test_r354_parity_clusters | the plot set of every cluster exact |
| 5 | Front edge and corner front | cad_links.front_frame, corner_front.corner_frame, plan_from_book.fronts | R122, R225, R226, R332, R355 | test_r355_parity_fronts | the same edge, corners within 0.000001 m, frontRule exact |
| 6 | Link frame | plan_from_cad.link_values, plan_rules.key_frame_section, plan_from_book.moved_link | R121, R332, R337, R356 | test_r356_parity_link_frame | insert within 0.001 m, rotation within 0.001 degree |
| 7 | Hand rules | majority.side_neighbours, majority.road_side, key_rules.row_pairs, cad_links.cluster_roles, plan_rules.joined_factors and key_hand | R271, R333, R337, R357 | test_r357_parity_hands | mirror flag and the hand rule exact |
| 8 | Ramp and floor outlines | ramp_outline.setback and outline, plan_from_book.generic_floor and ramp_floor, plan_from_cad.flat_floor, plan_rules.key_outlines, key_yard and key_ramp_class | R127, R135, R208, R210, R334, R337, R358 | test_r358_parity_floors | every vertex within 0.001 m, ramp type exact |
| 9 | Road level snap from LandXML | plan_from_book.load_roads and road_level, c3d_connect.snap_z | R40, R42, R157, R339, R349 | test_r349_parity_road_levels | 0.00005 m, surface exact |
| 10 | Levels | plan_rules.entry_of, key_pieces, key_level_rows, steps_by_rule, prototype_ffl, plan_from_book.key_levels and rule_levels | R67, R278, R339, R340, R359 | test_r359_parity_levels | El @ Entry equal at 4 decimals, FFL and SSL at 3, height at 4, steps and Prototype-FFL exact |
| 11 | Walls | none yet, python/plan_walls.py is written first from the rules R335 measured | R335, R362 | test_r362_parity_walls | ends within 0.001 m, base and top within 0.001 m |
| 12 | Overrides | plan_rules.load_overrides and gap_of, override_source.default_sources, the Outcomes of plan_from_book.py | R272, R280, R330, R337, R340, R360 | test_r360_parity_overrides | applied and left per plot and field, and the why, exact |
| 13 | Parameters | plan_rules.key_parameters, the link name of R107 | R85, R107, R337, R361 | test_r361_parity_parameters | text exact |
| 14 | Issues | the missing list and summary of plan_from_book.py, the cannot list, decidedByQuestion, the join statuses, python/check_rows.py written first as the reference of the check table | R272, R326, R363 | test_r363_parity_check_rows | every row, its type and its Use exact |

The Python build of NBH 012 by the parsons rules took 12 s and wrote 336 of 337 plots (RAN, round_addin\design\work\plan_012.json, the 28 inputs equal by hash before and after). Phase 4 PRs, each at most four changes: 4a R345 to R347, 4b R348 to R353, 4c R354 to R357, 4d R358 to R361, 4e R362 to R365, 4f R366 to R369.

## 6. Settings

The user chooses every setting in the pane, nothing is read from or written to the registry (R106).

- What: Settings (the project settings file, config/project.json for this project), Rules (the rules profile, rules/roshn-makkah.json), Level profile (a name of levelProfiles of the rules file, levelProfiles.default when none is chosen, parsons2024Rev6, R277), the seven input files and folders, the override list (config/overrides/roshn-makkah.json, majority.overrideDefaultSources parsons applied, R330), the scope, the dry run box, and the work folder.
- Where: one Extensible Storage entity on the ProjectInformation element of the document, schema SiteAdaptation.Settings with a fixed GUID, vendor SITEADAPT (the VendorId of the manifests), read access public, write access vendor, one string field holding the settings JSON of Common (R367 planned). So the settings are saved with the .rvt, travel with it and come back when it opens. A new project starts with the defaults config names, prefilled from inputRoot and drawings of config (READ, config/project.json).
- The settings JSON: formatVersion, configFile, rulesFile, levelProfile, overrideFile, inputs (baseplan, villaBlocks, curbs, roadSurface, dlus, sales, plotNumbers, prototypes), scope (kind, nbh, plots), dryRunFirst, workFolder, and per button its last run: time, mode, scope, the hash of every input, counts and issues. Paths are kept as chosen, the pane shows the file or folder name.
- When: a pick in the pane writes the entity through an ExternalEvent in a transaction of its own, "Site Adaptation settings". A button writes its run record in its own transaction. A settings JSON of a later formatVersion is refused with both versions named.
- Work folder: by default a folder SiteAdaptation beside the .rvt, for an unsaved project `Documents\SiteAdaptation\<project title>`. It holds the reads, plans, checks, run logs and the two Excel files of each NBH.
- Config gains in Phase 4a a block addin: the position file (Q068, outside git), the wall types, the shared parameter names with the GUIDs the keys hold (read from the key copies in Phase 4b, so a new site file and the keys share them), the floor type thicknesses read from floorTypes of the key reads (R53), minFreeGB (the 2 GB floor of R200), the worker time limit, and the check point pass limit 1 mm. No project value goes in code (CLAUDE.md).

## 7. Every screen and file of the mock

In the order of docs/addin-mock.md, with its labels word for word. The values are those of NBH 012, the IMC area, from the outputs of this round, each with its source. A coordinate shows [coordinate], a level [level]. The fully filled pages are in round_addin\design\mock_filled.md outside the repo.

### 7.1 Ribbon

Ribbon tab "Site Adaptation" with three ribbon panels in this order: Setup (1 Set up site, 2 Read data, 3 Check), Build (4 Place villas, 5 Floors and ramps, 6 Levels, 7 Walls), Output (8 Report). Large buttons, one icon each.

- How: App.OnStartup calls UIControlledApplication.CreateRibbonTab("Site Adaptation"), then CreateRibbonPanel for "Setup", "Build" and "Output" in that order, and one PushButtonData per button with its text as the mock gives it, the full class name of its IExternalCommand, a LargeImage of 32 px and an Image of 16 px, so each is a large button in the normal Revit look (R371 planned). The icons are PNG files of the add-in, one per button, the number of the button on a plain square (CHOSEN, the mock asks one icon each and shows none). An availability class keeps the buttons grey with no project open.
- Values: the labels only.

### 7.2 Dockable pane

Dockable pane "Site Adaptation" on the right, project name at the top right, four sections in this order.

- How: App.OnStartup registers a DockablePaneId with a fixed GUID and an IDockablePaneProvider whose FrameworkElement is a WPF Page and whose InitialState docks it right. The page is a stack of four sections under a top row, each a header TextBlock and a Grid. The project name is a TextBlock aligned right in the top row, Document.Title of the active document, set again on ViewActivated, DocumentOpened and DocumentClosed. A change in the pane reaches the model through an ExternalEvent (R372 planned).
- Project name filled: 01101-PSN-IMC-R17B-TEST-NEW, the last new IMC site file this tool made (READ, kept\round_17).

Project: Settings (config file), Rules (rules file), Level profile

| Label | Control | Filled | Source |
|---|---|---|---|
| Settings | TextBlock with the file name and a Browse button (Microsoft.Win32.OpenFileDialog) | project.json | config/project.json |
| Rules | TextBlock and Browse | roshn-makkah.json | profile of config/project.json |
| Level profile | ComboBox of the names of levelProfiles of the rules file | parsons2024Rev6 | levelProfiles.default, Q050 |

Input files: Baseplan, Villa blocks, Curbs, Road surface, DLUS and sales, Plot numbers, Prototypes. Each shows the chosen file or folder name.

| Label | Control | Filled | Source |
|---|---|---|---|
| Baseplan | TextBlock and Browse | ROSHN Makkah Baseplan.dwg | drawings.baseplan |
| Villa blocks | TextBlock and Browse | Makkah Building Footprint.dwg | drawings.villaBlocks, R300 |
| Curbs | TextBlock and Browse | Makkah Phase 1_Road Layout.dwg | drawings.roadLayout, analysis.roadEdgeLayers RD-CURB-LINE |
| Road surface | TextBlock and a folder Browse | landxml, 6 surfaces of Makkah Phase 1_Road Surface C3D | siteBuild.roadSurfaces, the landxml folder of the snapshot |
| DLUS and sales | TextBlock with two names and Browse | TBD-PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_Rev9.0.xlsx, PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_(Rev9)_Accomodation_Schedule.xlsx | dataBook.data5.areaKey and dataBook.locationPlan.sales of python/c3d_probe.json |
| Plot numbers | TextBlock and Browse | 01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf | dataBook.locationPlan.pdf |
| Prototypes | TextBlock and a folder Browse | Prototypes_2024 | pastJob.upgradedPrototypeFolder |

Scope: radio Whole project, Neighbourhood [code], Plot list. Checkbox "Dry run first, change nothing", on by default.

| Label | Control | Filled | Source |
|---|---|---|---|
| Whole project | RadioButton | not chosen | |
| Neighbourhood | RadioButton and a ComboBox of the NBH codes of the plot table | 012 chosen | area key of R256, the NBH of the IMC key (R326) |
| Plot list | RadioButton and a TextBox of official numbers | not chosen | |
| Dry run first, change nothing | CheckBox | ticked | the default of the mock |

Steps: one line per button 1 to 8 with a status: Done, Done with [n] issues, Ready, or Needs step [n].

| Line | Filled, after buttons 1 to 3 on NBH 012 | Source |
|---|---|---|
| 1 Set up site | Done | 5 of 5 check points at 0.0 mm on the IMC new site file of Round 17 (READ, site_report_i18.json) |
| 2 Read data | Done | the reads of the data book, section 4 |
| 3 Check | Done with 1,247 issues | the rows of the check table, 7.4 |
| 4 Place villas | Ready | step 3 done |
| 5 Floors and ramps | Needs step 4 | |
| 6 Levels | Needs step 5 | |
| 7 Walls | Needs step 6 | |
| 8 Report | Ready | step 3 done |

How: an ItemsControl of eight rows, the text of each from the step state of Common (R366 planned), refreshed after every run and every settings change.

### 7.3 Set up site dialog

Set up site dialog. Title "1 Set up site" and a badge "Empty project found" or "Existing site file".

- How: a modal WPF Window, its owner the Revit main window by WindowInteropHelper, title "1 Set up site". The badge is a Border with a TextBlock, its text from what the dialog found: no survey point moved, no position and none of the types and parameters of config means Empty project found, else Existing site file (R373, R374 planned). The left is a Grid of two parts, the right a DataGrid, the buttons a right aligned StackPanel. The dialog is filled by a dry run of button 1 in Common, the Revit side only reads (SiteTarget).

Left: "What the project has now" (Survey point, Project base point, Levels, Floor types and wall types, Shared parameters, Units) each with its state.

| Row | Empty project found, from the default metric template | Existing site file, the IMD 2024 copy | Source |
|---|---|---|---|
| Survey point | at the internal origin of the template | [coordinate] | Revit BasePoint.GetSurveyPoint, the RD read |
| Project base point | at the internal origin of the template | [coordinate] | BasePoint.GetProjectBasePoint, the RD read |
| Levels | Level 1, Level 2 of the template | Level 1 [level], Level 2 [level] | site_report_i18.json, levels kept, the RD read |
| Floor types and wall types | Generic 150mm, no yard, no ramp, the wall types of the template UNKNOWN until Phase 5b reads them | Flat Ramps, Generic 150mm, Ramps 0.5%, Ramps 2%, yard, BW and 4 garden wall types | site_report_i18.json, the RD read and key_reads.json |
| Shared parameters | none | the 9 RRE parameters and CASE on its links | site_report_i18.json made 10, the RD read |
| Units | metric, the length unit of the template is UNKNOWN until Phase 5b reads it | meters, accuracy 0.01 | the RD read, R311 |

Then "What the tool will set" (Coordinate system, Survey point, Angle to true north, Levels, Floor types, Wall types, Shared parameters, Units) with the values it will set.

| Row | Value it will set | Source |
|---|---|---|
| Coordinate system | shared coordinates of CAD, site Internal, the Parsons project position [coordinate] | keyRules.site, the position file (Q068) |
| Survey point | [coordinate] | the position file, equal on the 7 keys (Phase 1) |
| Angle to true north | 0 | keyRules.site.angleToTrueNorthDeg |
| Levels | Level 1 [level], Level 2 [level], 4.0 m over Level 1 | keyRules.site.levelStepsM, the position file |
| Floor types | Generic 150mm, yard, Ramps 0.5%, Ramps 2%, Flat Ramps | floors.types of config |
| Wall types | BW, Generic Wall-150mm_ Garden | the IMC key read, config block addin (Phase 4a) |
| Shared parameters | Plot on floors and walls, the 9 RRE parameters and CASE on links | floors.plotParameter, pastJob.parameters, RRE_ZZ_SP_Prototype-FFL from the key reads |
| Units | meters, accuracy 0.01 | keyRules.site.units, lengthAccuracy of the TU keys |

Right: table "Check points from CAD" with columns Point, CAD, Revit, Diff, and a line "Pass limit 1 mm. [n] of [n] pass".

| Point | CAD | Revit | Diff |
|---|---|---|---|
| control 1 | [coordinate] | [coordinate] | 0.0 mm |
| control 2 | [coordinate] | [coordinate] | 0.0 mm |
| control 3 | [coordinate] | [coordinate] | 0.0 mm |
| plots min | [coordinate] | [coordinate] | 0.0 mm |
| plots max | [coordinate] | [coordinate] | 0.0 mm |

Pass limit 1 mm. 5 of 5 pass.

- Source: the five check points of the newSite job of the IMC new site file of Round 17 from the default metric template, each 0.0 mm, and its survey point 0.0 mm (READ, site_report_i18.json). That run placed the position of R211, under Q068 button 1 adds the survey point and the project base point as two more rows, held the same way. For the existing IMD copy the survey point and the project base point are 0.0 mm from the IMC key in shared and internal coordinates (RAN Phase 1, p1_compare.json).
- How: a DataGrid with four text columns, CAD and Revit as east, north and elevation in metres, Diff in mm with one decimal. The line is a TextBlock under it.

Below it: "On an existing site file" with a line saying nothing changes until Apply.

- Filled: "On an existing site file", then "Nothing changes until Apply. Apply sets only the rows marked to change." Source: the round prompt.
- How: a TextBlock header and a TextBlock line, shown on both badges.

Buttons Cancel, Dry run, Apply.

- How: Cancel closes with no change. Dry run runs the dry run of button 1 and fills both tables. Apply runs the real run in one transaction and closes when it commits, else shows the reason and stays open (R382 planned).

### 7.4 Check table

Check table. Title "3 Check, NBH [code], [n] plots", a line with the level profile and the override file.

- How: a modeless WPF Window owned by the Revit main window, so the user can work in Revit while it is open, built from `checks_<NBH>.json`. The title is a TextBlock, the line under it a TextBlock (R376 planned).
- Filled: "3 Check, NBH 012, 337 plots", then "Level profile parsons2024Rev6, override file roshn-makkah.json, 12,424 overrides, 1,225 of them on NBH 012". Source: the 337 client code labels of NBH 012 (RAN, labels.csv), the list (READ, config/overrides/roshn-makkah.json, 1,225 entries of key imc, all on plots of the build).

Buttons Zoom to plot, Export to Excel, Save overrides.

- How: three Buttons. Zoom to plot raises an ExternalEvent that calls UIDocument.ShowElements on the tagged elements of the plot, else zooms the active view to the face box by UIView.ZoomAndCenterRectangle. Export to Excel asks the worker to write the rows shown to `Check_<NBH>.xlsx`. Save overrides writes the Use column into the override list as section 3 says and rebuilds the plan.

Filter chips: All, then one per check type (Plot cut, Floor, Hand, Unit code, Position, Level, and any other type found), each with its count, and "[n] plots follow every rule" on the right.

| Chip | Count | Source of the rows |
|---|---|---|
| All | 1,247 | the sum |
| Plot cut | 12 | the cut plots of the IMC join, 2311, 2312, 2375 to 2378, 2387 to 2390, 2544, 2545 (R326) |
| Floor | 38 | 32 overrides (genericFloor 4, yardOutline 14, rampOutline 4, rampType 10), 5 plots with no fitted yard outline (2378, 2387, 2441, 2544, 2545), 2441 with no generic floor in the key |
| Hand | 12 | 11 mirrored overrides, and IMC 2302 decided by Q017 |
| Unit code | 3 | 2301 and 2302, VD4 by Rev 9 while the key links the DP4 file (Q017), 2312 with no unit, it shares the face of 2311 (Q016) |
| Position | 4 | 4 insert overrides |
| Level | 817 | entryM 304, heightM 300, rampFrontM 113, rampFrontFallM 90, steps 5, Prototype-FFL 5 |
| Parameter | 361 | CASE 206, Prototype-Foundation-Type 150, RVT-Name 5 |

"2 plots follow every rule" on the right: 2616 and 2624, the plots of the 337 with no row.

- Source: the plan of NBH 012 by the parsons rules (RAN, round_addin\design\work\plan_012.json), the override list, decidedByQuestion of majority.json of the 3c review and key_join.json, counted by round_addin\design\work\mock_values.py (RAN).
- How: a WrapPanel of ToggleButtons, each with its type and count, All first, then the types in the order of the mock, then any other type found. The count line is a TextBlock aligned right.

Columns: Plot, NBH, Unit, Check, Tool value (rule), Past job, Rule, Use. Use is a dropdown Rule, Past, Skip.

- How: a DataGrid, Plot, NBH, Unit, Check, Tool value (rule), Past job and Rule as read only text columns, Use a DataGridComboBoxColumn with Rule, Past and Skip. Past writes the past value to the override list with source parsons. Skip leaves the plot out and lists it in the report. Selecting a row zooms the Revit view to that plot, through the same ExternalEvent as Zoom to plot.
- Filled, real IMC rows (the levels as [level], the past value of a level row is the rule value plus the gap of its entry, the gap a distance):

| Plot | NBH | Unit | Check | Tool value (rule) | Past job | Rule | Use |
|---|---|---|---|---|---|---|---|
| 2311 | 012 | DP2 | Plot cut | built on the whole baseplan face | the key generic floor holds under 0.9 of one face | R120, R326 | Rule |
| 2441 | 012 | VD2 | Floor | the Generic floor on the whole face | the key holds no generic floor on the plot | R208, R326 | Rule |
| 2301 | 012 | VD4 | Floor | fitted yard outline | the key yard 0.037 m off it | yardOutline, fittedOutlineRefit, a build places the fitted outline | Rule |
| 2291 | 012 | DT1 | Hand | not mirrored | mirrored | hand cluster, clusterSide | Past |
| 2293 | 012 | VD1 | Hand | not mirrored | mirrored | hand villa, blockMirrorRoadSide | Past |
| 2301 | 012 | VD4 | Unit code | RRE-WP-NSP-KEO-ARC-VD4-ZZ-MOD-2400.rvt | RRE-WP-NSP-KEO-ARC-DP4-ZZ-MOD-2400.rvt | Typology_Rev9, Q017 | Rule |
| 2338 | 012 | DP4 | Position | the origin of the plotFrame refit [coordinate] | [coordinate], 0.011 m back along the link X axis and 0.001 m along Y, into the plot | linkOrigin, plotFrameRefit | Past |
| 2290 | 012 | VD1 | Level | El @ Entry [level] | [level], 0.0077 m lower | entry, roadLevel | Past |
| 2289 | 012 | VD4 | Level | FFL [level] | [level], 0.001 m higher | ffl, oneFflPerPieceAtTheHighestEntry | Past |
| 2291 | 012 | DT1 | Parameter | CASE D | F | parameter CASE, typologyMirrored | Past |

### 7.5 Level Excel

Level Excel, file `<NBH>_Levels_<profile>.xlsx`.

- How: the worker writes it with DocumentFormat.OpenXml from the plan and the counts the add-in read back, one workbook per NBH of the scope (R364 planned). Filled: 012_Levels_parsons2024Rev6.xlsx.

A top line with NBH, Zone, road surface and the formulas used.

- Filled: "NBH 012, Zone 2, road surface Top Surface-02 on 672 of 672 front corners, formulas: El @ Entry = the road at the two front corners + the slope of the unit x the ramp depth (R278, R339), FFL = the highest El @ Entry of the piece + the base of its unit, to the mm (R340), SSL = FFL - 0.075 m (levels.ground_ssl), Height = FFL - El @ Entry, Steps = one for the base and one per riser over it, half a riser no step (R340)". Source: the summary of plan_012.json (RAN) and the rules profile (READ).
- How: row 1, one merged cell, bold.

Columns in this order: Plot No, Unit, Cluster, Road at front, El @ Entry, FFL, SSL, Height, Steps, Source (Rule, or Override with the reason).

| Plot No | Unit | Cluster | Road at front | El @ Entry | FFL | SSL | Height | Steps | Source |
|---|---|---|---|---|---|---|---|---|---|
| 2289 | VD4 | none | [level] | [level] | [level] | [level] | 0.2006 | 1 | Override heightM (breaks oneFflPerPieceAtTheHighestEntry), rampFrontM (breaks roadLevel) |
| 2290 | VD1 | 2290-2291 | [level] | [level] | [level] | [level] | 0.1 | 1 | Override entryM (breaks roadLevel), heightM (breaks oneFflPerPieceAtTheHighestEntry) |
| 2292 | DT1 | 2292-2293 | [level] | [level] | [level] | [level] | 0.1003 | 1 | Override heightM (breaks oneFflPerPieceAtTheHighestEntry) |
| 2294 | DT3 | 2294-2295 | [level] | [level] | [level] | [level] | 0.1027 | 1 | Override entryM (breaks roadLevel) |
| 2295 | DT3 | 2294-2295 | [level] | [level] | [level] | [level] | 0.1003 | 1 | Rule |

- Source: plan_012.json levels and levelRule per plot, the cluster by clusterPlots of majority.json (the site clusters a build takes, R271), the reason by the override list (RAN, mock_values.py). Of the 336 rows 24 show Source Rule and 312 an override of El @ Entry, the FFL, the steps or the ramp front, since El @ Entry holds by the rule alone on 157 of 1,753 key plots (Q001).
- How: a header row and one row per written plot, Height with 4 decimals as R2 keeps it, a plot not written listed at the end with its reason.

Sheets: Levels, Clusters, Overrides used, Settings.

- Levels: the table above. Clusters: one row per piece, its plots, unit sequence, split (levelSplit.bySequence) and FFL [level]. Overrides used: plot, field, gap, reason, source parsons, model of the key, from the list. Settings: the profile, the SHA-256 of the rules file and the override list, the road surface order, the pass limit, the run time.

### 7.6 Report Excel

Report Excel, file `<NBH>_Report.xlsx`.

- How: the worker writes it with DocumentFormat.OpenXml from the run logs of buttons 1 to 7 and the counts the add-in read from the model (R365 planned). Filled: 012_Report.xlsx.

A top line with file name, NBH, plot count, profile, run time.

- Filled: "01101-PSN-IMC-R17B-TEST-NEW.rvt, NBH 012, 337 plots, parsons2024Rev6, run time 7.96 min". Source: the IMC new site file of Round 17 and its 13 place runs (READ, place_codes_b.txt), the plot count of the location plan (RAN).

Summary table with columns Step, Done, Skipped, Override, Issues, one row per button 1 to 7.

| Step | Done | Skipped | Override | Issues | Source |
|---|---|---|---|---|---|
| 1 Set up site | 14 | 0 | 0 | 0 | site_report_i18.json: 4 floor types and 10 parameters made, Level 1, Level 2 and Generic 150mm kept as the template held them, 5 check points pass |
| 2 Read data | 337 | 43 | 0 | 0 | the snapshot plots.csv, 380 faces of NBH 012, 337 unit plots, 43 faces with no unit |
| 3 Check | 337 | 0 | 1,225 | 1,247 | 337 plots checked, the 1,225 overrides of the list and the 1,247 rows of 7.4 |
| 4 Place villas | 336 | 1 | 371 | 0 | plan_012.json: 336 links, 2312 not written, overrides applied mirrored 11, insert 4, CASE 206, Prototype-Foundation-Type 150 |
| 5 Floors and ramps | 1,003 | 8 | 123 | 112 | plan_012.json: generic 336, yard 331, ramp 336, the 3 floors of 2312 and 5 yards not written, rampType 10 and rampFrontM 113 applied, genericFloor 4, rampOutline 4, yardOutline 14 and rampFrontFallM 90 left (Q057) |
| 6 Levels | 336 | 1 | 619 | 0 | plan_012.json: entryM 304, heightM 300, steps 5, Prototype-FFL 5 and RVT-Name 5 applied, 2312 with no level |
| 7 Walls | 0 | 0 | 0 | 1 | no wall placed yet, Phase 4e codes the walls, the wall top rule is weak and no garden wall rule is measured (Q071), keyRules wallTop 0.346 |

- How: a table of six columns from row 3, one row per button 1 to 7. The counts of steps 4 to 6 are those of the plan of NBH 012 a real run would place, read from plan_012.json (RAN). No Parsons build has been placed in Revit yet, Phase 6 runs it.

A box "Counts agree across outputs" with columns Item, Revit, Excel for Links, Floors, Levels.

| Item | Revit | Excel |
|---|---|---|
| Links | 336 | 336 |
| Floors | 999 | 999 |
| Levels | 336 | 336 |

- Source: the last build this tool placed of NBH 012, the Round 17 IMC rebuild: 336 links and 999 floors in its read, and 336 links whose Z is the FFL of the plan within 1 mm, against 336 plots, 999 floors and 336 FFL of its plan (RAN, prototypes_imc_r17b.json against plan_imc_r17b.json). A mismatch in this box is an issue of the report.
- How: a bordered range of three columns under the summary.

A box "Rules applied".

- Filled: one line per decision of keyRules the build applied with its coded rule, share and strong or weak: linkRotation frontRule 0.991 strong, linkOrigin plotFrameRefit 0.971 strong, hand villa blockMirrorRoadSide 0.960 strong, hand cluster clusterSide 0.981 strong, hand alone rowPairSideOrBlockMirrorOpenEnd 1.0 strong, rampSlope typology 0.988 strong, rampOutline outlineR127 0.969 strong, generic all 0.991 strong, yard zone 1.0 strong, yardOutline fittedOutlineRefit 0.963 strong, entry roadLevel 0.088 weak, rampFront roadLevel 0.692 weak, levelSplit units 0.702 weak, baseHeight profile 0.844 weak, ffl oneFflPerPieceAtTheHighestEntry 0.749 weak, steps stepsFromHeight 0.988 strong, the parameters (CASE typologyMirrored 0.275 weak, Prototype-Foundation-Type 0.620 weak, Prototype-FFL fflDownToTheCm 0.994 strong, the rest 0.992 to 1.0 strong), with the hand rules of NBH 012: clusterSide 202, blockMirrorRoadSide 108, row pair or block mirror 15, override 11. Source: keyRules of rules/roshn-makkah.json (READ) and the summary of plan_012.json (RAN).
- How: a bordered range, one row per decision with its rule id.

Sheets: Summary, Placed, Skipped, Overrides, Issues, Rules.

- Summary: the top line and the three boxes. Placed: one row per placed element, plot, kind, type, the element id. Skipped: plot, kind, reason. Overrides: the overrides applied and left, by plot and field. Issues: every issue of every button. Rules: every rule id applied with its text from docs/rules.md.

## 8. Install and the unattended runs

Install (R370 planned):

- dotnet/SiteAdaptation.AddIn/Install-SiteAdaptation.ps1, PowerShell 7 (#Requires -Version 7). It refuses while Revit runs, builds unless -NoBuild, copies the add-in, Common and the icons into %APPDATA%\Autodesk\Revit\Addins\2024\SiteAdaptationAddIn and the worker with its libraries and exe.config into its Worker folder, and writes SiteAdaptationAddIn_Revit2024.addin into %APPDATA%\Autodesk\Revit\Addins\2024 with the full path of the DLL.
- The manifest: AddIn Type Application, Name Site Adaptation, FullClassName SiteAdaptation.AddIn.App, an AddInId of its own fixed in the repo, VendorId SITEADAPT and VendorDescription Site Adaptation Tool, as the three manifests of today (READ).
- No admin rights: every path is under %APPDATA% of the user. Never the registry: the script reads no CodeSigning key and writes none (R106), unlike the optional report of Install-TestHost.ps1. Revit asks once whether to load the unsigned add-in, Bader answers it by hand, the watcher answers Load Once in a test window (R108), whose list of the tool's add-ins gains the new manifest.
- One Common: Revit loads one SiteAdaptation.Common.dll for every add-in of the tool, the first it meets, so the script installs Common into every folder of the tool from the same build and checks one SHA-256 across them, as Install-TestHost.ps1 does today (READ).
- Inside Revit the add-in registers an AssemblyResolve handler first thing in OnStartup that serves only the assemblies of its own folder of the same or a higher version, and only to a request of the tool's assemblies or with no requester, never to another add-in (RAN in the host simulation of docs/licences.md). Newtonsoft.Json comes from Revit's own copy by the redirect of Revit.exe.config (READ).

Unattended runs for Phase 6 (R384 planned):

- Run-TestHost.ps1 gains a round spec of the buttons: a job of kind newProject makes `<name>-TEST-NEW.rvt` (R213) from DefaultMetric.rte or from no template with imperial units (Application.NewProjectDocument with UnitSystem.Imperial), then a job of kind buttons names the settings, the scope (one NBH), the buttons 1 to 8, dryRun false, the answers of the dialogs (Apply in Set up site, the Use of every row as built, Apply after each dry run) and the screenshots. The guard of R103 learns both kinds, one window at a time under the lock of R200, the 2 GB floor, the watcher in our own Revit only.
- The test host calls the public entry of each button, SiteAdaptation.AddIn.Buttons.Run, the one the ribbon command calls, so no run logic is copied (as R102 does for the placer). A dialog is a view model first, the ribbon command shows it as a window, the test host answers it from the job, so no window waits for a click.
- Screenshots of our own Revit only: the pane page, the Set up site dialog and the check table are WPF visuals of the add-in, rendered to PNG by RenderTargetBitmap inside the Revit process the run script started. The ribbon tab is taken by PrintWindow of the main window handle of that same process, cropped to the ribbon. Never a screen grab of the desktop, never another process (CHOSEN, Phase 6c proves it or the step becomes a question).
- Each run writes its samples (R310), its log and the hashes of every source before and after (R201, R202).

## 9. Risks

| Risk | Evidence | Handling |
|---|---|---|
| ACadSharp inside Revit | it needs System.Memory 4.0.5.0, Revit 2024 holds 4.0.1.1 with no redirect, it ran beside the host copy in the Revit-like host (RAN) | it runs in the worker, never inside Revit |
| The xlsx writer inside Revit | DocumentFormat.OpenXml 3.5.1 loads beside an older one (RAN), Dynamo for Revit ships 2.12.3.0 (READ) | it runs in the worker, Revit writes no xlsx |
| The PDF reader inside Revit | PdfPig failed on System.Memory 4.0.2.0 with no resolver, ran with one (RAN) | it runs in the worker, whose generated redirects let it run with no resolver (RAN) |
| Newtonsoft.Json | Revit 2024 ships 13.0.1, its config redirects 0.0.0.0 to 13.0.0.0, the host copy served the add-in (READ, RAN) | Common and the add-in pin 13.0.1, the add-in ships no copy, the run log writes the version and file loaded |
| Another add-in hands back a wrong version | the resolver of an add-in that loaded first could answer any request (UNKNOWN in Revit) | the library line of every run log, a button stops naming the assembly when Common or Newtonsoft.Json is not the one expected |
| Memory of a full NBH build | the Road Layout read alone took 2,576 MiB (RAN), the whole NBH 03 image held 10,732 MiB and hung (Q063), each upgrade window of an STR prototype took up to 2,053 MiB (READ, round-addin.md) | the reads and the build run in the worker, which ends after each button, every heavy step checks free memory against the floor first, Phase 5d places NBH 012 with all 13 prototype files in one window and logs the peak by R310 before any larger scope runs, and if one transaction for all codes does not fit, the change of the button to one transaction per code becomes a question |
| The 2 GB floor | R200 checks free memory at the lock, R310 logs it in every sample and stops nothing (READ) | the add-in reads free memory by GlobalMemoryStatusEx before each drawing and each link type load and stops naming it, the floor in config |
| The Zone 1 gap of Q066 | IMA and IMB hold no level, ramp or yard, the yard of Zone 1 is taken from Zones 2 and 3, notMeasured (READ, keyRules.yard) | the build says it on every Zone 1 plot, the check table shows an empty Past job and Source Rule (no key, Q066) |
| Open questions that set values | Q001 El @ Entry, Q012 the split, Q013 CASE, Q017 VD4, Q057 the ramp fall, Q066 Zone 1, Q068 the position, Q070 the entry parameter, Q071 the wall top, Q072 the side road, Q073 no road level, Q074 the steps | each value lives in keyRules or the override list, never in code, and every plot a question decides shows that question in its check row and in Source, so the answer changes data, not code |
| Rough levels on an NBH with no key | by the rule alone El @ Entry holds on 157 of 1,753 key plots, the FFL on 217 (READ, round-addin.md 3c) | the Source column of the level Excel and the check rows say Rule or Override plot by plot (Q001) |
| Settings lost or wrong | a path chosen on another PC | the settings record keeps each path and hash, a missing file stops the button naming it |
| Revit API steps not yet proven | moving the survey point, a project from no template, the WPF pane and windows, the screenshots | each is proven on the smallest case in its Phase 5 PR, an attempt that fails three times becomes a question |

## 10. Planned rule ids, conflicts and questions

Planned rules, taken from R345 to R384, each written into docs/rules.md with its test in the PR that builds it:

| Id | Rule | PR |
|---|---|---|
| R345 | The parity harness: --steps of plan_from_book.py, the step files of the worker, python/parity.py with the tolerance of each step | 4a |
| R346 | Common on netstandard2.0, the worker on net48 and net8.0, Newtonsoft.Json 13.0.1, the test project on net48 and net8.0 with a Linux leg | 4a |
| R347 | The worker request and answer, its time limit, its exit codes and the free memory floor | 4a |
| R348 | DWG reads: faces, labels, blocks, curbs, ROW and setbacks | 4b |
| R349 | LandXML surfaces and the road level snap | 4b |
| R350 | DLUS and sales reads | 4b |
| R351 | The location plan: text lines, pairs, the page to site fit by the control points of the sheet refined on the faces, number to face | 4b |
| R352 | The plot table | 4b |
| R353 | The project position file, the check points and the shared parameter ids of the keys | 4b |
| R354 | Footprint and analysis clusters | 4c |
| R355 | Front edge and corner front | 4c |
| R356 | Link frame | 4c |
| R357 | Hand rules | 4c |
| R358 | Ramp, generic and yard outlines and the ramp type | 4d |
| R359 | Levels | 4d |
| R360 | Overrides | 4d |
| R361 | Link parameters and the link name | 4d |
| R362 | Walls and python/plan_walls.py | 4e |
| R363 | Issues and the check rows, python/check_rows.py | 4e |
| R364 | The level Excel | 4e |
| R365 | The report Excel | 4e |
| R366 | Scope and step state | 4f |
| R367 | The settings JSON kept in the project | 4f |
| R368 | Element tags and the match of the found elements to the plan | 4f |
| R369 | Second method of Phase 4, seeds 14 and 7 | 4f |
| R370 | Install script, manifest, one Common | 5a |
| R371 | The ribbon | 5a |
| R372 | The pane and the settings saved with the project | 5a |
| R373 | Button 1 on an empty project | 5b |
| R374 | Button 1 on an existing site file, its dialog and the position file | 5b |
| R375 | Button 2 Read data | 5c |
| R376 | Button 3 Check, the check table and Save overrides | 5c |
| R377 | Button 4 Place villas, the move in place of a tagged link | 5c |
| R378 | Button 5 Floors and ramps | 5d |
| R379 | Button 6 Levels, RRE_ZZ_SP_Prototype-FFL written | 5d |
| R380 | Button 7 Walls | 5d |
| R381 | Button 8 Report | 5d |
| R382 | One transaction per button, the roll back with the reason, the dry run, the stop | 5a |
| R383 | Element tags in Revit, the library and memory line of each run | 5a |
| R384 | The test host jobs of the buttons and the screenshots | 5d |

Conflicts, none resolved silently:

1. R85 never writes RRE_ZZ_SP_Prototype-FFL and R80 keeps it out of parameters, while keyRules writes it by fflDownToTheCm, 0.994 on 1,791 TU plots, and the plan of 3c holds it (READ). By the majority rule button 6 writes it, and R379 changes R85 and R80 in the same change.
2. R80 lists 9 link parameters and the newSite job of R212 bound Plot and 9, while the keys hold 10 on their links, RRE_ZZ_SP_Prototype-FFL with them (READ, the RC and RD reads). Button 1 binds 11.
3. R181 fits the location plan to the site on the plots of the IMC schedule, a new project has no schedule. R351 fits it by the three control points the sheet prints, refined on the faces that hold one label each, and the parity takes the face of every number exact and the site point within 0.5 m. The IMC fit put the control points 0.265 to 0.305 m from their stated points (READ, locationPlan.json).
4. R211 puts the internal origin at the plot centre of the NBH, the keys share the Parsons position. Under Q068 button 1 sets the Parsons position from the file config names.
5. R212 makes a new shared parameter file, so new GUIDs, the keys hold the client's. Phase 4b reads the GUIDs from the key copies into config so button 1 writes the same ones. Until then the GUIDs of a new file differ from the keys and a schedule over both files does not join them.
6. R87 skips a plot already placed, button 4 moves a link it tagged when the plan moved it. R377 keeps the skip for every untagged link.
7. Place from plan runs links and floors as two steps of one button (R116), the mock makes them buttons 4 and 5, one transaction each, as R116 already gives each step its own.
8. The garden walls have no measured rule (R335 counts them), so button 7 places boundary walls only and lists the garden walls as not placed, while Q071 asks for the wall top rule.
9. shapely, a requirement of the Python checker, bundles GEOS under LGPLv2.1 (Q076).

Questions opened by this design, the full text in steps/questions-closed.md:

- Q076. The Python checker packages: shapely is BSD-3-Clause and its wheels bundle GEOS under LGPLv2.1, numpy, pillow and pypdfium2 hold parts under other permissive licences, none ships with the add-in: keep them for the checker (recommended), or replace shapely.
- Q077. The worker: button 2 and the plan of button 3 run in the add-in's own worker process, CadReader.exe in C#, installed with the add-in and started and ended by the button, since the Road Layout read alone took 2,576 MiB and PdfPig needs a System.Memory Revit 2024 does not hold: accept (recommended), or read inside Revit with the resolver.

The design follows the recommended answers of Q068 (the Parsons position), Q017 (VD4 by Rev 9), Q057 (no ramp front fall) and Q071 (the base, the line and the open front of the boundary walls, the top weak until the architect answers), and the state until an answer of Q066 (the Zone 1 yard of Zones 2 and 3) and Q073 (no level for a plot with a front corner on no road surface, none in NBH 012, the recommended answer takes the road of the covered corner for both). An answer otherwise changes data, not code.
