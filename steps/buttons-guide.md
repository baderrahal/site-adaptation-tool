# Guide to the 9 buttons

For Bader and the team. Paths are those of Bader's laptop and the dry run model C:\SA-demo\SA-dryrun3nov.rvt. Times are from one rehearsal on NBH 012 (steps/dryrun-3nov.md), a person adds to them.

## The folders

| Short name | Folder | What it holds |
|---|---|---|
| Inputs | C:\SA-demo\inputs | Copies of the drawings, schedules and data book. No button writes here |
| Project files | C:\SA-demo\config, C:\SA-demo\rules | Project settings, parameter sheet, override list, rules |
| Work root | Documents\SiteAdaptation (named in %APPDATA%\SiteAdaptation\machine.json) | Prototypes, masses, saved projects, one work folder per model |
| Work folder | Work root\<model name>_<8 letters>, here SA-dryrun3nov_35a6f822 | Everything a run writes for one model |
| Install | %APPDATA%\Autodesk\Revit\Addins\2024\SiteAdaptationAddIn | The add-in, the Worker folder with CadReader.exe, the ShippedSet folder |
| Received files | C:\10- ROSHEN MECCA | Files as received, such as the manual. No input of the dry run comes from here |
| Original inputs | C:\02-Site Adaptation | Named as inputRoot in config/project.json and the shipped set. The kit's own settings file names Inputs instead |

In file names, <run> is the button and the time, such as 4_20261103-141015-492, and <NBH> the neighbourhood, such as 012.

## All buttons at a glance

| Name | What it does | Files it needs | Where they come from | What it writes and where | About how long |
|---|---|---|---|---|---|
| New project | Names the settings and files of this model | A saved project or Empty project, the five file rows | User picks, Work root\Saved projects, Install\ShippedSet | Setup in the model. Under Empty project, a copy of the set in Work root\New projects | Not known (Q414) |
| Input files | Holds the file picks of the model | Drawings, schedules, the road surface folder | User picks in Inputs | Nothing on disk. Picks stay in memory until the next real run (Q413) | A Browse took 30 to 60 s late in the rehearsal |
| Prototypes | Holds the folder of villa files | 13 Revit 2024 files | User picks Work root\prototypes_2024 | Nothing on disk, as Input files | As Input files |
| 1 Set up site | Sets position, levels, types and parameters | Project settings, rules, position file, parameter sheet | Project files and Inputs\position | Revit settings and types. site.json in the work folder | 8 s |
| 2 Read data | Reads the drawings and schedules | Input files, data book, past schedules | User picks, Inputs | read_<code> and scan_<run> in the work folder | 100 s, the picks window at 49 s |
| 3 Review data | The team checks each plot | The plot table of step 2 | Step 2 | review_<NBH>.json in the work folder | 9 s, then the team's time |
| 4 Check | Builds the plan and compares it with the past job | Step 2 reads, rules, override list, prototypes, site.json | Step 1, step 2, Project files, Prototypes | check_<run> in the work folder. Save overrides writes the override list | 23 s, then the team's time |
| 5 Floors and ramps | Places generic, yard and ramp floors | Plan of step 4 | Step 4 | Floors in Revit. floors_<NBH>.json in the work folder | 21 s and the Revit box |
| 6 Levels | Sets floor heights, writes the level Excel | Plan of step 4, floors of step 5 | Step 4, step 5, Project files | Floor heights in Revit. <NBH>_Levels_<profile>.xlsx in the work folder | 5 s |
| 7 Place villas | Places one villa on each plot at its FFL | Plan of step 4, mass files or prototypes | Step 4, Work root\masses | Revit links with parameters. place_<NBH>.json in the work folder | 18 s with masses ready |
| 8 Walls | Places boundary walls, no garden walls yet | Plan of step 4 | Step 4 | Walls in Revit. walls_<NBH>.json in the work folder | 5 s |
| 9 Report | Writes the report Excel | Plan and logs of step 4, counts of the model | Step 4, the model | <NBH>_Report.xlsx in the work folder | 5 s |

