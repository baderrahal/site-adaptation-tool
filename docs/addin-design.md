# Site Adaptation add-in, the design

Round ADDIN Phase 3b, 2026-10-26, revised the same day after the breaker and the claim-checker read it and again in the last pass on it (PR 49). Written before any add-in code (R343). Phases 4 and 5 build what this file says, or change it first in the same change. The rules live in docs/rules.md, this file names their ids. The approved mock is docs/addin-mock.md and wins over this file where the two differ, the libraries are in docs/licences.md.

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
| SiteAdaptation.Common | netstandard2.0 | Newtonsoft.Json 13.0.1 only at run time, Microsoft.NETCore.Platforms 3.1.0 build only (R346, Q079) | every decision and every computation, tested without Revit |
| CadReader, the worker | net48 and net8.0, an exe | Common, ACadSharp, PdfPig, DocumentFormat.OpenXml | reads the input files and runs the Common steps of buttons 2 and 3, writes the two Excel files |
| SiteAdaptation.AddIn | net48 | Common, the Revit 2024 API, WPF | the ribbon, the commands, the pane, the dialogs, the Revit targets, Extensible Storage |
| SiteAdaptation.Tests | net48 and net8.0 | Common, CadReader, C3DProbe on net48 | every test, the net8.0 leg runs the tests of Common and CadReader on the Linux runner |

Why Common targets netstandard2.0 (CHOSEN): the round asks that the tests pass on the Linux runner too, and a netstandard2.0 library loads in the net48 process of Revit, which holds netstandard 2.0 support in box. A copy of the 31 source files of today's Common built for netstandard2.0 with Newtonsoft.Json 13.0.1 with 0 errors and 0 warnings (RAN, outside the repo). Phase 4a adds a dotnet job on ubuntu-latest that runs the net8.0 leg, the Windows job keeps running every test (R346). The SDK gives netstandard2.0 the NETStandard.Library package, which asks for Microsoft.NETCore.Platforms 1.1.0 under the Microsoft .NET Library licence (READ, dotnet_library_license.txt in the package), so Common pins its first MIT version, 3.1.0, build only, and no package of another licence is restored (R342, Q079). The tests of the Windows tools, the test host guard, the file identity, the window samples and the prototype sets, run on Windows only and the Linux leg skips them with their reason, 52 on the first Linux run (RAN, Phase 4a).

Why Common keeps its name (CHOSEN): the reader, the placer, the test host and both watchers load it by that name, the resolver of the test host names it and Install-TestHost.ps1 checks one hash of it across every installed folder (READ, TestHostApp.cs and Install-TestHost.ps1). A new name would move every reference for no gain.

Why a worker process beside Revit (CHOSEN, Q077): the Road Layout drawing alone took 2,571 MiB of private memory at its peak and 21.2 s in CadReader.exe, the baseplan 100 MiB in 1.4 s and the footprint drawing 77 MiB in 1.3 s (RAN on copies at 67c1fa0, each source equal by hash before and after, round_addin\logs\cadreader_peaks_pr49.txt). The claim-checker's rerun gave 2,612 MiB in 24.8 s, 99 MiB in 1.2 s and 77 MiB in 0.9 s, the first run of 3b 2,576 MiB, neither with its output saved. PdfPig asks for System.Memory 4.0.2.0 while Revit 2024 holds 4.0.1.1 with no redirect, so it fails in a Revit-like host with no resolver (RAN, docs/licences.md). In the worker the readers have their own memory, their own binding redirects and their own crash, and Revit loads only Common and the add-in. The worker is CadReader because it is already the net48 exe of the DWG reader with its tests, and the data book names it (python/c3d_probe.json dataBook.cadReader).

The worker run (R347 planned), each part testable without Revit:

- Request: the add-in writes `request_<run id>.json` into the work folder with formatVersion, runId, document (the full path of the .rvt and the session id the add-in gave that document when it opened, section 6), command (read, build, levelExcel, reportExcel, exportChecks or position), the full path and SHA-256 of every input, the config, rules and override files, the level profile, the scope, the output folder (the dry folder of a dry run, section 3) and the time limit. It starts `CadReader.exe --request <file>` by Process.Start with UseShellExecute false and CreateNoWindow true, its standard error going to `worker_<run id>.log`.
- Memory: before each large read the worker reads the free physical memory (GlobalMemoryStatusEx) and needs at least the measured peak of that input, addin.workerPeakMiB of config keyed by the file name and size of each measured drawing, the largest known for a drawing not measured, plus the floor, siteBuild.minFreeGB, 2.0 GiB (R200). It checks every large input before any read and again right before each read, since Revit may take memory while the worker runs (R347, review of PR 50). The Road Layout value is 2,612 MiB, the highest of three runs: 2,576 MiB in 3b and 2,612 in the claim-checker's rerun, neither output saved (READ, round-addin.md 3b), and 2,571 at 67c1fa0 (RAN, cadreader_peaks_pr49.txt, which holds 2,571, 100 and 77 MiB private for the three drawings). Short of it the worker reads nothing and refuses with both numbers named, such as "free memory 1.42 GiB, the Road Layout read needs 2.55 GiB and the floor 2.00 GiB". In 2a the free memory fell to 0.53 to 1.42 GiB in each read window with a key open (READ, steps/PROGRESS.md open bugs), so beside a large open model button 2 refuses instead of slowing Revit down. A read of unchanged inputs is reused, so the Road Layout is read once per input hash. Since the review of PR 53 (Q083) a command whose whole peak addin.workerPeakMiB.commands names, read 4,100 MiB, the highest whole read of button 2 measured rounded up, is held against that peak and the floor before its first read, and before each read against the rest of it, the peak less what the worker holds, plus the floor (R347).
- Outputs: every output goes to its temporary name with the run id and the SHA-256 of each input it was made from, and the worker moves the outputs onto their names only once every check of the run passed, so a run that fails, is refused or is killed leaves an earlier output as it was, and a reader refuses an output whose inputs changed (R347, review of PR 50).
- Answer: the worker writes `answer_<run id>.json` as its last act, by a temporary name and a rename (section 3, work files): runId, the document of the request, status (done, failed or refused), exitCode, every output with its SHA-256, counts, issues, every file it opened with its SHA-256 before and after, its peak private memory, the free memory before and after, the seconds and the library line. The add-in deletes an answer of the run id before the start, refuses a run id whose log is in the work folder and hands on an answer only when it names the run id, the document and the exit code of the worker, and an exception on the end or the time limit becomes a failed answer with its reason, never an exception in Revit (R347, review of PR 50).
- Exit codes, defined once in Common so both sides read them the same way: 0 done, 1 failed (an exception, its text in the answer), 2 the request refused before any read (an input missing or not chosen, a later formatVersion, a referenceOnly drawing of R300, an input unit that is not metres), 3 refused for memory, 4 an input changed while it was read (its SHA-256 before and after differ, R1). An exit with no answer file is a failure that names the code.
- Time limit: addin.workerTimeLimitS of config per command, 300 s for a read since Phase 4c, about 14 times the longest whole read of button 2 measured in the worker, 20.2 to 21.2 s on net48 and 16.8 to 17.0 s on net8.0 over three runs each (RAN, round_addin\logs\p4c_button2_time.txt), 600 s before (CHOSEN, 28 times the 21.2 s of the Road Layout read). Phase 4d measures it again with the location plan read. In the review of PR 53 the same read took 20.9 to 47.0 s on net48 and 21.5 to 32.6 s on net8.0 as the free memory of the PC went from 4.4 to 6.6 GiB, the build before the review as long in the same state (RAN, p4cfix_time_compare_floor1.txt and _floor2.txt), 300 s still over 6 times the longest. Past the limit the add-in kills the worker by Process.Kill (the worker starts no child process), writes the answer itself with status timedOut, deletes the temporary files of that run id, and the step line stays as it was.
- Revit does not wait: the command starts the worker and returns at once, so the Revit window stays live and the user keeps working. The Exited event of the process raises an ExternalEvent, whose handler runs in the API context of Revit, reads the answer and finds the document of the request among the open documents by its path and session id, whatever document is active. It writes the run record to that document and goes on with the button there, and the check table or the stop message waits until that document is active again. When that document has closed, or closed and opened again, which gives it a new session id, nothing of the answer is applied: the answer stays in the work folder of that document and its run log gets the issue "run [id] ended after its document closed, nothing applied". A second click of a button whose worker runs says so and does nothing. The one wait of the design is the level Excel of button 6, which the worker writes inside the transaction of that button (3.6).
- Tests of R347: the request and the answer read back equal, the memory check as a function of free memory, peak and floor, every exit code to its message, the kill at the time limit with a test worker that only sleeps, the answer routed to the document of its request while another is active and dropped with its issue when that document closed, on net48 and on net8.0.

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