Only a Neighbourhood scope passes step 3 today, see 3 Review data (Q417). Times for New project and a whole project are not known (Q414).

## What every button shares

- The pane or the ribbon starts a button. Both run the same thing.
- Each button needs the one before it. 9 Report needs only 4 Check. A model with no setup reads Needs setup.
- Dry run first is on by default. A box then shows the counts with Apply and Cancel. Cancel is the default and changes nothing in the model. A dry run writes its files under the work folder in dry\<run>, they are not a real result.
- 4 Check has no dry run, so the tool unticks it when 4 runs and it stays off. Steps after 4 still ask Apply or Cancel, with the counts only and no list per plot.
- A real run records itself in the model, so a reopen shows the same rows.
- Every run writes run_<run>.log and run_timing_<run>.json in the work folder.
- A changed input file, or a changed scope, sends the steps after it back to Ready or Needs step.
- A model never saved has no work folder. The box reads: Save the project first, its work folder is named by its file.

## New project

**What it does.** Opens from the pane with Open New project. It says where the settings and files of this model come from, and saves that in the model. Save setup writes at once. A saved project is a start point. Its file holds settings but no file picks, so the rows still need Browse.

**Files it needs.**

| Row | What it holds | Who gives it | Used by | Where it comes from |
|---|---|---|---|---|
| Site drawing (CAD) | Plots, roads, curb lines. It fills Baseplan | Civil team | Buttons 1 to 4 | User picks, Inputs\ROSHN Makkah Baseplan.dwg |
| Plot list (Excel) | Plot, code and cluster. It fills the Sales schedule | Sales team | Button 3 | User picks, Inputs\PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_(Rev9)_Accomodation_Schedule.xlsx |
| Villa levels (Excel) | Floor level and door level per villa type. Reads Comes later, no file is taken yet | Architect | Buttons 4 and 5 | Nothing today |
| Road levels (civil file) | Road surfaces. The row reads optional, but step 2 stops with no folder picked (Q416) | Civil team | Button 4 | User picks, Inputs\landxml |
| Parameter sheet (Excel) | The shared parameters | BIM team | Button 1 | The set: C:\SA-demo\config\shared_parameters.xlsx |

Start from also reads:

- A saved project: Work root\Saved projects\ROSHN Makkah.json. It names the project settings and rules in Project files, past job rules.
- Empty project, New project rules, Rev 06: Install\ShippedSet, which holds config\project.json, floor_outlines.json, prototype_offsets.json, cad_layers.json, shared_parameters.xlsx and rules\roshn-makkah.json.

**Before it.** Open and save the model. Empty project stops on a model never saved.

**What the user clicks.**

1. Pane, Open New project.
2. Pick Start from.
3. Browse each row, or Get template to send a team an empty sheet. Pick surfaces is active when the settings follow Rev 06, as Empty project does, and grey under the past job.
4. Check files. It shows what is found and a yellow box for what stops a button.
5. Save setup.

Boxes:

- The tool stops here, no saved project is chosen: pick one under Start from.
- A template refused because it would lie in the input folder: pick another folder.

**What it writes.** No element in Revit. The setup goes on Project Information. Empty project copies the set once to Work root\New projects\<model name>_<8 letters>, with an override list holding no entries. This happens only on a model with no settings yet. On a model that already has settings, Empty project keeps its old rules and override list, with no message (Q418).

**Limits today.**

- Q379 A new project on another PC needs the inputs of this job.
- Q394, Q395, Q397 Empty project with Update, modes and Save As.
- Q416 The road row reads optional, step 2 stops without it.
- Q418 Empty project on a model with settings keeps its old rules.
- The Shared parameters row reads Not chosen with an amber mark though button 1 finds the sheet (steps/dryrun-3nov.md).

## Input files and Prototypes rows

**What they do.** Show each file the tool reads, with size and a Found mark. Browse changes one.

**Files.**