- One transaction per button. The model changes of a real run happen in one Revit transaction named by the button, such as "Site Adaptation 4 Place villas". A failure preprocessor keeps warnings as issues (WarningCollector of R86) and an error, an exception, a read back that does not match or a commit that does not end committed rolls the whole transaction back. A TaskDialog then shows the reason and the step line of the pane stays as it was. Two steps run outside that transaction because the Revit API does not allow them inside one, each before it: the load of an unloaded link type and the reload of one whose file changed, of button 4, on a confirm of its own, and the shared parameter file setting of button 1, put back after the transaction ends. Buttons 2, 3 and 8 change no model element. A file a button writes from what the model holds, `site.json` of button 1 and the level Excel of button 6, is written inside the transaction after the read back, to its temporary name (Work files below), and a failure to write it rolls the transaction back. After the commit it is moved onto its name. A move that fails, such as File.Replace onto a level Excel the user holds open in Excel, leaves the step not Done (Steps below) and the temporary file in place, and the TaskDialog and the run log name the file and say another program holds it open, so the user closes it and runs the button again. The report Excel of button 8 is written the same way, to its temporary name before the record of its run and moved after it (R382, planned).
- Nothing changes until the user confirms, as the round prompt asks of every button. With "Dry run first, change nothing" ticked, the default, a button first runs its dry run: no transaction, no link loaded (R86), nothing written but its own log and its outputs under `dry\<run id>\` of the work folder, which no later button reads, then its summary, the counts and the list per plot, with Cancel and Apply. Apply checks the hashes of the inputs again and runs the real run on the same inputs. Unticked, the button writes no dry folder and shows no list per plot, but it still shows the summary, the counts it will place, move, skip and leave, and asks Apply or Cancel before any write. Button 1 has the buttons Dry run and Apply in its own dialog, and its Apply is the confirm (R382, planned).
- Scope. The pane chooses Whole project (every unit plot of the plot table inside the border), Neighbourhood (the unit plots of one NBH by the area key of R256, never an IM letter) or Plot list. A plot list is official numbers typed in the TextBox, parted by commas, spaces or line breaks: each a whole number, leading zeros dropped as R81 drops them, or a range of two such numbers joined by a hyphen with the lower first, such as 2290-2299, which holds every number between them the plot table holds, a number given twice counted once. Button 1 always works on the whole project, its check points included. The scope applies from button 2 on: button 2 reads every file whole and then holds the scope against the plot table it wrote, buttons 3 to 8 take only the plots of the scope. A token that is no number and no range, a range written high to low, a number or a range the plot table holds no plot of (R113), an empty plot list and a neighbourhood with no unit plot each stop the button before any change with a message naming it, such as "Plot list: 2299-2290 is a range written high to low" or "Neighbourhood 013 holds no unit plot in the plot table" (R366 planned).
- Element tags. Every element a button places carries an Extensible Storage entity of the schema SiteAdaptation_Placed. A schema name and a field name hold letters, digits and underscore only, so SiteAdaptation.Placed with its dot would make SchemaBuilder throw. The fields: plot (the official number), kind (link, generic, yard, ramp, boundaryWall), part (the edge of a wall, else empty), run (the run id), profile, placedId (the UniqueId of the element when the tool placed it) and placed (the values the tool gave it, rounded by R385: for a link its internal origin, rotation and mirror flag, for a floor its outline and top, for a wall its line, base and top). A second run collects the elements of every version of the schema with an ExtensibleStorageFilter and keys them by plot, kind and part (R368, R383 planned).
- A second run, item by item of the plan in the scope, reports and leaves by default:
  - Tagged and equal to the plan: skipped and counted as already placed.
  - Tagged, equal to its own placed values but not to the plan: the plan changed, so the element is moved or updated in place and listed with the change.
  - Tagged and off its own placed values: changed by hand after the run that placed it. It is listed with what differs and left. The summary offers Leave, the default, or Set to the plan for each such element, and only the elements the user sets are changed. The tool never reverts a hand change on its own.
  - Tagged and gone, deleted by hand: placed again only when its plot is in the scope, and listed as placed again. Outside the scope it is only listed.
  - Two elements with one key, when Copy, Mirror or a paste carried the entity to a new element: an issue of the plot. The element whose own UniqueId equals the placedId of its entity is the one the tool placed and stays the tool's, the other is listed as a copy of plot n and never changed, moved or deleted. When no element or more than one holds its own UniqueId, every element of the key is listed and left and the item is skipped.
  - An element of the same plot and kind with no entity, placed by hand or by a past job: never changed, the item is skipped with the reason already placed, not by this tool, and listed, as R87 and R114 skip a plot whose number the parameter holds.
- Schema versions. Each version of a schema has its own fixed GUID and its own name, SiteAdaptation_Placed and then SiteAdaptation_Placed_2, and Common lists every GUID ever used with its field names. A run looks each listed version up by Schema.Lookup and reads an old entity by its field names. When it changes such an element it writes the entity of the latest version and deletes the old one in the same transaction, so an element an earlier add-in tagged is still found and never placed twice (R368, R383 planned).
- Stops. A button whose earlier step is not done, or whose input file is not chosen or not on disk, says which one and stops before any change: for example "5 Floors and ramps needs step 4" or "2 Read data needs Curbs, the file is not on disk". It never guesses an input (R382 planned).
- Steps. Each button has a line in the pane: Done, Done with [n] issues, Ready or Needs step [n] (R366 planned). A step is Done when its last real run committed with no issue, on the scope it has now and on the inputs it read then, held by one SHA-256 over every input of the step: the files of the pane it reads, Settings (config/project.json) and every config file beside it that the worker or the build reads for that step (cad_layers.json, floor_outlines.json, prototype_offsets.json, road_surfaces.json, area_codes.json, plot_numbers.json, second_source.json, the answer of the worker lists the files it opened), the rules file, the override list and the project position file. Every output its run record lists must also be on disk under its name with the SHA-256 the record gives, so a move that failed after the commit leaves the step not Done. A file is hashed by its content. A folder input, Road surface and Prototypes, is hashed by the list of the files the step uses in it, each with its name, size and last write time, not by their content: the 13 prototype files are 1,027 MiB and the 18 LandXML files of the snapshot 226 MiB, the content of the 13 hashed in 1.16 s with the files in the cache against 0.014 s for the list (RAN, round_addin\logs\hash_times_pr49.txt), and the pane works out the step state at every document switch and settings change, over a gigabyte each time from a cold cache or a network drive. A copy over a file keeps the write time of its source, so a button that reads a folder file also hashes its content as it reads it, the run log keeps that hash (R1), and a content hash that differs from the last run's marks the step and every step after it to run again at that run. Done with [n] issues when the last real run left n issues, Ready when the steps it needs are done, and Needs step [n] names the first step it needs that is not. The steps each button needs (CHOSEN, the order of the mock): 2 needs 1, 3 needs 2, 4 needs 3, 5 needs 4, 6 needs 5, 7 needs 6, 8 needs 3. Button 1 needs only the settings. An input changed since a step makes that step and every step after it need running again.
- Work files. Every file the worker or the add-in writes, the reads, plans, checks, run logs, the override list of Save overrides, the two Excel files and the answer of the worker, is first written to `<name>.tmp-<run id>` in the folder of its name and closed, then moved onto its name, by File.Replace when the name is there and File.Move when it is not, the answer last. A move that fails leaves its temporary file and an issue naming the file. A name holding .tmp- is never read, and a run deletes those of earlier run ids it finds. So a partial file of a run that failed, timed out or was killed is never taken as an output, and a read is reused only when the answer of its run lists it with the SHA-256 the file on disk has (R347 planned).
- The log. Each run writes `run_<button>_<time>.json` to the work folder: the rules applied by id (CLAUDE.md, a line per rule applied), the inputs and their hashes, counts, issues, the library line (the version and file of Common and Newtonsoft.Json loaded) and free memory before and after (R383 planned).

### 1 Set up site

- Inputs: Settings (config/project.json), Rules (rules/roshn-makkah.json, keyRules.site of the level profile: angle 0, site Internal, Level 1 and Level 2 with Level 2 4.0 m over Level 1, metric meters), the project position file that config names (outside git, Q068), Baseplan and Plot numbers for the check points, which the worker reads for this button alone (the extreme plots and the three control points of the sheet), floors.types, floors.levels and floors.plotParameter of config, pastJob.parameters, and the wall types and shared parameter ids that Phase 4 adds to config (READ, config/project.json and the profile).
- Outputs: on an empty project the units, the project position, the survey point and the project base point, Level 1 and Level 2 with a floor plan each, the floor types Generic 150mm, yard, Ramps 0.5%, Ramps 2% and Flat Ramps, the wall types BW and Generic Wall-150mm_ Garden (the most used garden type of the IMC key, READ key_reads.json), and the 11 shared parameters bound by the GUIDs of config as text instance parameters: Plot on floors and walls, and RRE_ZZ_SP_Plot-Number, RRE_ZZ_SP_Prototype, RRE_ZZ_SP_Number-of-Steps, RRE_ZZ_SP_Façade-Color, RRE_ZZ_SP_Façade-Design-Option, RRE_ZZ_SP_Prototype-Foundation-Type, RRE_ZZ_SP_Prototype-HVAC-Schedule-Type, RRE_ZZ_SP_RVT-Name, RRE_ZZ_SP_Prototype-FFL and CASE on Revit links, the ten the keys hold (READ, the RC and RD reads). The Settings entity of the project with the record of the run. `site.json` in the work folder, written to its temporary name inside the transaction after the read back and moved onto its name after the commit (section 3, One transaction per button): the site facts of R311 read back from the document, the survey point, the angle and the level elevations, which button 3 hands to the build as plan_from_book.py takes --site (section 5).
- Checks: the check points from CAD within the pass limit, 1 mm in config (R211 holds 1 mm today): the three control points the location plan prints (E = and N = texts), the corners of the extreme plots of the whole baseplan (plots min, plots max), the survey point and the project base point. A CAD point is a shared point, and the CAD column shows it as the drawing gives it. Common takes it to its internal point by the SiteCoordinates math of R211 from the position file. The Revit column shows the shared point Revit gives for that internal point, by another route than Common. After Apply, and on an existing site file whose position is already set, it is the value Revit reports: the inverse of ProjectLocation.GetTotalTransform of the document, which takes a shared point to its internal point (READ, SiteSteps.cs l.65 to 71 and l.202 to 204, R211, and RAN, 5 check points at 0.0 mm in Round 17, site_report_i18.json), applied to that internal point. In a dry run it is the value Apply would give, computed from the position Apply would set: a Revit Transform made in memory from that ProjectPosition, the turn by its angle about the internal origin and then the move by its east, north and elevation, with no change to the document. Never the internal point Revit's own transform gives taken back through the same transform, which holds Revit against itself and always shows 0. The Diff is the distance from the CAD point to the Revit point. After a real run the site facts of R311 are read again and held against keyRules.site and the position file within 0.5 mm (R331).
- Rules: R211, R212, R311, R331 now, R353, R373 and R374 planned.
- Empty project from the default metric template: the template holds Level 1, Level 2 and Generic 150mm, kept by name, the other four floor types are made as copies of it (READ, site_report_i18.json of Round 17: levels kept, Generic 150mm kept, 4 made).
- The order of a real run, all in the transaction "Site Adaptation 1 Set up site" but the shared parameter file setting:
  1. Units: Document.GetUnits gives a copy, the format options of length (meters, accuracy 0.01), area (square meters), angle (degrees) and slope (degrees) are set on that copy by SetFormatOptions, then Document.SetUnits writes the copy into the document, since SetFormatOptions alone changes only the copy (as on the TU keys, READ the RC read).
  2. The position and the survey point: BasePoint.Clipped of the survey point is read, and since Revit 2021.1 it can be set on the survey point (the Revit API as the review of PR 49 gives it), so button 1 sets it unclipped for the move and sets it back to the state it read at the end of this step. SetProjectPosition sets the position of the file, then ElementTransformUtils.MoveElement takes the survey point to its internal point, which unclipped moves the point and not the shared coordinates, then the project base point to its internal point. Then the survey point and the project base point in internal and shared coordinates and the position are read back, each within 0.5 mm of the file (R331), else the transaction rolls back.
  3. Levels: Level 1 and Level 2 kept by name or made by Level.Create with a floor plan each. Level.Elevation is measured from the Elevation Base of the level type, the project base point or the survey point (LEVEL_RELATIVE_BASE_TYPE), while Level.ProjectElevation is measured from the internal origin whatever the base and is read only, so a level is set by Level.Elevation alone. The 7 keys hold both levels on a type with Elevation Base Survey Point and Level.Elevation equal to Level.ProjectElevation within 0.0001 m (RAN on the 7 reads of 2a, round_addin\logs\key_level_bases_pr49.txt). keyRules.site does not name the base today (READ), so Phase 4d writes it there as levelBase, Survey Point, with the position file (R353). Button 1 first sets the Elevation Base of the level type to that base, then sets Level.Elevation of each level, measured from it, to the value of the position file, then reads Level.Elevation and Level.ProjectElevation back, each within 0.5 mm of the file (R331).
  4. Floor types and wall types by name, copies of a type the project holds, as R212 does.
  5. The shared parameters, bound in the same transaction (below).
  6. `site.json` to its temporary name from what steps 2 and 3 read back, then the Settings entity with the record of the run, which lists it.
- Phase 5b measures, on one new project from the default metric template and one from no template, by the reader of R311 before and after each step, what the open list of section 9 names for button 1. It then proves the 14 fields of R331 equal to the IMC read within 0.5 mm. An order that does not give the values of the keys is changed in this design first (R343).
- Shared parameters, one per name: before it binds, button 1 lists every shared parameter the document holds with its name and GUID (the SharedParameterElement of each). A parameter with the name of one of config and another GUID stops the button before the transaction with the list of each such name, the GUID found and the GUID of config, and nothing is bound, so a second parameter of one name is never added. That is the state of a site file made under R212, such as 01101-PSN-IMC-R17B-TEST-NEW, whose 10 parameters came from a new shared parameter file (R212, site_report_i18.json made 10), where binding by the client GUIDs would add parameters of the same names beside them. A parameter of config already bound by its GUID is kept, and a parameter of config with no GUID in config stops button 1 naming it (Phase 4b fills them). Every read and write of a tool parameter, by every button, goes by its GUID, Element.get_Parameter(Guid), never by name, since LookupParameter returns one of two parameters of one name at random. RevitPlacerTarget.cs l.50 and l.166 and RevitFloorTarget.cs l.66 and l.199 look up by name today (READ), so each goes by the GUID when it moves into the add-in (R377, R378).
- The shared parameter file: binding needs Application.SharedParametersFilename to name a file on disk that holds the definitions (READ, SiteSteps.cs l.120 to 148). Before the transaction the button writes `shared_<run id>.txt` into the work folder with the group and the 11 definitions, each with the GUID config names (Phase 4b, conflict 5), keeps the user's own setting, sets the setting to that file and opens it, then starts the transaction. After the commit or the roll back, in a finally block, it puts the user's setting back and deletes the file. Both happen outside the transaction, since the setting belongs to the application and not to the document. The run id in the name means a run that rolled back or crashed and left its file never blocks the next one, where SiteSteps.cs l.42 to 44 refuses a file that is there.
- Empty project from no template, imperial: the same, the length unit set from feet to meters on the copy of Document.GetUnits and written by Document.SetUnits (Document.DisplayUnitSystem stays IMPERIAL, R311 reads both). Levels made by Level.Create with a floor plan, floor types copied from the first FloorType the project holds, wall types from the first basic WallType, as the round asks. Whether a project from no template holds a floor type at all is UNKNOWN, a project with none stops naming the missing system type, never guessing one.
- Empty project or existing site file, the badge: the dialog says Existing site file when the document holds any of what button 1 or a past site job sets and a template never holds: the Settings entity of SiteAdaptation_Settings, a shared parameter bound with the GUID or the name of one of config, a project position other than zero east, north, elevation and angle, or a survey point or a project base point off the internal origin by more than 0.5 mm. Else it says Empty project found. Under the badge it names the signs it found. Types are no sign, since the default metric template holds Generic 150mm, a floor type of config, so a test on types would call every new project an existing site file (READ, site_report_i18.json, Generic 150mm kept). Levels by name are no sign either, the template holds Level 1 and Level 2. Phase 5b tests the badge on copies, each by the signs it holds: the default metric template and no template, Empty project found with no sign. The IMD 2024 copy, Existing site file by a project position other than zero, the survey point and the project base point off the internal origin (RAN, the RD read of 2a, booleans only) and the parameters of config on its links, by name in that read and by GUID once 4b reads the GUIDs. It never holds the Settings entity, which no answer key holds. The new project of 5b after one real run of button 1, opened again: the Settings entity and the signs that run set. A copy of 01101-PSN-IMC-R17B-TEST-NEW: the parameters of config by name with other GUIDs, which also stop Apply (Shared parameters above) (R373, R374 planned).
- Existing site file: the dialog reads what the file holds and changes nothing until Apply. Apply sets every row of What the tool will set that differs from What the project has now, the rows the dialog marks as differing, and leaves the others as they are. The dialog has no tick per row and no action that saves a position, since the mock dialog has neither. The project position file of a new project is written by the worker from the read of a past site model (command position, R353), never by this dialog (R374 planned).
- Dry run: the Dry run button of the dialog fills the right side and the What the tool will set column with no transaction, so the document is not changed. Document.IsModified stays as it was, the Settings entity and the shared parameter file are not written, and a dry run of button 1 on the IMD copy changes nothing in the file (Phase 6 step 3 checks it by IsModified and by the SHA-256 of the copy before and after, closed without a save).
- Scope: button 1 always works on the whole project, its check points are the extreme plots of the whole baseplan whatever the scope of the pane.
- Second run: every item is kept by name or by value and only a difference is set, so a second run changes nothing and says so.
- Stop: a missing settings file, rules file, position file, Baseplan or Plot numbers, a shared parameter of the name of one of config with another GUID, or a parameter of config with no GUID in config, is named and the button stops.

Under the recommended answer of Q068 button 1 sets the Parsons position: the one project position, survey point, project base point, angle 0, Level 1 and Level 2 the 7 keys share (RAN Phase 1, every gap 0.0 mm against the IMC key on 8 reads), from the position file outside git. Under the other answer it puts the internal origin at the plot centre of the NBH as R211 does, and the rest of the button is the same.

### 2 Read data

- Inputs: Baseplan, Villa blocks, Curbs, Road surface, DLUS and sales, Plot numbers, Prototypes (the seven Input files of the mock), cad_layers.json, analysis and drawings of config, the dataBook.join layers, the rev9 and areaKey sheets and the sales sheet of locationPlan, which Phase 4c moved from python/c3d_probe.json into dataBook of config/project.json (R350), the rest of locationPlan for Phase 4d, and the schedule of the location plan fit with its plot number shift (section 5, The location plan fit).
- Outputs: the worker writes into the work folder, each file named by the hash of its inputs and written by a temporary name and a rename (work files above): faces and labels, blocks, road edges, setback lines, the LandXML surfaces as triangles, the location plan labels with the page to site fit of the schedule (R181) and the list of the faces the control point fit numbers otherwise (R387), the DLUS and sales rows, the prototype file per code, and the plot table: one row per baseplan face with every column the build reads (section 4). No model element.
- Checks: each reader's counts against the data book of the same input (section 4 parity), the referenceOnly drawings refused (R300), the free memory of R347 before each large read, every file read only and its hash before and after (R1). The unit of every input: a LandXML file with no Units element, a Units element with no linearUnit, or a linearUnit other than meter, such as foot or USSurveyFoot, is refused, never read with its unit as None as landxml_reader.py reads a missing one today (READ, its linearUnit stays None), and a drawing whose header InsUnits is not Meters is refused, each with the file and the unit it holds named, never converted (R348, R349 planned). All 42 LandXML reports under Documents\SiteAdaptation give meter (READ, the linearUnit of every report found by a search there) and the three drawings give Meters (RAN, cadreader_peaks_pr49.txt). A conversion would need a named rule with its factor in config, and none is written until a file in another unit is met. The Python reference refuses the same (R388, Phase 4b, section 5), a CadReader output by the units of the layers.json of its read, so a copy of plots.json alone, as the Round 17 snapshot holds, is refused and the build reads the outputs in their read folders, equal by SHA-256.
- Inputs are known by their full path and SHA-256, never by their name alone: 01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf names two different files on this machine, 3,056,293 bytes in Roshn Makkah - Master Plan Rev.09, the one dataBook.locationPlan.pdf reads, and 2,395,117 bytes in 01_Output\1.0 General\PDF, SHA-256 starting 7061ac0d and 24ffcbac (READ, a listing and sha256sum). The pane shows which one is chosen (7.2).
- Rules: R5, R20, R22, R40, R42, R68, R166, R177, R179 to R183, R186, R256, R300 now, R347 to R352, R375, R387 and R388 planned.
- Empty project or existing site file: the same, it reads files, not the model.
- Dry run: lists the files it would read, their sizes and hashes, and those it can reuse because their hash has not changed.
- Scope: the files are read whole and the plot table is kept whole, then the scope is held against it, so an empty plot list, a number it does not hold or a neighbourhood with no unit plot stops here, and from button 3 on only the plots of the scope are built.
- Second run: a read whose input hashes are unchanged is reused, never read twice.
- Stop: an input not chosen or not on disk, the fit schedule not named in config or not on disk, a drawing of drawings.referenceOnly, a worker that ends with an error or runs past its time limit, each named.

### 3 Check

- Inputs: the plot table of button 2, Rules and Level profile, the override list, keyRules of the profile, the prototype files, `site.json` of button 1 (what plan_from_book.py takes as --site), and floor_outlines.json and prototype_offsets.json beside the config file (section 6).
- Outputs: the worker builds `plan_<NBH>.json` in the format of R80 for every NBH of the scope by the C# of section 5, and `checks_<NBH>.json`, the rows of the check table. The check table opens on them. Save overrides writes the override list (below). No model element.
- Checks: the rows of the check table, one per plot and check, of seven kinds of source: an override of the list for the plot (its field gives the check type), a plot and field an open question decides (majority decidedByQuestion), a key the plan lists as missing for the plot, the join status of the key (cut, no generic floor, R326), the prototype file of the key against the file of Typology_Rev9, an entry of the cannot list (R272), and on an existing site file the elements the file holds for the plot. The check types: Plot cut, Floor, Hand, Unit code, Position, Level, then any other type found, such as Parameter.
- Rules: R272, R280, R326, R330, R337, R340 now, R359 to R363, R376 and R386 planned.
- Use: each row opens with Use set to Past when the build applies an override of the list for it, else Rule, and the table keeps that opening value of every row. Past writes in the form the list holds and the build reads, a gap to the rule as plan_rules.gap_of reads it, never a level or a coordinate. What Past writes, by check and field:

| Check | Field | Past writes | Offered |
|---|---|---|---|
| Level, El @ Entry | entryM | {"gapToRuleM": the past value less the rule value}, 4 decimals by R385 | yes |
| Level, FFL | heightM | {"gapToRuleM": the past FFL less the rule FFL}, 3 decimals | yes |
| Level, ramp front | rampFrontM | {"gapToRuleM": the past front less the rule front}, 4 decimals | yes |
| Level, steps | steps | the step count as a whole number | yes |
| Level, ramp front fall | rampFrontFallM | nothing, a build never applies a fall (Q057) | no |
| Position, link origin | insert | {"gapToRuleM": [along X, along Y]} in the link axes, X along the front and Y into the plot, as plan_from_book.moved_link reads it, 3 decimals | yes |
| Position, rotation | rotationDeg | {"gapToRuleDeg": the past rotation less the rule rotation} | yes |
| Hand | mirrored | true or false | yes |
| Floor, ramp type | rampType | the slope class, flat, 0.5 or 2 | yes |
| Floor, outlines | genericFloor, yardOutline, rampOutline | nothing, a build places the outline of its rule and leaves these (LEFT of plan_from_book.py) | no |
| Parameter | `parameter <name>` | the text of the past value, for RRE_ZZ_SP_Prototype-FFL {"gapToRuleM": the past less the rule} | yes |
| Unit code | none | nothing, no field of the list gives a prototype file, the code is Typology_Rev9 (Q017) and never a key's (Q056) | no |
| Plot cut | none | nothing, a build always takes the whole baseplan face (R326) | no |

- Where Past is not offered it is grey in the dropdown with its reason as a tooltip, and Rule and Skip stay. Past on a row with no override writes the entry with source parsons, the key and the model of the answer key that holds the plot, and "byHand": true, so a run of majority.py never drops it (R330). A plot no answer key holds has no key and no model to write, so Past is grey on its rows with the reason "no answer key holds plot [n]". On an existing site file Past job shows what the file holds for the plot, and Past writes it only when the file is one of the answer keys of majority.groups, as its key and model, since the list takes no other source (override_source.check_entries). For any other site file Past is grey with the reason "the site file is no answer key, the list takes no source for it". Rule on a row with an override marks the entry "use": "rule" with "byHand": true, the build leaves it and says why. Skip writes the plot to a list skip of the override file, {"plot", "reason", "byHand": true}, the build leaves the plot out and the report lists it. Save overrides writes only the rows whose Use the user changed in this session of the table, each with "byHand": true, and keeps every other entry cell for cell with no mark added. A row changed and set back to its opening value is no change. So the 112 rows of NBH 012 that open on Rule over an entry the build leaves (genericFloor 4, rampOutline 4, yardOutline 14, rampFrontFallM 90, 7.6) stay as they are and a majority run may still replace them, where a byHand mark on each would keep them for ever, since override_source.merge keeps every entry marked byHand or holding a use one by one, cell for cell, and leaves out the entry of the run on its plot, key and field (R330, R386, Round ADDIN 4b review, where it refused a run that dropped one). The list is written by a temporary file and a rename, then the worker builds the plan again in a real run of button 3 (R376 planned, the list format of R330 grows the field use and the list skip, R386 planned).
- The Python side of Use (R386, Phase 4b, section 5): before 4b majority.write_overrides wrote only note, overrides and cannot, so a skip list would have been lost at the next majority run, and plan_rules.load_overrides ignored use, so an entry marked use rule would still have been applied (READ, majority.py and plan_rules.py of 4a). Since 4b a use needs "byHand": true, and a majority run keeps every hand edit one by one and reads its use as the build does, refusing a use of another spelling or value or with no byHand (review of PR 51), and R386 makes write_overrides keep skip whole, override_source keep use with its entry and refuse any value but rule or past, load_overrides leave an entry marked use rule with that reason and give back the skip plots, and plan_from_book.py leave those plots out and list them, so step 12 of section 5 compares the same applied, left and skipped on both sides.
- Empty project: the past values come from the override list and the key reads, a plot no key holds has an empty Past job. Existing site file: the elements the file holds for each plot, found by plot number as a key read finds them (R322), fill Past job as well.
- Dry run: the plan and the rows are built into `dry\<run id>\plan_<NBH>.json` and `checks_<NBH>.json` and shown, and Save overrides is greyed out. Button 4 never reads a dry folder: it reads only the plan of the last real run of 3, which the run record names with its SHA-256.
- Scope: the check table shows the scope, one NBH per title, and for Whole project one table with NBH all in its title (7.4).
- Second run: the plan and the rows are rebuilt from the same inputs and are equal, the list changes only on Save overrides, which rebuilds the plan only in a real run of 3.
- Stop: step 2 not done or done on other inputs, the override list or the rules file missing, a level profile the rules file does not hold (R277), an override list a build refuses (R280), each named.

### 4 Place villas

- Inputs: `plan_<NBH>.json` of the last real run of button 3, held against the SHA-256 its run record names, never a plan of a dry folder, and the Prototypes folder.
- Outputs: one Revit link instance per plot, origin to origin at the insert of the plan, turned by rotationDeg, mirrored, Z at the FFL, the link instance Name of R107 and the parameters of the plan written (RRE_ZZ_SP_Prototype-FFL by button 6, see the conflicts). One link type per prototype file.
- Link types: an existing link type is matched by the full path of its file, the absolute path of GetExternalFileReference compared with the path of the plan without case after Path.GetFullPath, never by the file name alone, as TypesByFile of Placer.cs l.296 to 331 matches today. A type of the same file name at another path is not reused, a new type is made for the path of the plan, and both are listed.
- Link types to load, a step of its own before the transaction: a type at the path of the plan that is not loaded is loaded again by RevitLinkType.Load, which may not run while a transaction is open and clears the Undo list, while RevitLinkType.Create refuses a path that is linked already (the Revit 2024 API as the review of PR 49 gives it, proven on one link in Phase 5c). A loaded type whose file changed on disk since the last real run of 4 is reloaded the same way by RevitLinkType.Reload, since a second run skips a tagged link equal to the plan and would leave the old file shown: button 4 hashes the content of each prototype file of the plan at every run (1.16 s for the 13 with the files in the cache, RAN, hash_times_pr49.txt), and a file whose hash differs from the one the record of the last real run of 4 lists, by the hash list of its step (section 3, Steps), marks its type for the reload. So the dry run lists the types it would load and those it would reload with the file that changed, and Apply first shows a TaskDialog that names each one with load or reload and says Undo is cleared, with Load and Cancel. Load loads and reloads them one by one outside any transaction, then the transaction of the button places the links. A type that does not load stops the button before its transaction with the file and the load result named. The mock does not show this dialog (7.7).
- Checks: the coordinates of the plan against the document (R83), each read back within 1 mm, 0.01 degree and the mirror flag (R84), the plot number parameter written or the run rolls back (R85), free memory against the floor before each link type is loaded.
- Rules: R83 to R87, R102, R107 now, R377 planned.
- Empty project: needs step 3, so step 1 set the coordinates and parameters. Existing site file: a plot holding a link of another hand, not tagged, is skipped and listed, never moved.
- Dry run: R86 as it is, the list of what it would place, move or skip, no transaction, no link loaded.
- Scope: the plots of the scope, filtered as R102 filters by prototype file when the test host asks.
- Second run: by the rules of a second run above, after the reload of every type whose file changed. Tagged links equal to the plan are skipped, those the plan moved are moved in place, those moved by hand are listed and left unless the user sets them to the plan, a deleted one is placed again only when its plot is in the scope, the count of each is reported.
- Stop: step 3 not done or only dry run, the prototype file of a code not in the folder, the coordinates of the plan off the document (R83), a link type that does not load.

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
- Outputs: the level of every tagged link and floor set to the plan: the link Z at the FFL, the height offset of the Generic and yard floors, the ramp corners, and the parameters that read the levels, RRE_ZZ_SP_Number-of-Steps, RRE_ZZ_SP_RVT-Name and RRE_ZZ_SP_Prototype-FFL by keyRules (R340). The level Excel `<NBH>_Levels_<profile>.xlsx` of the mock, from the plan and the values the add-in read back from the model, written inside the transaction: after the read back the add-in writes those values to `levels_<run id>.json` and waits for the worker command levelExcel, the one wait of the design, within addin.workerTimeLimitS.levelExcel (60 s, CHOSEN, Phase 4g measures it), and the worker writes the Excel to its temporary name. A worker that fails or runs past the limit rolls the transaction back. After the commit the Excel is moved onto its name, and a move that fails, as onto an Excel the user holds open, leaves step 6 not Done with the issue naming the file (section 3, One transaction per button). The run log of the run lists every link and floor it set with its UniqueId and the level read back, and the Settings sheet of the Excel carries the run id.
- Checks: every level read back within 1 mm, the rows of the Excel held against the plan by a second route (R364 planned), the plots this run set and read back and the rows of the Levels sheet of its Excel counted for the box Counts agree across outputs of the report (button 8), both by the run id.
- Rules: R67, R278, R339, R340 now, R359, R364 and R379 planned.
- Empty project: needs step 5. Existing site file: untagged elements are listed, not changed.
- Dry run: lists every element it would move and by how much, and the Excel it would write.
- Second run: after buttons 4 and 5 placed the same plan it moves nothing and says 0 moved, then writes the Excel again.
- Stop: step 5 not done, a plot of the scope with no FFL in the plan named with its reason (such as no road level at a front corner, which R344 lists with the surfaces tried and why, Q082).

### 7 Walls

- Inputs: `plan_<NBH>.json`, the faces, keyRules wallBase, wallLine and frontOpen.
- Outputs: the boundary walls of type BW along the plot edges by the rules the keys follow: the base at the yard level of the higher plot (0.983), the centre on the edge (0.921), no wall along the front, which is the gate (0.988), one wall per edge shared by two plots (READ, keyRules of the profile). The top at the FFL of the higher plot plus 1.7 m of Rev 6 is coded but weak (0.346), every wall top is an issue until Q071. Garden walls have no measured rule, the 28 garden walls of the IMC key are counted only (READ, key_reads.json), so button 7 places none and the report says so.
- Checks: each wall read back within 1 mm on its line, base and top, and the walls held against the key walls by the route of R335 in Phase 6.
- Rules: R335 now, R362 and R380 planned.
- Empty project, existing site file, dry run, second run: as button 5, the wall key being plot pair and edge.
- Stop: step 6 not done, the wall type BW missing (button 1 makes it).

### 8 Report

- Inputs: the plan, the run logs of buttons 1 to 7, the counts the add-in reads from the model.
- Outputs: `<NBH>_Report.xlsx` of the mock per NBH of the scope, written by the worker to its temporary name, then the record of the run is written, then the report is moved onto its name, and a move that fails leaves step 8 not Done with the issue naming the file (section 3, One transaction per button).
- Checks: the box Counts agree across outputs compares counts made apart, so it can disagree. Revit is what the add-in reads from the model at report time: the tagged links and floors of the scope by kind, and for Levels the plots whose link and floors the last run of button 6 that committed set and read back, found by the UniqueIds its run log lists and still in the model at the levels that run read back, within 1 mm. A link Z button 4 put at the FFL counts for Levels only once a run of 6 set it, so a run of 6 that rolled back after button 4 changed a plot, or no run of 6, shows as a Levels count short of the plan. Excel is the rows of files written before: the rows of the Placed sheet of kind link and of the floor kinds, written from the run logs of buttons 4 and 5 with the element ids they placed, and the rows of the Levels sheet of the level Excel whose Settings sheet carries the run id of that run of 6. A level Excel on disk of another run id, or none, is an issue naming both run ids, and its rows are not counted. The plan count is held against both apart. Each mismatch of any two is an issue naming the item and the three counts, so a link deleted by hand after button 4, a level moved by hand after button 6, a run of 6 that failed or a level Excel of another run shows. The report is written after the read, by the worker, and every number of one report comes from one plan and one read.
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
| Baseplan | ROSHN Makkah Baseplan.dwg | CadReader Exploder, PlotShapes, PlotLabels, ACadSharp 3.8.0 MIT | plots.json of CadReader (R5, R22) through yard_rule.face_shapes | the same code moved, every face vertex by the points tolerance of section 5, label exact, InsUnits Meters |
| Villa blocks | Makkah Building Footprint.dwg | CadReader blocks (R68), ACadSharp | blocks.json, project_profile.read_villa_blocks, footprint_check.block_shapes | outline vertex by the points tolerance, rotation by the angles tolerance, name and mirror exact, referenceOnly refused (R300) |
| Villa blocks, the setbacks | the same footprint drawing, its layer MP_Setback Line, so the mock input Villa blocks carries the setbacks and they need no row of their own | CadReader pieces | wiring.setback_shapes and the bestShare of data_book.py | setback from the front by the points tolerance, a check of button 3 only, the build takes the Rev 6 setback of the profile (R127) |
| Curbs | Makkah Phase 1_Road Layout.dwg, layers RD-CURB-LINE and ROW | CadReader pieces, ACadSharp | cad_analysis.read_road_edges | every edge by the points tolerance |
| Road surface | the six LandXML surfaces of siteBuild.roadSurfaces, Top Surface-02 first | Common Read run by the worker, System.Xml | landxml_reader.read_landxml, surface_lookup.Surface, c3d_connect.snap_z and road_level, the one rule of R344 the build and the data book take | the level at a point by the levels tolerance, the surface name exact, linearUnit meter |
| DLUS and sales | the Rev 9 DLUS workbook (PH_1-DLUS) and the accommodation workbook (JN Sales Data_Base_Rev09) | CadReader xlsx reader, DocumentFormat.OpenXml 3.5.1 MIT, read row by row | area_key.area_rows, rev9_workbooks.setbacks and widths, corner_front.sales_rows, location_plan.read_table | text exact, numbers exact |
| Plot numbers | 01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf of Roshn Makkah - Master Plan Rev.09, 3,056,293 bytes, the location plan | CadReader PDF reader, PdfPig 0.1.16 Apache-2.0 | location_plan.py labels.csv (R179 to R183) with the schedule fit of R181, the control point fit of R387 as its check | the face of every number exact, page points within 0.01 pt, site points by the points tolerance, both sides numbering by the same schedule fit (section 5) |
| Prototypes | Prototypes_2024, 13 ARC files | a folder listing by the worker | plan_from_book.folder_files, plan_from_cad.prototype_files | the file of each code exact |

How the tolerances read: section 5, Parity tolerances, names one per kind of field, for the value before R385 rounds it and for the rounded output.

The plot table is the plots.csv of R166 for the add-in: per baseplan face its label, the location plan number, its code and Typology_Rev9, zone and NBH by the area key, the footprint block and its mirror flag, the front frame and the road level at its two front corners. Its reference is the snapshot plots.csv of Round 17 (round_17\snapshot\plots.csv), column by column with the tolerance of the column, on every face of the border (R352 planned). data_book.py also takes blocks from the Civil 3D read where it has one, the worker reads only CadReader outputs, which data_book.py takes for the footprint blocks today (READ, footprintSource of plots.csv).

The data book of NBH 012, the IMC area, as the build reads it (RAN on the snapshot plots.csv and labels.csv): 380 faces, 337 unit plots with Typology_Rev9, 348 with a footprint block, 345 with a road level, 337 client code labels, all in Zone 2. The location plan holds 3,181 pairs, 2,704 of them client codes, 41 codes and 1 number alone (READ, locationPlan.json of the snapshot).

## 5. The plan builder steps Phase 4 moves to C#

The Python reference is plan_from_book.py with the modules it calls, run as the build route of R340 runs it. Phase 4a adds --steps to it, which writes each step's output per plot beside the plan, and python/parity.py, which compares the C# output of the same step field by field with the tolerance its test names, on every unit plot of the data book, 2,704 client code labels of the location plan, 2,591 of them in the 7 NBH and 113 with no sales row (RAN, labels.csv of the snapshot). Real data stays out of git, so the parity runs are local and their counts go to the log, and every step also has tests on small dummy data built in code that run on both runners (R10, R345). A step is done only when parity holds. Phase 4a proves the harness on the step faces, the baseplan face of each plot from the plots.json of the baseplan reader as the build takes it, before the DWG read of step 2 moves to the worker in 4c (R348): plan_from_book.py all --steps --steps-only against the worker build with steps faces, the cover all being every client code label, a step file of each side, step_<step>_python.json and step_<step>_csharp.json, holding its side, its scope, the SHA-256 of the inputs it was made from and per plot the raw values and the values as that side writes them, python/parity.py writing parity_<step>.json outside the repo. parity.py refuses one file twice, the same side twice and a step file whose inputs changed, compares every field of either side, a field with no tolerance in config or a configured field missing failing, types and shapes exact, and decides a one unit case on the decimals of the written texts (R345, review of PR 50). On the 2,704 unit plots, 2,557 of them with a face, 12,932 field values held on net48 and net8.0 with 0 one unit cases and 0 failures (RAN, round_addin\logs\p4a_parity_faces.txt).

The inputs of the reference are the same files by path on both sides. plan_from_book.py takes --site, the site facts the newSite job read back (testhost_<code>_site.json). In the add-in that file is `site.json`, which button 1 writes into the work folder inside its transaction and moves onto its name after its commit (3.1), and the worker takes it from there for button 3. --floors and --offsets default to config/floor_outlines.json and config/prototype_offsets.json of the repo. The add-in finds the two beside the config file the pane names, by the file names of the addin block of config (section 6), so the install ships no copy and no project value is in code. A parity run hands both sides the same site file, the same two config files and the same snapshot by path.

### One rounding rule (R385 planned)

Python round() rounds the binary value of a float, so 12.3455 to 3 decimals gives 12.345, while C# Math.Round with MidpointRounding.AwayFromZero, which JsonOut.Round3 uses (READ, JsonOut.cs l.18), gives 12.346. The same holds for 15.1235 to 15.123 against 15.124, 1.0005 to 1.0 against 1.001, 2.0465 to 2.046 against 2.047 and 7.0625 to 7.062 against 7.063 (RAN on Python 3.12.10 and on net48 and net8.0, round_addin\logs\rounding_probe_pr49.txt). A parity that allowed 0.001 would pass such a 1 mm flip. R385 gives both languages one rule, on decimal values and not on binary floats:

1. The value is written as its text of 15 significant digits, Python format(value, ".15g") and C# value.ToString("G15", CultureInfo.InvariantCulture). A double holds 15 significant digits exactly, so the noise in the last bits of one value, such as 0.30000000000000004, or 150.38 less 150.0, drops out here. It cannot undo the error a sum or a product of site coordinates carries into a value, which reaches far above the 15th digit, so geometry is compared by the tolerances below and not for equality.
2. That text is read as a decimal, Python decimal.Decimal and C# decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture). The text of a value under 1E-04 carries an exponent, such as 5.55111512312578E-17 for 0.1 + 0.2 - 0.3, 4E-06 or 1.234E-05, and decimal.Parse with InvariantCulture alone, whose default styles take no exponent, throws a FormatException on each on net48 and on net8.0, while NumberStyles.Float reads each and rounds it to 0 (RAN, 5 of 5 texts on both runtimes, round_addin\logs\pr49_last_float_parse.txt). A value whose digit after the asked decimals, the one that decides a tie, lies past the 15 digits of the text is refused by both sides and never written, at 3 decimals every value of 1E+11 or more, so a value past the decimal type, 7.9E+28 or more, is refused as well: there the text itself rounds, and G15 of net48 breaks an exact tie of 16 digits half up where Python and net8.0 go half to even, so a value of 13 whole digits ending in .125 gave texts one unit apart (RAN, review of PR 50, round_addin\logs\p4afix_r385_second.txt). No coordinate or level comes near it.
3. The decimal is rounded half away from zero to the decimals of R2, Python quantize with ROUND_HALF_UP and C# Math.Round(decimal, n, MidpointRounding.AwayFromZero).
4. It goes back to a binary number for the JSON, and minus zero is written as 0. A value that is not finite stays None, as common.r3 gives it.

The ties under R385, the same in both languages (RAN, the same probe): 12.3455 to 12.346, 15.1235 to 15.124, 1.0005 to 1.001, 2.0465 to 2.047, 7.0625 to 7.063. Half away from zero is the mode C# writes today, so the Python outputs move, by one unit of their last decimal on a tie (Q078). A C# output moves as well where its double lies under the tie: Math.Round on the double rounds its binary value, so 0.5005 gave 0.5 in C# and gives 0.501 under R385 on net48 and net8.0 (RAN, Phase 4a, round_addin\logs\p4a_r385_first_csharp.txt). On 200,012 made up values Python, C# on both runtimes and a rounding by integer arithmetic on the exact fraction of the 15 digit text gave the same on every one (RAN, p4a_r385_second.txt). In the level builders R385 moved the FFL of a piece up by 1 mm on a tie on 21 to 59 plots per key, and 350 level items of the build proof lost their key, since the level overrides were measured on the round() of before. So Phase 4a put R385 into python/common.py and C# only, where 0 unexplained holds on every key, and Phase 4b put it into the level builders and the rule paths with the level overrides measured again on it: 0 unexplained on every key again, 126 heightM gaps 1 mm smaller, 23 gone and 20 new (RAN, round_addin\logs\p4b_proof_after.txt and p4b_list_compare.txt, Q080). The height is the FFL less El @ Entry as written and SSL the FFL as written plus the ground, so the C# takes them from the written values as well. config/project.json majority.overrideRounding names the rounding the overrides of each level profile were measured on, R385 for parsons2024Rev6 and the round() of Round 17 for native2022, whose native overrides were never measured again (Q050): a Python build of native2022 keeps round(), and the C# builds parsons2024Rev6 only, so it carries R385 alone (Round ADDIN 4b review).

### Parity tolerances (R345 planned)

LandXML and DWG values stay binary floats through every sum and product and become decimal only when R385 writes them, so two sides that take the same terms in another order give other values, and a sum of products of site coordinates of millions of metres loses far more than its last bits. On 20,000 made up four sided faces at such coordinates the shoelace area in reversed order differed from shapely by up to 3.8E-04 m2 and 1,483 areas differed after R385 at 3 decimals, where the review counted 1,786 of 20,000 on its own made up set, and the centroid of a plain loop on the raw coordinates was up to 1.56 m off. About the first vertex of the ring the same loops held shapely within 9.1E-13 m2 for the area and 1.4E-09 m for the centroid, 0 of 20,000 differing after R385, two orders of one point sum differed by 5.8E-11 m, two formulas of the level in a triangle by 3.0E-12 m, and the affine fit of 300 page points by normal equations about the means held numpy lstsq on the raw values within 4.4E-07 m over 200 fits (RAN, round_addin\logs\pr49_last_tolerances.txt, the probes in pr49_last_probes). So the C# takes areas and centroids about the first vertex of each ring and the fit about the means, and exact equality of geometry with shapely and numpy is not asked. Each field is compared by its kind:

| Kind | Fields | Before rounding | After rounding |
|---|---|---|---|
| Points and lengths | face and outline vertices, inserts, corners, centroids, curb and setback edges, site points of the location plan, offsets, wall ends | 0.000001 m | equal, or one unit of the last place apart only where the two unrounded values lie within 0.000001 m and straddle a rounding boundary of R385, each such case listed |
| Levels | road levels, road at front, El @ Entry, FFL, SSL, height, ramp corners, Prototype-FFL, wall base and top | 0.000001 m | equal, or one unit of the last place apart only where the two unrounded values lie within 0.000001 m and straddle a rounding boundary, each such case listed |
| Areas | face and floor areas | 0.000001 m2 | equal, or one unit of the last place apart only where the two unrounded values lie within 0.000001 m2 and straddle a rounding boundary, each such case listed |
| Angles | link rotation, angle to true north | 0.000001 degree | equal, or one unit of the last place apart only where the two unrounded values lie within 0.000001 degree and straddle a rounding boundary, each such case listed |
| No geometry | counts, plot numbers, codes, names, hands, mirror flags, steps, ramp types, surfaces, rules, the why, Use | none | exact |

Each tolerance in metres is 1/500 of the 0.5 mm pass limit of R331 and over twice the worst order error of the probes, the fit at 4.4E-07 m. PDF page points are compared within 0.01 pt as the location plan reads them. A one unit case is listed by step, plot, field and both unrounded values in the parity output outside git, the log of its PR counts them, and any other difference fails the step, so a 1 mm flip whose unrounded values lie further apart than the tolerance still fails. The tolerances live in config, addin.parity (Phase 4a), and python/parity.py and the C# tests read them from there.

### The steps

| Order | Step | Python source | Rules | Parity test, planned | Tolerance |
|---|---|---|---|---|---|
| 1 | Coordinates from CAD | key_rules.py site facts of the reads (R331), site_spec.py and SiteCoordinates of R211 | R211, R331, R353 | test_r353_parity_position: the position file against the IMC read, and the check points of button 1 against SiteCoordinates | the file against the read within 0.5 mm as R331 holds a read, the check points of C# and Python by the points tolerance, the angle by the angles tolerance |
| 2 | Plot faces | CadReader plots.json, yard_rule.face_shapes, the refusals of face_rings.py (R345) | R5, R22, R345, R348 | test_r348_parity_faces | every vertex by the points tolerance and the area by the areas tolerance, a ring that is not simple and rings of one label that overlap refused on both sides |
| 3 | Labels and official numbers | location_plan.run with the schedule fit of R181 and the control point fit of R387 as its check, wiring.point_inside, data_book.build, area_key.area_rows, plan_from_book.book_rows and nbh_plots | R177, R179 to R183, R256, R350, R351, R352, R387 | test_r351_parity_numbers, test_r352_parity_plot_table | number, code, Typology_Rev9, zone and NBH exact per face, site points by the points tolerance, the kept points of the fit and the faces the control point fit numbers otherwise, with why, exact |
| 4 | Footprint clusters | plan_from_book.footprint_clusters, joined_clusters, numbered_clusters, cad_analysis clusters | R64, R70, R236, R354 | test_r354_parity_clusters | the plot set of every cluster exact |
| 5 | Front edge and corner front | cad_links.front_frame, corner_front.corner_frame, plan_from_book.fronts | R122, R225, R226, R332, R355 | test_r355_parity_fronts | the same edge, corners by the points tolerance, frontRule exact |
| 6 | Link frame | plan_from_cad.link_values, plan_rules.key_frame_section, plan_from_book.moved_link | R121, R332, R337, R356 | test_r356_parity_link_frame | insert by the points tolerance and rotation by the angles tolerance |
| 7 | Hand rules | majority.side_neighbours, road_side, kind_of and pair_kind, key_rules.row_pairs, cad_links.cluster_roles, plan_rules.unit_class, joined_factors and key_hand | R271, R333, R337, R357 | test_r357_parity_hands | the kind, the pair kind, the unit class, the mirror flag and the hand rule exact |
| 8 | Ramp and floor outlines | ramp_outline.setback and outline, plan_from_book.generic_floor and ramp_floor, plan_from_cad.flat_floor, plan_rules.key_outlines, key_yard and key_ramp_class | R127, R135, R208, R210, R334, R337, R358 | test_r358_parity_floors | every vertex by the points tolerance, ramp type exact, the Z of the ramp corners by the levels tolerance on the chain of Step 8 below, the back corners from El @ Entry as written at 4 decimals and never from its raw value |
| 9 | Road level snap from LandXML | plan_from_book.load_roads and road_level, c3d_connect.snap_z and road_level, plan_rules.road_snap: the surfaces in their order within snapM, a snapM that is not a finite number from 0 to snapMaxM refused, a corner none covers given no level and listed in noRoadLevel with the surfaces tried and why (R344) | R40, R42, R157, R339, R344, R349 | test_r349_parity_road_levels | by the levels tolerance, surface exact, the corners with no level and their plots exact |
| 10 | Levels | plan_rules.entry_of, which also gives the road at front from the same unrounded mean (7.5), row_partners, key_pieces, level_pieces, key_level_rows, steps_by_rule, prototype_ffl, plan_from_book.key_levels and rule_levels | R67, R278, R339, R340, R359 | test_r359_parity_levels | El @ Entry, height, the road at front, FFL, SSL and Prototype-FFL by the levels tolerance, the row partners, the pieces and the steps exact |
| 11 | Walls | none yet, python/plan_walls.py is written first from the rules R335 measured | R335, R362 | test_r362_parity_walls | ends by the points tolerance, base and top by the levels tolerance |
| 12 | Overrides | plan_rules.load_overrides and gap_of, override_source.default_sources, the use and skip of R386, the Outcomes of plan_from_book.py | R272, R280, R330, R337, R340, R360, R386 | test_r360_parity_overrides | applied, left and skipped per plot and field, and the why, exact |
| 13 | Parameters | plan_rules.key_parameters, the link name of R107 | R85, R107, R337, R361 | test_r361_parity_parameters | text exact |
| 14 | Issues | the missing list of plan_from_book.py, the cannot list, decidedByQuestion, the join statuses, python/check_rows.py written first as the reference of the check table | R272, R326, R363 | test_r363_parity_check_rows | every row, its type and its Use exact |
| 15 | Plan summary | the summary of plan_from_book.py: frontRule, roadSurfacesOfFrontCorners, floorsFound, floorTypes, and under rules applied, handRules, overridesNotApplied and steps, which fill the top line of the level Excel and the box Rules applied of the report | R364, R365 | test_r365_parity_summary | every count and every name exact |

### Step 8, the Z of the ramp corners

The ramp corners carry a level each, and the build takes them in this chain, which the C# copies step for step, since the ramp corners of the keys were measured on it (R339, R340) and a shorter chain moves a corner by 1 mm on a tie (READ, plan_from_book.ramp_floor and build, Round ADDIN 4b review):

1. The two front corners are the road levels of step 9 at the front corners, raw, each plus the rampFrontM gap of the plot when the override list holds one, raw.
2. El @ Entry is the one of step 10 as the plan writes it: the rule rounded to 4 decimals by R385, then the entryM gap added and rounded to 4 again (R340).
3. Each back corner is that El @ Entry plus its front corner less the mean of the two front corners, so the back pair runs parallel to the front with its mean at El @ Entry (R210), raw.
4. All four are written at 3 decimals by R385 (plan_from_past.point), and heightOffset is the lowest of the four raw values less the elevation of the ramp level, at 3.

So the back corners are rounded twice, once to 4 at El @ Entry and once to 3 when written: a road at 10.0 with a raw El @ Entry of 10.02745 gives El @ Entry 10.0275 and back corners of 10.028, where one rounding of the raw value gives 10.027. The parity of step 8 compares the corners each side writes, and its one unit cases are judged on the values of step 3 above, not on the raw El @ Entry.

### The location plan fit (R181, R387 planned)

The schedule fit of R181 numbers every build (CHOSEN, the review of PR 49). location_plan.py fits the page to the site on the labels of the plots of the IMC schedule, dataBook.locationPlan.fitNeighbourhood IMC with the workbook 00101-PSN-IMC-XXX-SCH-TU-090000.xlsx and the shift 10 of connect.neighbourhoods in python/c3d_probe.json (READ, location_plan.run), and keyRules of parsons2024Rev6 and the overrides of the list, 12,421 after PR 52, were measured on plots numbered by that fit. A build numbered by another fit could put a number on another face and an override on another plot. The schedule belongs to the project, not to a site file, so a new site file is numbered by it as well: Phase 4a moves its name and shift into config beside the position file, the worker reads it read only with the DLUS reader and hashes it as an input of button 2 (section 3, Steps), and since the mock gives it no row it has none in the pane, its path in the tooltip of the Settings row (7.7). A config that names no fit schedule, or one not on disk, stops button 2 naming it. The parity holds the C# schedule fit against the Python one, the kept points exact, each site point by the points tolerance and every face exact.

R387 gives location_plan.py --check control the control point fit as a check of that numbering, never a numbering (Q081, answered 2026-10-26): the mark of each control point is the one curve of the mark shape of dataBook.locationPlan.controlMark, 3.96 by 3.96 pt with 6 points, within controlSearchPt of the start of its E = text, a text with none or two refused. The three marks lie almost on one line, the middle one 0.55 pt, 0.97 m, off the line through the other two (RAN, round_addin\logs\p4b_r387_probe2.txt), so they give no affine map of their own, and the fit starts from their similarity, a scale, a turn, a mirror or none and a shift by least squares, of the two mirror states the one with the smaller largest residual. It is then refined by the fit of R181 on the centroids of the faces that hold one label each under it. Every face it numbers otherwise than the schedule fit is listed apart with both numbers and why: each label the two fits put apart, its face by each, how far it lies from the edge of the face it is in and how far apart the two fits put it. Button 2 lists each as an issue and the C# gives the same list, compared exact. On the location plan of the data book (RAN, round_addin\logs\q081_run.txt): the marks 0.01 to 0.041 m off their stated points under the similarity, the refine keeping 2,702 of 2,911 faces with one label, the two fits within 0.129 m of each other at every one of the 3,181 labels, 0.039 m at the median, and 4 faces listed apart, each with a client code label: 372 and 2646 by label 2656, 0.005 m from the edge of face 372 with the fits 0.029 m apart at it, 1759 and 1760 by label 1770, 0.028 m from the edge of face 1759 with the fits 0.05 m apart. A second route, the PDF by pypdfium2, the similarity in closed form, the refine by the normal equations of R184 and an own ray cast, lists the same 4 faces with the same numbers (RAN, q081_second.txt). labels.csv of the run equals the snapshot by SHA-256, so the check numbered nothing. The refine moves the marks from 0.01 to 0.041 m to 0.162 to 0.319 m off their stated points (markGapRefinedM of the report), where the schedule fit has them 0.265 to 0.305 m off, and the similarity start alone, with no refine, lies 0.289 m from the schedule fit at the median of the 3,181 labels and 0.48 m at most and numbers 19 faces otherwise, 17 of them unit faces (RAN, round_addin\logs\q081b_startonly.txt), so the check as chosen can differ from the schedule fit only a little: it tells the faces the two fits put apart, not which fit is right. The schedule fit of IMD stays the second method of R184.

### The Python changes for parity

Each is made in the repo with its tests before the C# step that is compared with it, and its effect on the 7 keys and on the build of NBH 012 is counted in the log of its PR.

| Id | Python change | Tests named | PR |
|---|---|---|---|
| R385 | python/common.py gains fixed(value, decimals) by the four steps above, NumberStyles.Float in C#, r3 and round_tree go through it, and so does every round() of a written value in plan_rules.py, plan_from_book.py and levels.py once the level overrides are measured on it (Q080). JsonOut.Round3 and the worker take the same four steps | test_r385_a_tie_rounds_half_away_from_zero_on_the_decimal_value with the five ties above, test_r385_float_noise_does_not_move_a_value, and in C# on net48 and net8.0 R385_a_tie_rounds_half_away_from_zero with the same values and R385_float_noise_does_not_move_a_value with 0.1 + 0.2 - 0.3, 4E-06 and 150.38 less 150.0, whose 15 digit texts carry an exponent or float noise | 4a |
| R386 | the override list keeps Use: majority.write_overrides writes skip after cannot and keeps it whole, override_source keeps use with its entry and refuses a value other than rule or past, plan_rules.load_overrides leaves an entry marked use rule and gives back the skip plots, plan_from_book.py leaves a skip plot out and lists it in missing as skip | test_r386_a_skip_list_survives_a_majority_run, test_r386_an_entry_marked_use_rule_is_left_by_the_build, test_r386_a_skip_plot_is_not_written | 4b |
| R387 | location_plan.py --check control, the marks the curves of controlMark, the start their similarity, the refine of R181 on the faces with one label, as a check of the schedule fit of R181, which stays the numbering: it numbers nothing and lists apart every face it numbers otherwise with why | test_r387_the_marks_are_the_curves_of_the_mark_shape_and_the_fit_starts_from_their_similarity, test_r387_the_refine_takes_only_faces_with_one_label, test_r387_a_face_numbered_otherwise_is_listed_apart_with_its_reason_and_the_check_numbers_nothing | after Q081, the PR of Q081 and Q073 |
| R388 | landxml_reader.read_landxml refuses a file with no Units element, no linearUnit or a linearUnit other than meter, where a missing one is read as None today, plan_from_book.py and data_book.py refuse a CadReader output whose units is not Meters | test_r388_a_landxml_in_feet_is_refused, test_r388_a_landxml_with_no_unit_is_refused, test_r388_a_drawing_in_millimetres_is_refused | 4b |

The Python build of NBH 012 by the parsons rules wrote 336 of 337 plots (RAN, round_addin\design\work\plan_012.json and plan_012_build.txt, the 28 inputs equal by hash before and after), its run time was not logged. Phase 4 PRs, each at most four changes: 4a R345 to R347 and R385, 4b R386 to R388, 4c R348 to R350, 4d R351 to R353, 4e R354 to R357, 4f R358 to R361, 4g R362 to R365, 4h R366 to R369.

## 6. Settings

The user chooses every setting in the pane, nothing is read from or written to the registry (R106).

- What: Settings (the project settings file, config/project.json for this project), Rules (the rules profile, rules/roshn-makkah.json), Level profile (a name of levelProfiles of the rules file, levelProfiles.default when none is chosen, parsons2024Rev6, R277), the seven input files and folders, the scope and the dry run box, each a row of the mock. The override list and the work folder have no row in the mock, so they have none in the pane either: the config file the Settings row names holds them in its addin block, overrideFile (config/overrides/roshn-makkah.json, majority.overrideDefaultSources parsons applied, R330) and workFolder, and the tooltip of the Settings row shows both paths with the fit schedule of the location plan (section 5). So the pane matches the mock, and the user changes either by editing the config file.
- Where: one Extensible Storage entity on the ProjectInformation element of the document, schema SiteAdaptation_Settings (letters, digits and underscore only, as section 3 says of every schema name) with a fixed GUID, vendor SITEADAPT (the VendorId of the manifests), read access public, write access vendor, one string field holding the settings JSON of Common (R367 planned). So the settings are saved with the .rvt, travel with it and come back when it opens. A new project starts with the defaults config names, prefilled from inputRoot and drawings of config (READ, config/project.json).
- The settings JSON: formatVersion, configFile, rulesFile, levelProfile, inputs (baseplan, villaBlocks, curbs, roadSurface, dlus, sales, plotNumbers, prototypes, each with its full path, size and SHA-256), scope (kind, nbh, plots), dryRunFirst, and per button its last run: time, mode, scope, the hash of every input, counts and issues. Paths are kept as chosen, the pane shows the file or folder name (7.2).
- When: a pick in the pane changes the settings in memory only. The Settings entity is written only inside the transaction of a real run of a button, with the record of that run, so a pick, a dry run and Cancel write nothing to the document and leave Document.IsModified as it was. The mock pane has no save control, so the picks reach the file with the next real run, and picks not yet written are lost when the document closes. A settings JSON of a later formatVersion is refused with both versions named.
- Work folder: by default `C:\Users\<user>\Documents\SiteAdaptation\<file name>_<path hash>`, the Documents folder of the user (Environment.SpecialFolder.MyDocuments), the file name of Document.PathName without its extension, and the first 8 hex digits of the SHA-256 of that full path after Path.GetFullPath and in lower case, since Windows paths ignore case. So two .rvt files of one name in two folders never share a work folder, and Document.Title, which is empty for a detached document, names none. A document with no path, never saved or detached, has no work folder: every button stops with "Save the project first, its work folder is named by its file" and the pane shows its steps with no status. Never beside the .rvt and never beside the sources in C:\02-Site Adaptation. A workFolder of the config that lies inside inputRoot is refused, since the sources stay read only (R1). It holds the reads, plans, checks, run logs, the dry folders and the two Excel files of each NBH.
- Config gains in Phase 4a a block addin: overrideFile and workFolder (above), the position file (Q068, outside git), the file names of the config files beside it the build reads (floorOutlines floor_outlines.json, prototypeOffsets prototype_offsets.json, cadLayers cad_layers.json, roadSurfaces road_surfaces.json, areaCodes area_codes.json), the wall types, the shared parameter names with the GUIDs the keys hold (read from the key copies in Phase 4b, so a new site file and the keys share them), the floor type thicknesses read from floorTypes of the key reads (R53, the Default Thickness of each type, Generic 150mm 0.10 m in the IMC key and 0.15 m in the IMD key, READ round_addin\logs\key_level_bases_pr49.txt), workerPeakMiB per input and workerTimeLimitS per command (R347), and the check point pass limit 1 mm. minFreeGB stays siteBuild.minFreeGB, the 2 GB floor of R200. No project value goes in code (CLAUDE.md).

## 7. Every screen and file of the mock

In the order of docs/addin-mock.md, every sentence of the mock word for word with its punctuation. The two sentences that name a file stand in a text fence, as in the mock, so Markdown keeps their angle brackets as written. The values are those of NBH 012, the IMC area, from the outputs of this round, each with its source. A coordinate shows [coordinate], a level [level]. The fully filled pages are in round_addin\design\mock_filled.md outside the repo.

### 7.1 Ribbon

Ribbon tab "Site Adaptation" with three ribbon panels in this order: Setup (1 Set up site, 2 Read data, 3 Check), Build (4 Place villas, 5 Floors and ramps, 6 Levels, 7 Walls), Output (8 Report). Large buttons, one icon each.

- How: App.OnStartup calls UIControlledApplication.CreateRibbonTab("Site Adaptation"), then CreateRibbonPanel for "Setup", "Build" and "Output" in that order, and one PushButtonData per button with its text as the mock gives it, the full class name of its IExternalCommand, a LargeImage of 32 px and an Image of 16 px, so each is a large button in the normal Revit look (R371 planned). The icons are PNG files of the add-in, one per button, the number of the button on a plain square (CHOSEN, the mock asks one icon each and shows none). An availability class keeps the buttons grey with no project open.
- Values: the labels only.

### 7.2 Dockable pane

Dockable pane "Site Adaptation" on the right, project name at the top right, four sections in this order:

- How: App.OnStartup registers a DockablePaneId with a fixed GUID and an IDockablePaneProvider whose FrameworkElement is a WPF Page and whose InitialState docks it right. The page is a stack of four sections under a top row, each a header TextBlock and a Grid. The project name is a TextBlock aligned right in the top row, Document.Title of the active document, or "not saved" when the title is empty, as for a detached document. On DocumentOpened and on a new document the add-in gives the document a session id, a new GUID held in memory beside the Document, which the requests of its worker runs carry (section 2). The pane, its view model and the check table belong to the active document: on ViewActivated when the document of the view changes, on DocumentOpened and on DocumentClosed the pane loads the settings and the step state of the active document again, every row and every status, and a check table window open on another document closes, so no row and no button click acts on a document that is not the active one, and a worker answer acts only on the document of its request, active or not (section 2). With no document open the pane shows its labels with no values and every button is grey. A real run reaches the model through an ExternalEvent (R372 planned).
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
| Plot numbers | TextBlock and Browse | 01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf, then in grey 2.91 MiB 7061ac0d | dataBook.locationPlan.pdf, the file of Master Plan Rev.09, 3,056,293 bytes |
| Prototypes | TextBlock and a folder Browse | Prototypes_2024 | pastJob.upgradedPrototypeFolder |

How each input row tells two files of one name apart: the TextBlock shows the file or folder name as the mock asks, then in a smaller grey run its size and the first 8 hex digits of its SHA-256, and its ToolTip gives the full path with the folder. So the two different files named 01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf on this machine, 3,056,293 bytes (7061ac0d) and 2,395,117 bytes (24ffcbac), read apart in the pane (READ, 3.2). The label of the row stays the mock's.

Scope: radio Whole project, Neighbourhood [code], Plot list. Checkbox "Dry run first, change nothing", on by default.

| Label | Control | Filled | Source |
|---|---|---|---|
| Whole project | RadioButton | not chosen | |
| Neighbourhood | RadioButton and a ComboBox of the NBH codes of the plot table | 012 chosen | area key of R256, the NBH of the IMC key (R326) |
| Plot list | RadioButton and a TextBox of official numbers and ranges, such as 2290-2299, 2311 (section 3, Scope) | not chosen | |
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

- How: a modal WPF Window, its owner the Revit main window by WindowInteropHelper, title "1 Set up site". The badge is a Border with a TextBlock, its text by the test of 3.1: Existing site file when the document holds the Settings entity of SiteAdaptation_Settings, a shared parameter bound with the GUID or the name of one of config, a project position other than zero or a survey point or project base point off the internal origin by more than 0.5 mm, else Empty project found, never by its types, which the default metric template holds too. A line under the badge names the signs found (R373, R374 planned). The left is a Grid of two parts, the right a DataGrid, the buttons a right aligned StackPanel. The dialog is filled by a dry run of button 1 in Common, the Revit side only reads (SiteTarget).

Left: "What the project has now" (Survey point, Project base point, Levels, Floor types and wall types, Shared parameters, Units) each with its state, then "What the tool will set" (Coordinate system, Survey point, Angle to true north, Levels, Floor types, Wall types, Shared parameters, Units) with the values it will set.

The states of What the project has now:

| Row | Empty project found, from the default metric template | Existing site file, the IMD 2024 copy | Source |
|---|---|---|---|
| Survey point | at the internal origin of the template | [coordinate] | Revit BasePoint.GetSurveyPoint, the RD read |
| Project base point | at the internal origin of the template | [coordinate] | BasePoint.GetProjectBasePoint, the RD read |
| Levels | Level 1, Level 2 of the template | Level 1 [level], Level 2 [level] | site_report_i18.json, levels kept, the RD read |
| Floor types and wall types | Generic 150mm, no yard, no ramp, the wall types of the template UNKNOWN until Phase 5b reads them | Flat Ramps, Generic 150mm, Ramps 0.5%, Ramps 2%, yard, BW and 4 garden wall types | site_report_i18.json, the RD read and key_reads.json |
| Shared parameters | none | the 9 RRE parameters and CASE on its links | site_report_i18.json made 10, the RD read |
| Units | metric, the length unit of the template is UNKNOWN until Phase 5b reads it | meters, accuracy 0.01 | the RD read, R311 |

The values of What the tool will set:

| Row | Value it will set | Source |
|---|---|---|
| Coordinate system | shared coordinates of CAD, site Internal, the Parsons project position [coordinate] and the project base point [coordinate], which has no row of its own in the mock and is shown here | keyRules.site, the position file (Q068) |
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
| control 1 | [coordinate] | [coordinate] | 0.00 mm |
| control 2 | [coordinate] | [coordinate] | 0.00 mm |
| control 3 | [coordinate] | [coordinate] | 0.00 mm |
| plots min | [coordinate] | [coordinate] | 0.00 mm |
| plots max | [coordinate] | [coordinate] | 0.00 mm |

Pass limit 1 mm. 5 of 5 pass.

- Source: the five check points of the newSite job of the IMC new site file of Round 17 from the default metric template, each 0.0 mm, and its survey point 0.0 mm (READ, site_report_i18.json). That run placed the position of R211 and took plots min and plots max of the IMC plots of its round. Button 1 takes the extreme plots of the whole baseplan (section 3, Scope), and under Q068 it adds the survey point and the project base point as two more rows, held the same way, so Phase 5b fills these rows again. For the existing IMD copy the survey point and the project base point are 0.0 mm from the IMC key in shared and internal coordinates (RAN Phase 1, p1_compare.json).
- How: a DataGrid with four text columns, CAD and Revit as east, north and elevation in metres, Revit as 3.1 Checks gives it, the value Apply would give in a dry run and the value Revit reports after Apply, Diff in mm with two decimals rounded by R385, so a gap of 1.04 mm reads 1.04 and not 1.0. A point passes when the Diff shown is at most the pass limit, so what the table shows is what is judged. The line is a TextBlock under it.

Below it: "On an existing site file" with a line saying nothing changes until Apply.

- Filled: "On an existing site file", then "Nothing changes until Apply. Apply sets only the rows that differ.", the differing rows marked in What the tool will set, with no tick per row. Source: the round prompt.
- How: a TextBlock header and a TextBlock line, shown on both badges.

Buttons Cancel, Dry run, Apply.

- How: Cancel closes with no change. Dry run runs the dry run of button 1 and fills both tables. Apply runs the real run in one transaction and closes when it commits, else shows the reason and stays open (R382 planned).

### 7.4 Check table

Check table. Title "3 Check, NBH [code], [n] plots", a line with the level profile and the override file.

- How: a modeless WPF Window owned by the Revit main window, so the user can work in Revit while it is open, built from `checks_<NBH>.json` and bound to the document it was opened on, which closes it when another document becomes active (7.2). The title is a TextBlock, the line under it a TextBlock (R376 planned). For Neighbourhood [code] is the code of the NBH. For Whole project the title reads "3 Check, NBH all, [n] plots", [code] showing all and [n] every unit plot of the scope in one table, the NBH column telling the rows apart and the filter chips counting over all of them. For a Plot list [code] is the codes of the NBH the list touches, joined by commas.
- Filled: "3 Check, NBH 012, 337 plots", then "Level profile parsons2024Rev6, override file roshn-makkah.json, 12,421 overrides, 1,227 of them on NBH 012". Source: the 337 client code labels of NBH 012 (RAN, labels.csv), the list after PR 52 (READ, config/overrides/roshn-makkah.json, 12,421 entries, 1,227 of key imc). The counts of the summary table and the chips below were taken on the 3c list of 12,424 with 1,225 of key imc and are not counted again.

Buttons Zoom to plot, Export to Excel, Save overrides.

- How: three Buttons. Zoom to plot raises an ExternalEvent that calls UIDocument.ShowElements on the tagged elements of the plot, else zooms the active view to the face box by UIView.ZoomAndCenterRectangle. Export to Excel asks the worker to write the rows shown to `Check_<NBH>.xlsx`. Save overrides writes the rows whose Use the user changed in this session into the override list as 3.3 says, each marked byHand and no other row, and rebuilds the plan.

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

Columns: Plot, NBH, Unit, Check, Tool value (rule), Past job, Rule, Use. Use is a dropdown Rule, Past, Skip. Past writes the past value to the override list with source parsons. Skip leaves the plot out and lists it in the report. Selecting a row zooms the Revit view to that plot.

- How: a DataGrid, Plot, NBH, Unit, Check, Tool value (rule), Past job and Rule as read only text columns, Use a DataGridComboBoxColumn with Rule, Past and Skip, each row opening on the Use of 3.3. Past writes in the form of the table of 3.3, a gap to the rule for a level or a position and never the level or the coordinate, and is grey on a row whose field no build reads, on every row of an existing site file that is no answer key and on every row of a plot no answer key holds. Skip writes the plot to the skip list, which the report lists. A selected row raises the same ExternalEvent as Zoom to plot.
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

```text
Level Excel, file <NBH>_Levels_<profile>.xlsx.
```

- How: the worker writes it with DocumentFormat.OpenXml inside the transaction of button 6 to its temporary name, from the plan and the values the add-in read back, and the add-in moves it onto its name after the commit (3.6), one workbook per NBH of the scope (R364 planned). Filled: 012_Levels_parsons2024Rev6.xlsx.

A top line with NBH, Zone, road surface and the formulas used.

- Filled: "NBH 012, Zone 2, road surface Top Surface-02 on 672 of 672 front corners, formulas: El @ Entry = the road at the two front corners + the slope of the unit x the ramp depth (R278, R339), FFL = the highest El @ Entry of the piece + the base of its unit, to the mm (R340), SSL = FFL - 0.075 m (levels.ground_ssl), Height = FFL - El @ Entry, Steps = one for the base and one per riser over it, half a riser no step (R340)". Source: the summary of plan_012.json (RAN) and the rules profile (READ).
- How: row 1, one merged cell, bold.

Columns in this order: Plot No, Unit, Cluster, Road at front, El @ Entry, FFL, SSL, Height, Steps, Source (Rule, or Override with the reason).

| Plot No | Unit | Cluster | Road at front | El @ Entry | FFL | SSL | Height | Steps | Source |
|---|---|---|---|---|---|---|---|---|---|
| 2289 | VD4 | none | [level] | [level] | [level] | [level] | 0.2006 | 1 | Override heightM (breaks oneFflPerPieceAtTheHighestEntry), rampFrontM (breaks roadLevel) |
| 2290 | VD1 | 2290-2291 | [level] | [level] | [level] | [level] | 0.1000 | 1 | Override entryM (breaks roadLevel), heightM (breaks oneFflPerPieceAtTheHighestEntry) |
| 2292 | DT1 | 2292-2293 | [level] | [level] | [level] | [level] | 0.1003 | 1 | Override heightM (breaks oneFflPerPieceAtTheHighestEntry) |
| 2294 | DT3 | 2294-2295 | [level] | [level] | [level] | [level] | 0.1027 | 1 | Override entryM (breaks roadLevel) |
| 2295 | DT3 | 2294-2295 | [level] | [level] | [level] | [level] | 0.1003 | 1 | Rule |

- Source: plan_012.json levels and levelRule per plot, the cluster by clusterPlots of majority.json (the site clusters a build takes, R271), the reason by the override list (RAN, mock_values.py). Of the 336 rows 24 show Source Rule and 312 an override of El @ Entry, the FFL, the steps or the ramp front, since El @ Entry holds by the rule alone on 157 of 1,753 key plots (Q001).
- Road at front is the front level the rule takes: the mean of the road levels at the two front corners, as entry_of of R278 and R339 takes it before the offset and the slope, from the unrounded corner levels of the road snap. It is never taken from levelRule.rampFrontCorners, which the plan writes at 3 decimals per corner while entry_of takes the unrounded mean (READ, plan_from_book.rule_levels and plan_rules.entry_of l.85 to 88), so the two could part by up to 0.5 mm. The build writes that unrounded mean into the levels of the plot as roadAtFrontM, and Road at front and El @ Entry are each rounded once from the same unrounded values by R385, at 4 decimals (R359). The two corner levels stay in plan_<NBH>.json, so the ten columns and the four sheets stay those of the mock.
- How: a header row and one row per written plot, Road at front, El @ Entry and Height with 4 decimals as R2 keeps them, FFL and SSL with 3, a plot not written listed at the end with its reason.

Sheets: Levels, Clusters, Overrides used, Settings.

- Levels: the table above. Clusters: one row per piece, its plots, unit sequence, split (levelSplit.bySequence) and FFL [level]. Overrides used: plot, field, gap, reason, source parsons, model of the key, from the list. Settings: the run id of the run of button 6 that wrote it, the profile, the SHA-256 of the rules file and the override list, the road surface order, the pass limit, the run time.

### 7.6 Report Excel

```text
Report Excel, file <NBH>_Report.xlsx.
```

- How: the worker writes it with DocumentFormat.OpenXml from the run logs of buttons 1 to 7 and the counts the add-in read from the model at report time (R365 planned). Every number of one report comes from one build, the plan of the last real run of button 3, and one read of the model. Filled: 012_Report.xlsx, every number of this page from the one build plan_012.json (RAN).

A top line with file name, NBH, plot count, profile, run time.

- Filled: "[file name].rvt, NBH 012, 337 plots, parsons2024Rev6, run time [n] min". Source: the plot count and the profile of plan_012.json (RAN). The build is not placed yet, so the file name and the run time stay open until Phase 6 places it. The Round 17 IMC rebuild placed another build into 01101-PSN-IMC-R17B-TEST-NEW.rvt in 7.96 min over 13 place runs (READ, place_codes_b.txt) and is not shown on this page.

Summary table with columns Step, Done, Skipped, Override, Issues, one row per button 1 to 7.

| Step | Done | Skipped | Override | Issues | Source |
|---|---|---|---|---|---|
| 1 Set up site | 17 | 0 | 0 | 0 | what 3.1 sets on the default metric template: 4 floor types, 2 wall types and 11 parameters made, Level 1, Level 2 and Generic 150mm kept, the check points pass (CHOSEN by this design, UNKNOWN by a run until 5b, the Round 17 newSite run made the 14 of R212, 4 floor types and 10 parameters, READ site_report_i18.json) |
| 2 Read data | 337 | 43 | 0 | 0 | the snapshot plots.csv, 380 faces of NBH 012, 337 unit plots, 43 faces with no unit |
| 3 Check | 337 | 0 | 1,225 | 1,247 | 337 plots checked, the 1,225 overrides of the list and the 1,247 rows of 7.4 |
| 4 Place villas | 336 | 1 | 371 | 0 | plan_012.json: 336 links, 2312 not written, overrides applied mirrored 11, insert 4, CASE 206, Prototype-Foundation-Type 150 |
| 5 Floors and ramps | 1,003 | 8 | 123 | 112 | plan_012.json: generic 336, yard 331, ramp 336, the 3 floors of 2312 and 5 yards not written, rampType 10 and rampFrontM 113 applied, genericFloor 4, rampOutline 4, yardOutline 14 and rampFrontFallM 90 left (Q057) |
| 6 Levels | 336 | 1 | 619 | 0 | plan_012.json: entryM 304, heightM 300, steps 5, Prototype-FFL 5 and RVT-Name 5 applied, 2312 with no level |
| 7 Walls | 0 | 0 | 0 | 1 | no wall placed yet, Phase 4e codes the walls, the wall top rule is weak and no garden wall rule is measured (Q071), keyRules wallTop 0.346 |

- How: a table of the five columns of the mock from row 3, one row per button 1 to 7. The Source column above is the note of this design on where each count comes from and is no column of the file. The counts of steps 4 to 6 are those of the plan of NBH 012 a real run would place, read from plan_012.json (RAN). No Parsons build has been placed in Revit yet, Phase 6 runs it.

A box "Counts agree across outputs" with columns Item, Revit, Excel for Links, Floors, Levels.

| Item | Revit | Excel |
|---|---|---|
| Links | not placed yet | 336 |
| Floors | not placed yet | 1,003 |
| Levels | not placed yet | 336 |

- Source: the one build of this page, plan_012.json (RAN). Excel is what the files of that build hold: the rows of the Placed sheet of kind link, 336, and of the floor kinds, generic 336, yard 331 and ramp 336, 1,003, the rows of the Levels sheet of the level Excel, 336, the same numbers as the summary above. Revit is read from the model at report time by the tags, Levels by the elements the last run of button 6 that committed set (3.8), and no Parsons build is placed yet, so it stays open until Phase 6 places NBH 012. The plan count is held against both apart and a mismatch of any two is an issue of the report. The Round 17 IMC rebuild placed another build, 336 links and 999 floors (RAN, prototypes_imc_r17b.json against plan_imc_r17b.json), and is not shown here.
- How: a bordered range of three columns under the summary.

A box "Rules applied".

- Filled: one line per decision of keyRules the build applied with its coded rule, share and strong or weak: linkRotation frontRule 0.991 strong, frontCorner frontRule 0.978 strong (applied: frontCorner measures the link rotation of R226 on the corner plots, and the build gives the 110 plots of NBH 012 with two or more road edges the edge the Orientation of the sales sheet names and the other 227 the nearest curb, RAN summary.frontRule of plan_012.json), linkOrigin plotFrameRefit 0.971 strong, hand villa blockMirrorRoadSide 0.960 strong, hand cluster clusterSide 0.981 strong, hand alone rowPairSideOrBlockMirrorOpenEnd 1.0 strong, rampSlope typology 0.988 strong, rampOutline outlineR127 0.969 strong, generic all 0.991 strong, yard zone 1.0 strong, yardOutline fittedOutlineRefit 0.963 strong, entry roadLevel 0.088 weak, rampFront roadLevel 0.692 weak, levelSplit units 0.702 weak, baseHeight profile 0.844 weak, ffl oneFflPerPieceAtTheHighestEntry 0.749 weak, steps stepsFromHeight 0.988 strong, the parameters (CASE typologyMirrored 0.275 weak, Prototype-Foundation-Type 0.620 weak, Prototype-FFL fflDownToTheCm 0.994 strong, the rest 0.992 to 1.0 strong), with the hand rules of NBH 012: clusterSide 202, blockMirrorRoadSide 108, row pair or block mirror 15, override 11. Source: keyRules of rules/roshn-makkah.json (READ) and the summary of plan_012.json (RAN).
- How: a bordered range, one row per decision with its rule id.

Sheets: Summary, Placed, Skipped, Overrides, Issues, Rules.

- Summary: the top line and the three boxes. Placed: one row per placed element, plot, kind, type, the element id. Skipped: plot, kind, reason. Overrides: the overrides applied and left, by plot and field. Issues: every issue of every button. Rules: every rule id applied with its text from docs/rules.md.

### 7.7 What the add-in shows that the mock does not

The mock wins. Each item below is a screen or a part the mock does not show, kept because a rule of section 3 needs it, and Phase 6 step 6 checks the screenshots against the mock and this list, so each difference is named and none is found by chance:

1. The summary of a run before any write, with Cancel and Apply, for buttons 2 to 8: after the dry run when the box is ticked, and as the confirm when it is not (section 3). Button 1 has its own Dry run and Apply in the mock.
2. The TaskDialog of button 4 before its transaction when a link type must be loaded again, or reloaded since its file changed, with Load and Cancel and the line that Undo is cleared (3.4).
3. Leave or Set to the plan per element changed by hand, in the summary of a second run (section 3).
4. Past grey in the Use dropdown, with its reason as a tooltip, on a row whose field no build reads, on every row of an existing site file that is no answer key, and on every row of a plot no answer key holds, which has no key or model to write (3.3).
5. The size and the short SHA-256 in grey beside each input name, and the full path as a tooltip (7.2).
6. The line under the badge that names the signs found (7.3), and the marks on the rows of What the tool will set that differ from What the project has now, with no tick per row.
7. The tooltip of the Settings row with the override list, the work folder and the fit schedule of the location plan, which the config file holds since the mock gives them no row (section 6).

## 8. Install and the unattended runs

Install (R370 planned):

- dotnet/SiteAdaptation.AddIn/Install-SiteAdaptation.ps1, PowerShell 7 (#Requires -Version 7). It refuses while Revit runs, builds unless -NoBuild, copies the add-in, Common and the icons into %APPDATA%\Autodesk\Revit\Addins\2024\SiteAdaptationAddIn and the worker with its libraries and exe.config into its Worker folder, and writes SiteAdaptationAddIn_Revit2024.addin into %APPDATA%\Autodesk\Revit\Addins\2024 with the full path of the DLL. It copies no config file: the user picks config/project.json in the pane, and the config files it names lie beside it (section 6).
- The manifest: AddIn Type Application, Name Site Adaptation, FullClassName SiteAdaptation.AddIn.App, an AddInId of its own fixed in the repo, VendorId SITEADAPT and VendorDescription Site Adaptation Tool, as the three manifests of today (READ).
- No admin rights: every path is under %APPDATA% of the user. Never the registry: the script reads no CodeSigning key and writes none (R106), unlike the optional report of Install-TestHost.ps1. Revit asks once whether to load the unsigned add-in, Bader answers it by hand, the watcher answers Load Once in a test window (R108), whose list of the tool's add-ins gains the new manifest.
- One Common: Revit loads one SiteAdaptation.Common.dll for every add-in of the tool, the first it meets, so the script installs Common into every folder of the tool from the same build and checks one SHA-256 across them, as Install-TestHost.ps1 does today (READ).
- Inside Revit the add-in registers an AssemblyResolve handler first thing in OnStartup that serves only the assemblies of its own folder of the same or a higher version, and only to a request of the tool's assemblies or with no requester, never to another add-in (RAN in the host simulation of docs/licences.md). Newtonsoft.Json comes from Revit's own copy by the redirect of Revit.exe.config (READ).

Unattended runs for Phase 6 (R384 planned):

- Run-TestHost.ps1 gains a round spec of the buttons: a job of kind newProject makes `<name>-TEST-NEW.rvt` (R213) from DefaultMetric.rte or from no template with imperial units (Application.NewProjectDocument with UnitSystem.Imperial) and saves it under that name before any button, since a document with no path has no work folder (section 6), then a job of kind buttons names the settings, the scope (one NBH), the buttons 1 to 8, dryRun false, the answers of the dialogs (Apply in Set up site, the Use of every row as built, Apply after each dry run) and the screenshots. The guard of R103 learns both kinds, one window at a time under the lock of R200, the 2 GB floor, the watcher in our own Revit only.
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
| Memory of a full NBH build | the Road Layout read alone took 2,571 MiB in the saved log (RAN, cadreader_peaks_pr49.txt, which holds 2,571, 100 and 77 MiB private for the three drawings) and 2,576 and 2,612 MiB in the runs of 3b and of the claim-checker, neither output saved (READ, round-addin.md 3b), free memory fell to 0.53 to 1.42 GiB in each read window of 2a (READ, steps/PROGRESS.md), the whole NBH 03 image held 10,732 MiB and hung (Q063), each upgrade window of an STR prototype took up to 2,053 MiB (READ, round-addin.md) | the reads and the build run in the worker, which ends after each button, every large read checks free memory against its measured peak plus the floor first and refuses with both named (R347, section 2), Phase 5d places NBH 012 with all 13 prototype files in one window and logs the peak by R310 before any larger scope runs, and if one transaction for all codes does not fit, the change of the button to one transaction per code becomes a question |
| The 2 GB floor | R200 checks free memory at the lock, R310 logs it in every sample and stops nothing (READ) | the worker reads free memory by GlobalMemoryStatusEx before each large read against its peak plus the floor (R347), the add-in before each link type load against the floor, each stops naming both numbers, the floor and the peaks in config |
| The Zone 1 gap of Q066 | IMA and IMB hold no level, ramp or yard, the yard of Zone 1 is taken from Zones 2 and 3, notMeasured (READ, keyRules.yard) | the build says it on every Zone 1 plot, the check table shows an empty Past job and Source Rule (no key, Q066) |
| Open questions that set values | Q001 El @ Entry, Q012 the split, Q013 CASE, Q017 VD4, Q057 the ramp fall, Q066 Zone 1, Q068 the position, Q070 the entry parameter, Q071 the wall top, Q072 the side road, Q074 the steps, Q082 the 33 plots with no road level | each value lives in keyRules or the override list, never in code, and every plot a question decides shows that question in its check row and in Source, so the answer changes data, not code |
| Rough levels on an NBH with no key | by the rule alone El @ Entry holds on 157 of 1,753 key plots, the FFL on 217 (READ, round-addin.md 3c) | the Source column of the level Excel and the check rows say Rule or Override plot by plot (Q001) |
| Settings lost or wrong | a path chosen on another PC | the Settings entity keeps each path and hash, a missing file stops the button naming it |
| Revit API steps not yet proven | moving the survey point, a project from no template, the WPF pane and windows, the screenshots | each is on the open list below with the phase that measures it, proven on the smallest case in its Phase 5 PR on a copy, an attempt that fails three times becomes a question |

### Open until a Revit run

What only a Revit run can settle, each with the phase that measures it. Until then each stays as this design says, and a result otherwise changes this design first (R343).

1. Phase 5a: the session id given on DocumentOpened and on a new document, the pane loaded again at ViewActivated, DocumentOpened and DocumentClosed, and a worker answer that ends while another document is active written to its own document, or dropped with its issue once that document closed (section 2, 7.2).
2. Phase 5a: Document.Title and Document.PathName of a detached document and of one never saved, and the stop that asks for a save first (section 6).
3. Phase 5b: BasePoint.Clipped set on the survey point inside the transaction of button 1, the clip state in which MoveElement moves the survey point and not the shared coordinates, and whether SetProjectPosition moves the survey point or the project base point in internal coordinates (3.1, order step 2).
4. Phase 5b: the Elevation Base of the level type of a project from the default metric template and from no template, and Level.Elevation set on the Survey Point base giving the Level.ProjectElevation of the keys within 0.5 mm (3.1, order step 3).
5. Phase 5b: the length unit, the floor types and the wall types of a project from the default metric template and from no template, and whether one from no template holds a floor type at all (3.1, 7.3).
6. Phase 5b: the Revit column of the check points in a dry run, from the Transform made of the position Apply would set, against the value GetTotalTransform gives after Apply, equal at the two decimals of the Diff on every check point (3.1 Checks).
7. Phase 5b: the shared parameters bound by the GUIDs of config, read and written by Element.get_Parameter(Guid), and the stop with the list on a copy of 01101-PSN-IMC-R17B-TEST-NEW (3.1, Shared parameters).
8. Phase 5b: the badge on the four copies of 3.1, each by the signs it holds.
9. Phase 5c: RevitLinkType.Load of an unloaded type and RevitLinkType.Reload of a loaded type whose file changed, outside any transaction, the Undo list cleared, on one link each (3.4).
10. Phase 5d: the wait for the worker inside the transaction of button 6 within addin.workerTimeLimitS.levelExcel, the roll back when it fails, and the move after the commit onto an Excel held open, which leaves step 6 not Done (3.6).
11. Phase 5d: the peak memory of NBH 012 placed with all 13 prototype files in one window (Risks, memory of a full NBH build).
12. Phase 5e: the screenshots of the pane, the dialog and the check table by RenderTargetBitmap and of the ribbon by PrintWindow, in our own Revit only (section 8).

## 10. Planned rule ids, conflicts and questions

Planned rules, taken from R345 to R388, each written into docs/rules.md with its test in the PR that builds it. R385 to R388 are the Python changes the parity needs (section 5), so Phase 6 takes R389 to R399. Each PR holds at most four changes:

| Id | Rule | PR |
|---|---|---|
| R345 | The parity harness: --steps of plan_from_book.py, the step files of the worker, python/parity.py with the tolerances of section 5 by kind of field, the one unit cases listed | 4a |
| R346 | Common on netstandard2.0, the worker on net48 and net8.0, Newtonsoft.Json 13.0.1, the test project on net48 and net8.0 with a Linux leg | 4a |
| R347 | The worker run: the request and the answer with the document they belong to, the free memory against the measured peak plus the floor, the exit codes, the kill at the time limit, the work files by a temporary name and a rename, Revit not waiting but for the level Excel (section 2) | 4a |
| R385 | One rounding rule in Python and C#: the 15 significant digit decimal of the value read with its exponent (NumberStyles.Float), half away from zero, minus zero as 0, and R2 changed to name it | 4a |
| R386 | The override list keeps use and skip through a majority run, and a Python build leaves an entry marked use rule and a skip plot | 4b |
| R387 | The control point fit of the location plan, the marks by their shape, the start from their similarity, refined on the faces with one label, as a check of the schedule fit of R181 in location_plan.py: it numbers nothing and lists apart every face it numbers otherwise with why | after Q081 |
| R388 | The Python reference refuses a LandXML with no linear unit or another than meter and a drawing whose InsUnits is not Meters | 4b |
| R348 | DWG reads: faces, labels, blocks, curbs, ROW and setbacks, a drawing not in Meters refused | 4c |
| R349 | LandXML surfaces and the road level snap, a LandXML with no unit or not in meter refused | 4c |
| R350 | DLUS and sales reads | 4c |
| R351 | The location plan: text lines, pairs, the page to site fit by the schedule fit of R181, which numbers every build, number to face, and the control point fit of R387 as its check, the marks the curves of the mark shape, the start their similarity, the refine on the faces with one label, listing apart the faces it numbers otherwise with why, as the Python does | 4d |
| R352 | The plot table, numbered by the schedule fit of R351 | 4d |
| R353 | The project position file written from a key read by the worker, the check points and the shared parameter ids of the keys | 4d |
| R354 | Footprint and analysis clusters | 4e |
| R355 | Front edge and corner front | 4e |
| R356 | Link frame | 4e |
| R357 | Hand rules, the kind, pair kind and unit class with them | 4e |
| R358 | Ramp, generic and yard outlines and the ramp type | 4f |
| R359 | Levels, the row partners and the road at front with them, the road at front and El @ Entry from one unrounded mean | 4f |
| R360 | Overrides, the use and skip of R386 with them | 4f |
| R361 | Link parameters and the link name | 4f |
| R362 | Walls and python/plan_walls.py | 4g |
| R363 | Issues and the check rows, python/check_rows.py, Past in the form of the list by check and field | 4g |
| R364 | The level Excel, written to its temporary name inside the transaction of button 6 and moved after the commit, its run id, the road at front | 4g |
| R365 | The report Excel, one build per report, the counts made apart, Levels by the run id of button 6, the plan summary | 4g |
| R366 | Scope with the plot list syntax and its stops, the step state with the hash of every input and of the folders by their lists and every output on disk by its hash | 4h |
| R367 | The settings JSON kept in the project, written only by a real run | 4h |
| R368 | Element tags, the match of the found elements to the plan, the hand change, the copy and the schema versions | 4h |
| R369 | Second method of Phase 4, seeds 14 and 7 | 4h |
| R370 | Install script, manifest, one Common | 5a |
| R371 | The ribbon | 5a |
| R372 | The pane bound to the active document, a worker answer routed to its own document, the work folder by the file name and the hash of its path, the settings saved with the project | 5a |
| R382 | One transaction per button, the steps outside it, the confirm before any write, the dry folder, the roll back with the reason, the stop | 5a |
| R373 | Button 1 on an empty project, the order of its steps, the units by Document.SetUnits, the level by Level.Elevation on its stated base, the shared parameters by GUID with the stop on a name of another GUID, the shared parameter file of the run, the badge by what button 1 sets | 5b |
| R374 | Button 1 on an existing site file, its dialog with no tick and no position save | 5b |
| R383 | Element tags in Revit, schema names of letters, digits and underscore, the library and memory line of each run | 5b |
| R375 | Button 2 Read data | 5c |
| R376 | Button 3 Check, the check table, Use and Save overrides of the rows changed in the session only | 5c |
| R377 | Button 4 Place villas, link types by full path, the load and reload step before the transaction, a changed prototype file reloaded, the move in place of a tagged link | 5c |
| R378 | Button 5 Floors and ramps | 5d |
| R379 | Button 6 Levels, RRE_ZZ_SP_Prototype-FFL written | 5d |
| R380 | Button 7 Walls | 5d |
| R381 | Button 8 Report | 5e |
| R384 | The test host jobs of the buttons and the screenshots | 5e |

Conflicts, none resolved silently:

1. R85 never writes RRE_ZZ_SP_Prototype-FFL and R80 keeps it out of parameters, while keyRules writes it by fflDownToTheCm, 0.994 on 1,791 TU plots, and the plan of 3c holds it (READ). By the majority rule button 6 writes it, and R379 changes R85 and R80 in the same change.
2. R80 lists 9 link parameters and the newSite job of R212 bound Plot and 9, while the keys hold 10 on their links, RRE_ZZ_SP_Prototype-FFL with them (READ, the RC and RD reads). Button 1 binds 11.
3. R181 fits the location plan to the site on the plots of the IMC schedule, and keyRules and the overrides, 12,421 after PR 52, were measured on that numbering, while the first draft of this design numbered a new project by the three control points the sheet prints. The schedule belongs to the project, so the schedule fit numbers every build on both sides (R351, R352), and the control point fit of R387 is only a check that lists the faces it numbers otherwise (section 5). The IMC fit put the control points 0.265 to 0.305 m from their stated points (READ, locationPlan.json).
4. R211 puts the internal origin at the plot centre of the NBH, the keys share the Parsons position. Under Q068 button 1 sets the Parsons position from the file config names.
5. R212 makes a new shared parameter file, so new GUIDs, the keys hold the client's. Phase 4b reads the GUIDs from the key copies into config so button 1 writes the same ones. Until then the GUIDs of a new file differ from the keys and a schedule over both files does not join them.
6. R87 skips a plot already placed, button 4 moves a link it tagged when the plan moved it. R377 keeps the skip for every untagged link.
7. Place from plan runs links and floors as two steps of one button (R116), the mock makes them buttons 4 and 5, one transaction each, as R116 already gives each step its own.
8. The garden walls have no measured rule (R335 counts them), so button 7 places boundary walls only and lists the garden walls as not placed, while Q071 asks for the wall top rule.
9. shapely, a requirement of the Python checker, bundles GEOS under LGPLv2.1 (Q076).
10. R2 rounds to 3 decimals, and Python round() and C# Math.Round give another result on a tie such as 12.3455 (RAN, rounding_probe_pr49.txt). R385 gives both one rule on the decimal value and changes R2 in the same change, the Python outputs move by one unit of their last decimal on a tie (Q078). The level builders kept round() in 4a, since with R385 there 350 level items of the build proof lost their key, and take R385 since 4b with the level overrides measured again on it, 0 unexplained on every key (Q080).
11. majority.write_overrides writes only note, overrides and cannot, and plan_rules.load_overrides ignores use (READ), so Skip and Rule of the check table would be lost or not honoured. R386 changes both before step 12 is compared, done in 4b.
12. TypesByFile of Placer.cs reuses a link type by its file name (READ, l.296 to 331), so two files of one name in two folders would share a type. R377 matches by the full path.
13. SiteSteps.cs refuses a shared parameter file that is there already (READ, l.42 to 44), so a run that rolled back after writing it would block the next. Button 1 names its file by the run id and deletes it after (R373).
14. R212 binds the parameters from a new shared parameter file, so a site file it made holds them under the names of config with other GUIDs, and R85, R87 and R114 find a parameter by name (READ, RevitPlacerTarget.cs l.50 and l.166, RevitFloorTarget.cs l.66 and l.199). Button 1 stops on such a file with the list and binds nothing, and every tool parameter is read and written by its GUID (R373, R377, R378).
15. R40 reads a LandXML with no Units element and gives its linearUnit as None (READ, landxml_reader.py l.34 to 46). R388 refuses it, and R349 does on the C# side.

Questions opened by this design, the full text in steps/questions-closed.md:

- Q076. The Python checker packages: shapely is BSD-3-Clause and its wheels bundle GEOS under LGPLv2.1, numpy, pillow and pypdfium2 hold parts under other permissive licences, none ships with the add-in: keep them for the checker (recommended), or replace shapely.
- Q077. The worker: button 2 and the plan of button 3 run in the add-in's own worker process, CadReader.exe in C#, installed with the add-in and started and ended by the button, since the Road Layout read alone took 2,571 to 2,612 MiB and PdfPig needs a System.Memory Revit 2024 does not hold: accept (recommended), or read inside Revit with the resolver.
- Q078. Rounding: one rule for Python and C#, the 15 significant digit decimal of the value rounded half away from zero (R385), so 12.3455 gives 12.346 in both, and the Python outputs move by one unit of their last decimal on a tie: accept (recommended), or round half to even in both.

The design follows the recommended answers of Q068 (the Parsons position), Q017 (VD4 by Rev 9), Q057 (no ramp front fall), Q071 (the base, the line and the open front of the boundary walls, the top weak until the architect answers) and Q078 (half away from zero), and the state until an answer of Q066 (the Zone 1 yard of Zones 2 and 3). Q073 is answered (Bader, 2026-10-26): the Roads folder was read in full and holds no road level at a front corner no road surface covers, so such a plot gets no level and is listed with why (R344), none in NBH 012, and step 9 carries the list into the C#. Q082 asks for the road model of the 33 plots of the keys with none, an answer changes data, not code.