| Row | File or folder in Inputs | What it holds |
|---|---|---|
| Baseplan | ROSHN Makkah Baseplan.dwg | Plot shapes and labels |
| Villa blocks | Makkah Building Footprint.dwg | Villa blocks and setbacks |
| Plot fronts (curb lines) | Makkah Phase 1_Road Layout.dwg | Curb lines. Read only, finds the front of each plot. No curb is built |
| Road surface | landxml folder | Road surfaces. Six are read, Top Surface-02 first |
| DLUS and sales, DLUS | TBD-PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_Rev9.0.xlsx | The DLUS schedule |
| DLUS and sales, sales | PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_(Rev9)_Accomodation_Schedule.xlsx | The sales schedule |
| Plot numbers | 01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf | The location plan with plot numbers |
| Prototypes | Work root\prototypes_2024 | 13 villa files, Revit 2024 |
| Shared parameters | C:\SA-demo\config\shared_parameters.xlsx | Taken from the settings, the row is not read by button 1 |

**Before it.** New project.

**What the user clicks.** Browse on a row, pick the file or folder. Do not Browse a prototype folder on the Desktop (Q381). Do not use a Revit 2021 folder. The pane warns that those files upgrade on each use.

**What it writes.** Nothing on disk. A pick stays in memory until the next real run of a button writes it. Run a button before you save and close.

**Limits today.**

- Q413 A model saved after picks and before step 1 loses them.
- Q372 When a road pick is kept.

## 1 Set up site

**What it does.** Makes this model ready. It sets units, position, survey point, project base point, Level 1 and Level 2, floor types, wall types and the shared parameters. It sets only what differs. The position comes from a position file today, not from CAD, as the settings say (Q415).

**Files it needs.**

| File | What it holds | Where it comes from | Folder |
|---|---|---|---|
| project.json | Project settings | The setup | C:\SA-demo\config |
| roshn-makkah.json | Rules and the site values | The setup | C:\SA-demo\rules |
| position_parsons.json | Site position | Settings | Inputs\position in the kit, Work root\round_addin\position in the shipped set |
| shared_parameters.xlsx | 23 rows, 11 marked Keep | Settings | C:\SA-demo\config |

**Before it.** New project. The Baseplan row picked.

**What the user clicks.** Run 1 Set up site. In the window:

1. Tick Current on each current file. Apply waits for the site drawing.
2. Check the two tables, What the project has now and What the tool will set.
3. Pick the Ramp slope. Default 0.5%.
4. Dry run, then Apply.

Boxes: No site drawing in the setup, pick the Baseplan in the pane first. Nothing differs: Apply only marks the step done.

**What it writes.**

- Revit: Level 1 and Level 2 with a floor plan each, floor types Generic 150mm, yard, Ramps 0.5%, Ramps 2%, Flat Ramps, wall types BW and Generic Wall-150mm_ Garden, 11 shared parameters.
- Work folder: site.json. A shared parameter text file is made for the run and removed.

**Limits today.**

- Q415 The pane says from CAD, the position comes from the file.
- Q336 Group of the link parameters.
- Q379 Another PC needs its own inputs.

## 2 Read data

**What it does.** The helper program CadReader.exe reads every input outside Revit. First it scans the files and shows What the tool read, where each value comes from and what conflicts. Then it reads them in full. No element changes in Revit. A read of unchanged files is reused.

**Files it needs.**

| File | Where it comes from | Folder |
|---|---|---|
| The input rows above, except Prototypes for the read | User picks | Inputs |
| cad_layers.json, the layer list | Settings | C:\SA-demo\config |
| 00101-PSN-IMC-XXX-SCH-TU-090000.xlsx, 00101-PSN-IMD-XXX-SCH-TU-090000.xlsx, past schedules | Settings | Inputs\01_Output\1.11 References |
| plots.csv, location_plan\labels.csv, phase1_border.csv, the data book | Settings | Inputs\data_book |
| The prototypes, for the scan | User picks | Work root\prototypes_2024 |

**Before it.** Step 1. All input rows picked.

**What the user clicks.**

1. Run 2 Read data.
2. Apply on the dry run box.
3. In What the tool read: Use the proposed for all, then Use these picks.
4. After it, pick the Scope: Whole project, Neighbourhood or Plot list. Step 3 takes Neighbourhood only. The list is empty until step 2 ends, so do not press Neighbourhood before it.

Boxes: a missing file names it, for example needs Villa blocks, the file is not chosen. Cancel in What the tool read stops the step. Scope stop: change the scope and run it again, the read is reused.

**What it writes.** Work folder only: read_<code>\ with read_baseplan.json, read_villaBlocks.json, read_curbs.json, read_roadSurfaces.json, read_workbooks.json, read_locationPlan.json, read_plotTable.json, picks.json, read.json and a steps folder. scan_<run>\scan.json. request_2, answer_2 and worker_2 files.

**Limits today.**

- Q306 A neighbourhood picked after the first read.
- Q345 to Q349 Conflict picks.

## 3 Review data

**What it does.** Shows every plot of the scope against its sources. The team marks each one. Nothing changes in Revit.

**Files it needs.** read_plotTable.json of step 2 in the work folder, and the last review_<NBH>.json if there is one.

**Before it.** Step 2. Scope on Neighbourhood. It shows one NBH, so Whole project or Plot list stops here with: Choose an NBH in Scope of the pane. 4 Check needs this step, so those scopes do not go on past it.

**What the user clicks.**

1. Run 3 Review data.
2. Ok or Wrong per row, or Confirm all proposed.
3. Save review.
4. Close.

Zoom to plot and Export to Excel are there if needed. Boxes: closing unsaved asks Save your review? with Save, Don't save and Cancel. After a scope change the pane goes back to Run 2, press it, the read is reused.

**What it writes.** review_<NBH>.json in the work folder. An export goes where the user picks, and it is refused in the input folder.

**Limits today.**

- Q417 Whole project and Plot list stop here.
- Q355 A Wrong on a plot of two numbers.
- Q300 Plots with no cluster proposal.
- Q305 Hide Confirmed under the past job.

## 4 Check

**What it does.** Builds the plan of the scope and compares it with the past job. It shows each plot where the tool and the past job differ. The team picks Rule, Past or Skip. Nothing changes in Revit. Every later button builds from this plan.

**Files it needs.**

| File | Where it comes from | Folder |
|---|---|---|
| read_baseplan, read_villaBlocks, read_curbs, read_workbooks | Step 2 | Work folder |
| site.json | Step 1 | Work folder |
| project.json, floor_outlines.json, prototype_offsets.json | Settings | C:\SA-demo\config |
| roshn-makkah.json | Setup | C:\SA-demo\rules |
| roshn-makkah.json, the override list | Beside the settings | C:\SA-demo\config\overrides |
| The six road surfaces, plots.csv, labels.csv | Step 2 inputs | Inputs |
| 13 prototypes | User picks | Work root\prototypes_2024 |

**Before it.** Steps 1 to 3.

**What the user clicks.**

1. Run 4 Check.
2. Apply.
3. When 4 Check opens, pick a row, see why, then Rule, Past or Skip.
4. Save overrides if anything changed.
5. Close the window, then Close the box.

**What it writes.** Work folder: check_<run>\ with plan_<NBH>.json, checks_<NBH>.json, check.json and a steps folder, and run_<run>.json. Save overrides writes C:\SA-demo\config\overrides\roshn-makkah.json.

**Limits today.**

- Q401 12 plots differ from the past FFL.
- Q402 Plots 2301 and 2302 read VD4.
- Q386 Override list changes leave steps Done.
- Q382 Step 4 builds on a site.json off the model.

## 5 Floors and ramps

**What it does.** Places the generic, yard and ramp floor of each plot in the scope, and fills the Plot parameter on them. Dry run on NBH 012: 1,003 floors, 0 refused.

**Files it needs.** plan_<NBH>.json of step 4 in the work folder. The floor types of step 1.

**Before it.** Step 4.

**What the user clicks.**

1. Run 5 Floors and ramps.
2. Apply.
3. Wait for the Revit box, 485 warnings on NBH 012, then OK. Cancel undoes the step.

Do not click anywhere while the Revit box is open. A press waits behind it and fires after OK.

**What it writes.** Floors in Revit, in one step of Undo. Work folder: floors_<NBH>.json, floors_<NBH>_accepted.json, run_<run>.json.

**Limits today.**

- Q385 Accepted warnings return after a save.
- Q398 The 443 hairline floor overlaps at step 5.
- Q356 The off axis warning of a cut yard.
- Q354 Yards of a model already built.
- Q411 Floor parameters bound but never filled.

## 6 Levels

**What it does.** Sets the height of each generic and yard floor and each ramp corner of this tool to the plan, then writes the level Excel. It writes no villa parameter. If the Excel cannot be written the run is undone.

**Files it needs.** All from earlier: plan_<NBH>.json and step files of check_<run> in the work folder. project.json, the rules, the override list and plots.csv in their folders above.

**Before it.** Step 5.

**What the user clicks.** Run 6 Levels, then Apply. A box names the Excel if another program holds it open. Close that program and run again.

**What it writes.**

- Revit: floor heights.
- Work folder: <NBH>_Levels_<profile>.xlsx, here 012_Levels_parsons2024Rev6.xlsx, with sheets Levels, Clusters, Overrides used and Settings. levels_<run>.json, run_<run>.json.

**Limits today.**

- Q412 Level 1 is empty after the build, use the 3D view.
- Q401 12 plots differ from the past FFL.

## 7 Place villas

**What it does.** Places one villa on each plot at its FFL and writes its parameters. Under Masses, the default, each villa is a link to a light mass file of its prototype. Under Links, it is a link to the prototype itself. Dry run on NBH 012: 336 villas.

**Files it needs.**

| File | Where it comes from | Folder |
|---|---|---|
| plan_<NBH>.json | Step 4 | Work folder |
| 13 mass files | Earlier run, or made from the prototypes if stale | Work root\masses |
| 13 prototypes | User picks | Work root\prototypes_2024 |

**Before it.** Step 6. Keep Masses unless asked.

**What the user clicks.** Run 7 Place villas, then Apply. Box, when a link type must load or reload: Link types to load before Place villas, with Load and Cancel. Load clears the Undo list of Revit.

**What it writes.**

- Revit: 336 links on NBH 012 with 9 parameters: RRE_ZZ_SP_Plot-Number, Prototype, Number-of-Steps, Façade-Color, Façade-Design-Option, Prototype-Foundation-Type, Prototype-HVAC-Schedule-Type, RVT-Name and CASE. RRE_ZZ_SP_Prototype-FFL stays empty, by rule (Q332).
- Work folder: place_<NBH>.json, footprint_<NBH>.json.
- Work root\masses: a mass file when one is made. 8.7 s each from Revit 2024 files. The pane says 20 to 41 minutes from older files.

**Limits today.**

- Q277 Footprint block names unlike the unit.
- Q391 Twin masses overlap along one side.
- Q337 Swap back in the same model unproven.
- Q332 FFL bound but never written.

## 8 Walls

**What it does.** Places the boundary walls of each plot in the scope. Garden walls are not placed, they have no measured rule. Dry run on NBH 012: 778 walls.

**Files it needs.** plan_<NBH>.json of step 4 in the work folder. project.json for the BW wall pattern. The wall types of step 1.

**Before it.** Step 7.

**What the user clicks.** Run 8 Walls, then Apply.

**What it writes.** Boundary walls in Revit. walls_<NBH>.json in the work folder.

**Limits today.**

- Q266 Walls on edges other units meet. The wall rule is weak, 2,539 of 3,427 walls hold on the keys.
- Q259 Garden door place of each unit.
- Q358 Walls only the keys hold.
- Q357 Key walls with the face on the line.
- Q359 A C10 side wall beside a VL6.

## 9 Report

**What it does.** Counts what stands in the model, then writes the report Excel. It changes no element. The pane reads Run complete only when no step is left. After 4 then 9, it still points to 5.

**Files it needs.** All from earlier: plan_<NBH>.json, step files and run logs of steps 4, 5 and 6 in the work folder. The model itself. project.json, the rules, the override list and plots.csv in their folders above.

**Before it.** Step 4. Steps 5 to 8 give it the counts, so run them first.

**What the user clicks.** Run 9 Report, then Apply. Then Open folder, open <NBH>_Report.xlsx.

**What it writes.** Work folder: <NBH>_Report.xlsx with sheets Summary, Placed, Skipped, Overrides, Assumed, Issues, Accepted warnings, Rules and Settings. revit_counts_<run>.json, accepted_warnings_<run>_<NBH>.json.

**Limits today.**

- Q307 Summary shows [file name].rvt and other gaps, Placed holds no wall rows, and the pane counts differ from Summary.
- Q385 Accepted warnings return after a save.

## Files from start to end

| Order | File | Folder | Read or written by |
|---|---|---|---|
| Install | ShippedSet\ files, Worker\CadReader.exe | Install | Install writes. New project and steps 1, 2, 4, 6, 9 use them |
| Start | ROSHN Makkah.json, other saved projects | Work root\Saved projects | New project reads |
| Start | machine.json | %APPDATA%\SiteAdaptation | Every button reads, names the work root |
| New project | Project copy of the set | Work root\New projects | New project writes on Empty project. Later buttons read it |
| Input | project.json | C:\SA-demo\config | Every button reads |
| Input | roshn-makkah.json, rules | C:\SA-demo\rules | 1, 4, 6, 9 read |
| Input | shared_parameters.xlsx | C:\SA-demo\config | 1 reads |
| Input | position_parsons.json | Inputs\position | 1 reads |
| Input | ROSHN Makkah Baseplan.dwg | Inputs | 2 reads |
| Input | Makkah Building Footprint.dwg | Inputs | 2 reads |
| Input | Makkah Phase 1_Road Layout.dwg | Inputs | 2 reads |
| Input | Six surfaces of landxml | Inputs\landxml | 2 and 4 read |
| Input | DLUS and accommodation Excel files | Inputs | 2 reads |
| Input | 01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf | Inputs | 2 reads |
| Input | Two 00101 past schedules | Inputs\01_Output\1.11 References | 2 reads |
| Input | plots.csv, labels.csv, phase1_border.csv | Inputs\data_book | 2 reads all, 4 reads two, 6 and 9 read plots.csv |
| Input | cad_layers.json | C:\SA-demo\config | 2 reads |
| Input | floor_outlines.json, prototype_offsets.json | C:\SA-demo\config | 4 reads |
| Input | 13 prototypes | Work root\prototypes_2024 | 2 and 4 read, 7 under Links |
| Step 1 | site.json | Work folder | 1 writes. 4 reads |
| Step 2 | scan_<run>\scan.json | Work folder | 2 writes |
| Step 2 | read_<code>\, picks.json, read.json | Work folder | 2 writes. 3 and 4 read |
| Step 3 | review_<NBH>.json | Work folder | 3 writes and reads again |
| Step 4 | check_<run>\plan_<NBH>.json, checks_<NBH>.json, steps | Work folder | 4 writes. 5 to 9 read the plan |
| Step 4 | run_<run>.json | Work folder | 4 writes. 9 reads |
| Step 4 | roshn-makkah.json, override list | C:\SA-demo\config\overrides | 4 reads, Save overrides writes. 6 and 9 read |
| Step 5 | floors_<NBH>.json, floors_<NBH>_accepted.json, run_<run>.json | Work folder | 5 writes. 9 reads the log |
| Step 6 | <NBH>_Levels_<profile>.xlsx, levels_<run>.json, run_<run>.json | Work folder | 6 writes. 9 reads the log |
| Step 7 | Mass files | Work root\masses | 7 reads, writes when stale |
| Step 7 | place_<NBH>.json, footprint_<NBH>.json | Work folder | 7 writes and reads the last |
| Step 8 | walls_<NBH>.json | Work folder | 8 writes |
| Step 9 | revit_counts_<run>.json, accepted_warnings_<run>_<NBH>.json | Work folder | 9 writes and reads |
| Step 9 | <NBH>_Report.xlsx | Work folder | 9 writes. Update from report reads an edited copy |
| Every step | run_<run>.log, run_timing_<run>.json | Work folder | Each button writes |
| Dry run | dry\<run>\ | Work folder | A dry run writes, no later button reads them as a result |
