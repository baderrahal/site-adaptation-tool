# Truth audit, 4 Nov 2026

Round TRUTH-4NOV-C. Read only, no code changed. The question: how much of what the managers saw was computed by the tool, and how much was copied from the past job.

Tags: READ a file with its path, RAN a count or a run, SEARCHED a search, CHOSEN a judgement of this audit, UNKNOWN not known. No real coordinate or level is written here.

## The answer

The managers are right on the main point.

1. **The tool cannot run today from raw inputs alone.** Step 2 stops without the past schedules, and step 4 stops without the data book file plots.csv, which is built from the same schedules (RAN, section 5).
2. **What the dry run showed was mostly past values on the levels.** On NBH 012, the override list changed 322 of 336 plots, with 1,115 values copied from the past job: 304 door levels, 302 FFL gaps, 206 CASE, 150 Foundation-Type, 113 ramp fronts, 11 mirrors, 10 ramp types, 5 steps, 5 RVT-Name, 5 Prototype-FFL, 4 inserts (RAN, plan_012.json of the dry run).
3. **Without that list the levels move a lot.** On the same 336 plots, FFL moves by over 5 mm on 232 plots, door level on 233, ramp slope on 301, steps on 17, mirror on 11 (RAN, section 5).
4. **The match with the past job proves little.** The list was made from the same past models it was compared with (READ steps/investigations/6b-data.md:9). All 7 NBHs were used to tune the rules and also to test them. None was held back (section 6).
5. **The dry run was scripted.** The kit opened with step 1 already done, on the past job's position file, in past job mode. The sheet gave the click order and the numbers to expect, from a rehearsal the same day (READ steps/dryrun-3nov.md, section 4).

What is real:

- Plot corners, generic floor outlines, ramp outlines, footprint rotation and mirror of the blocks, road levels and toposolid points are computed from CAD and LandXML on each run.
- The masses are computed from the prototype files.
- Button 1 on a blank new model takes position and survey point from its own CAD (READ SiteSources.cs:114-126). position_parsons.json is not needed there.
- A new project gets an empty override list and Rev 06 mode. It built 336 plots with 0 overrides on NBH 012 (RAN, truth_4nov, and steps/log.md). But the rules it uses were still measured on the past job, and it could only get that far on a read that needed the past schedules.

The wording "computed" in the plan hides this. A rule value measured on the past models reads as a rule, and a plan status of RECOMMENDED or MEASURED tells a reader nothing about where a number came from.

## 1. Table 1, files read at run time

Mark: raw input, or past output. For a past output: needed for the run, or only for compare. Code lines are under dotnet/SiteAdaptation.Common unless a path says otherwise. The proof is what the code does when the file is missing.

| Button | File | Folder or key | What it holds | Mark | Needed or compare |
|---|---|---|---|---|---|
| 1 | Baseplan dwg, villa blocks dwg, curbs dwg, LandXML | pane rows | plot outlines and the stated coordinate system | raw input | |
| 1 | cad_layers.json | config | layer names | raw input, names from this job's CAD | |
| 1 | position_parsons.json | addin.position.file, under the work root | position, survey point, levels of the past model | past output | Past job on a model with coordinates: needed unless the user switches source, Apply stays off (SiteSources.cs:305-312). Empty project: only for compare, the compare rows drop (SiteSources.cs:470-473) |
| 1 | rules/roshn-makkah.json, keyRules.site | rules | level names, steps, units, measured on 7 keys | past output | Needed in both paths, no levels stops (SetUpTarget.cs:134-137) |
| 1 | shared_parameters.xlsx | config | parameter names and GUIDs of the past links | past output | Needed (ParameterBook.cs:23) |
| 2 | Baseplan, villa blocks, Road Layout dwg, 6 LandXML | pane rows | shapes, blocks, curbs, road surfaces | raw input | |
| 2 | DLUS Rev 9 xlsx, Accommodation xlsx | pane rows | area key, setbacks, sales, villa type | raw input | |
| 2 | 01101-...-005000.pdf | pane row | location plan numbers | raw input, CHOSEN. A record calls it the Rev 9 location plan. It also prints control coordinates, and who made it is UNKNOWN | |
| 2 | 00101-PSN-IMC and IMD -XXX-SCH-TU-090000.xlsx | 01_Output/1.11 References | past level schedules | **past output** | **Needed.** RAN: "the input schedule:IMC is not on disk". They fit the location plan page to the site (LocationPlan.cs:1056), give the official number (PlotTable.cs:365, 392) and the neighbourhood |
| 2 | phase1_border.csv | data_book | Phase 1 ring | **past output**. Ring from a drawing, chosen by holding every schedule plot (python/data_book_border.py:51-65) | **Needed**, RAN: "the step plotTable needs the input border" |
| 2 | plots.csv | data_book | joined plot rows, with pastFfl, pastEntry, pastSteps, scheduleFile | **past output**. Built from the schedules and the past plan (python/data_book.py:246-249, 330-334) | **Needed**, RAN: "the step roadLevels needs the input plotsCsv" |
| 2 | location_plan/labels.csv | data_book | labels from the schedule fit | past output, by rule R181, writer not followed to its end | Needed for road levels, with plots.csv |
| 3 | read.json, read_plotTable.json | work folder, from step 2 | every row shown | past output by chain | Needed, stops (ReviewLoad.cs:56-64). Step 3 opens no past file itself and compares nothing (SEARCHED), though the guide says "against its sources" |
| 4 | config/overrides/roshn-makkah.json | config | 11,080 entries, 7,204 Parsons, 3,876 Native, 2,390 Parsons plots, plus 1,484 cannot lines (RAN) | **past output** | Past job mode: **needed**, RAN: "the override list holds no override of parsons". Rev 06: the file must exist, an empty one builds (RAN). Under Rev 06 a full list still applies (RAN, Q255) |
| 4 | rules/roshn-makkah.json, keyRules | rules | 37 rules measured on 7 Parsons keys (RAN) | **past output** | **Needed in both modes**, RAN: "the level profile parsons2024Rev6 holds none" |
| 4 | Other blocks of the rules file | rules | manual values, typologies | raw input by their source field, each value not traced | Needed |
| 4 | rules, steps.base_height | rules | base height by unit, from 747 schedule plots, not confirmed by the architect (READ) | past output | Needed (KeyLevels.cs:242) |
| 4 | floor_outlines.json | config | outlines and ramp type bands from the IMC past job | past output | Past job: needed only by a stop, no value read (CheckRun.cs:229, FloorOutlines.cs:21). Rev 06: rampTypes.bands read, decides ramp type. RAN: "the request names no input floorOutlines" |
| 4 | prototype_offsets.json | config | offsets from the IMC past job | past output | Stop only, no value read by a build (SEARCHED, CheckRun.cs:43 the only non test use). A needless stop |
| 4 | key_join.json, key reads | roles keyJoin, keyReads | past links and plot cuts | past output | **Only for compare.** Optional, the pane never names them (CheckRun.cs:299-322). So the check table compares with stored list values and not live with a past model |
| 4 | Baseplan and other reads of step 2, LandXML, 13 prototypes | work folder, pane rows | as step 2 | raw input through step 2 | The reads carry the schedule fit |
| 4 | site.json | work folder, from step 1 | survey, angle, levels | past output in past job, CAD and rules on an Empty project | Needed (CheckRun.cs:243-246) |
| 5 | plan_NBH.json | work folder | every floor value | past output in past job, mix in Rev 06 | Needed (FloorLoad.cs:126) |
| 5 | Top Surface-02 LandXML, floors.roadTopography | pane, config | toposolid road | raw input, the choice of surface is CHOSEN | Toposolid only |
| 5 | keyRules.yardOutline.groups | rules | 102 groups of stored yard loops (RAN) | **past output** | Past job: needed. Rev 06: no yard floor, loops not read (FloorOutlines.cs:553-565) |
| 6 | plan_NBH.json, override list | work folder, config | levels with gaps | past output | Needed. Button 6 computes no level, it sets floors to the plan (LevelsRun.cs:206-217). No level is read from a schedule or PDF at this step (SEARCHED) |
| 7 | prototypes_2024, 13 rvt | work root | villa models | raw input, CHOSEN with a note: tool upgrades of the 2021 files in the past job's Zone 02 folder, 13 of 13 on name and size (RAN). Whether a new team has them is UNKNOWN | Needed |
| 7 | masses folder | work root | mass per prototype | made by the tool from the prototypes | Remade if missing (MassFiles.cs:238) |
| 7 | massPartyLines | addin.lightMode | cut lines of 9 units, changes 9 of 13 masses (RAN) | UNKNOWN, config says measured on the whole project, no script found | Optional |
| 7 | shared_parameters.xlsx | config | as button 1 | past output | Needed |
| 8 | majority.wallTypes.boundary, keyRules wall rules, yardBelowFflM | config, rules | type BW, wall rules | past output | Needed. No past file opened by this button (READ WallLoad.cs, SEARCHED Walls.cs) |
| 9 | plots.csv, override list, keyRules, plan | as above | report rows | past output by chain | Needed, stops (ReportRun.cs:461-469). keyRules only fills a box |
| 9 | addin.report.fileName | config | 00101, PSN, ROSHN Makkah, P01, S0 | past output for the codes, P01 and S0 CHOSEN (Q450) | Only for the name |

## 2. Table 2, values the tool writes

Sources: computed from raw input, a rule, a user pick, copied from a past output. "Tuned" means the rule's number or choice was measured on the past job. Counts are NBH 012, the dry run plan (RAN), past job mode. Rev 06 here means an Empty project with no list.

| Value | Past job mode | Rev 06, new project | Class or key |
|---|---|---|---|
| Position | **Copied** from position_parsons.json | Computed from CAD, plot box centre (Q326) | SetUpTarget.ReadPosition, SiteFromCad.FromCad |
| Angle to north | **Copied** | A rule, coordinates.angleToTrueNorthDeg, not read from CAD | SitePosition.cs:61 |
| Survey point | **Copied** | East and north computed from CAD, height a rule tuned on the past job (Q327) | SiteFromCad.At |
| Levels and units of the site | **Copied** | A rule, tuned, measured on 7 keys, no raw input read | keyRules.site |
| Plot corners | Computed from baseplan | Same | BuildFace |
| Plot number | Mix: PDF label fitted on the past schedule, official number kept only if the schedule holds it | Same, the read has no other route | LocationPlan, PlotTable.cs:365 |
| Neighbourhood | Copied, schedule membership, then DLUS | Same | PlotTable.cs:373 |
| Villa type | Computed from the sales sheet, through the fitted number, or a user pick on a conflict | Same | typologyRev9, Q168 |
| Mirror | A rule, tuned, on raw block factors. 325 rule, **11 copied** | A rule, tuned, 0 copied | Hands.cs:496-507 |
| Rotation | A rule, tuned, on a computed front, share 0.9908. 0 copied in the plan of NBH 012 | Same | keyRules.linkRotation |
| Insert point | A rule, tuned: 15 stored fits per unit code on a computed face, share 0.9712. 332 rule, **4 copied** | Same 15 fits, right only with the same prototypes | keyRules.linkOrigin.byCode |
| FFL | A rule, tuned, share 0.7465 (not strong), then gap. **302 of 336 copied** | Same rule, 0 copied. Over 5 mm from the past on 232 plots | KeyLevels, heightM |
| Door level | A rule, tuned, share 0.088 (not strong), then gap. **304 of 336 copied** | Road plus slope by unit, slopes CHOSEN (Q228) | KeyLevels.EntryOf, DesignBasis.DoorEntry |
| Steps | A rule, tuned, share 0.9877, on FFL less door. 5 copied directly, more moved through the gaps | A rule, tuned | keyRules.steps |
| Ramp corner levels | Road computed from LandXML, door by rule. **113 fronts copied**, and backs follow door levels | Road and door computed, joints a rule (R515) | FloorOutlines.cs:551, rampFrontM |
| Generic floor outline | Computed from baseplan, pulled 2 mm (R517) | Same | FloorOutlines.cs:543 |
| Ramp outline | Computed from the front plus manual setback | Same | Fronts.cs:500 |
| Yard outline | **Copied: stored loops of the past job**, placed by the link and cut to the plot, on 331 plots | No yard floor | keyRules.yardOutline, BuildPlan.cs:524 |
| Mass outline | Computed from prototype walls and slab, limits CHOSEN | Same | MassFileSet.Make |
| Wall lines | Computed from baseplan edges. Which edges and open spans: a rule, tuned, wallOpen share 0.7409 | Same, with the inside move of R416 | Walls.OfPlans |
| Wall base and top | Rule on FFL, so past on copied plots. Top +1700 from the manual, it agrees with the keys only 0.3465 | Rule on Rev 06 FFL | keyRules.wallBase, wallTop |
| Toposolid | Computed from LandXML. Boundary a hull of the plan floors, under past job holding the yard loops. Choices CHOSEN by Code (Q473) | Same, without yards | RoadTopography.Plan |
| Plot-Number parameter | Computed | Same | LinkParameters |
| Prototype, HVAC, Number-of-Steps, Facade color and option | A rule, tuned, tables from the keys | Same | keyRules parameters |
| Foundation-Type | Rule 186, **copied 150** | Rule, share 0.6198 | keyRules, list |
| CASE | Rule 130, **copied 206** | Rule, share 0.2747 | keyRules, list |
| RVT-Name | Rule 331, copied 5 | Rule | keyRules, list |
| Parameter names and GUIDs | **Copied** from the past links | Same | shared_parameters.xlsx |
| Wall and floor type names | **Copied** names from the keys | Same, the tool makes them | addin.wallTypes, floors.types |
| User picks | Layers, surfaces, conflict picks, slopes, ticks | Same | ReadPicks, RampSlopes |

Two faults this table shows, by READ:

- BW wall thickness: button 1 only duplicates a type, no width is set. The manual's 200 mm (M14) is not applied (RevitSiteDocument.cs:238-241).
- The Summary box of the report prints past job shares on a new project as if they were of that job (ReportExcel.cs:894-905).

## 3. Config check

Marks are CHOSEN by this audit from each file's own notes, no new project was run. The install ships project.json, floor_outlines, prototype_offsets, rule_values and the rules file to a new project unchanged, and writes an empty override list (READ Install-SiteAdaptation.ps1, ShippedSet.cs:142). So the overrides stop at the door and the rest goes through.

| File and key | What it holds | How it was got | On a new project |
|---|---|---|---|
| overrides, overrides and cannot | 11,080 and 1,484 entries, 2,544 plots | python/majority.py against 11 past models (R272, R330) | **Wrong**, not shipped, but a repo run loads it |
| prototype_offsets, 4 blocks | 15 groups, 14 units, 16 plots, 6 keys | IMC past job (R94, R121 to R123) | **Wrong**, no build reads it |
| floor_outlines, generic, yard, rampFront | 51, 52, 4 | IMC past job (R135, R136) | **Wrong** |
| floor_outlines, rampTypes | bands cut between past medians | IMC past job | Needs checking. Decides ramp type under Rev 06 |
| area_codes | 7 and 4 code rows, 9 and 8 area rows | key copies (R301, R326) | **Wrong** |
| plot_numbers | shifts of IMC and IMD | "measured on the full run" | **Wrong** |
| road_surfaces, surfaces | 7 roles | this job | **Wrong**, a user pick. rampTypes names need checking |
| cad_layers, second_source | layer names | this job's CAD | Needs checking |
| rule_values | 9 rows | manual | Right |
| project.json drawings, schedules, pastJob, connect, answerKeys, pastAll | file names | this job | **Wrong** |
| project.json positionName, placeName, roadSurfaces, datumSurfaces | site names | this job | **Wrong** |
| project.json addin.position.file | position_parsons.json | past key | **Wrong**, and a run under CAD still needs the key named |
| project.json cadLinks.cutNeighbours | 4 plot numbers | this job | **Wrong** |
| project.json inputRoot, listener inputRoots | folders | this PC | **Wrong** on another PC |
| project.json blockClusters, unitPlotTypes, prototypeNames, patterns, type and level names | block, file and type names | this job | Needs checking |
| project.json majority, siteBuild shares and bands | 46 keys and more | tuned on the past job | Needs checking |
| project.json designBasis.mode | roshnPastJob, new project rev6 | CHOSEN | Right only if a new project takes newProjectMode, it does (ProjectModes.cs:54) |
| project.json status block | 323 values: 220 UNKNOWN, 67 Bader, 17 Code, 14 recommended, 5 document | R401 | 220 values have no stated source |
| rules typologies, clusters, slopes, plot, levels, foundation, rev6, ramp | manual values | Rev 06 manual | Right if the manual applies |
| rules typology_codes, scan shifts | client table, shifts | this job | Needs checking |
| rules steps.base_height | 6 values | 747 schedule plots, not confirmed by the architect | Needs checking, plainly past job |
| rules keyRules, 37 rules | default of every new build (Q050) | 7 Parsons keys (R331 to R335) | Needs checking. entry 0.088 and ffl 0.7465 are not strong |
| rules keyRules.linkOrigin.byCode | 15 fits | past models | **Wrong** unless the same prototypes. This table really places the links |
| rules keyRules.yardOutline.groups | 102 loops | past models | **Wrong** |
| rules native2022 | 15 heights | Native job | **Wrong**, reference only |
| plan_imc_past.json of Round 15a | 337 of 337 plots | built from the past model and schedule (steps/history/round-15a.md:106) | **Wrong**. Not in git, read by Python compare tools and by data_book.py to build plots.csv |

C# literals (SEARCHED): RRE_ZZ_SP names in LinkFields.cs and Placer.cs, the KEO file form in Hands.cs:88, layer defaults in LayerConfig.cs, and the shown line "28 of the IMC key" in WallsRun.cs:20, wrong on a new project. No NBH number or folder drives a value.

Totals of the 43 items the check listed: 24 wrong on a new project, 16 need checking, 3 right.

## 4. The dry run kit

SA-dryrun3nov.rvt and SA-dryrun3nov-done.rvt, 3 Nov, in C:\SA-demo, setup from the saved project ROSHN Makkah, NBH 012, mode roshnPastJob (RAN, projectMode in request_4 of the -done folder).

- **SA-dryrun3nov.rvt** comes from SA-demo-start.rvt with step 1 run at 15:41, so step 1 was never pressed live on it (RAN, write times). The sheet tells the presenter to say "step 1 set the position from the position file" (READ dryrun-3nov.md). Its work folder holds only the 3 files of step 1, last written 15:41 (RAN).
- **SA-dryrun3nov-done.rvt** is a plain copy of the kit after a full rehearsal, with its work folder: steps 1 to 8 between 14:01 and 14:41 (RAN, READ lane_b_check/p3nov_check/mkdone.py).

| Step | Kit | Fallback -done | Not computed live, if shown |
|---|---|---|---|
| 1 | Done in the file | Done 14:01 | Position from position_parsons.json, a past output |
| 2 | No trace of a press | Done 14:02, 14:08 | Read live if pressed, using the past schedules. Picks proposed from the saved project |
| 3 | No trace | Done | The script says Confirm all, no plot checked. Saved review on the fallback |
| 4 | No trace | Done 14:10 | Build live if pressed, with the override list, 1,115 values on 322 plots. Check rows show stored past values |
| 5 | No trace | Done 14:12 to 14:23 | Fallback floors. Yard outlines stored |
| 6 | No trace | Done 14:36 | Fallback levels |
| 7 | No trace | Done 14:39 | 13 masses ready and not remade in every case |
| 8 | No trace | Done 14:41 | Fallback walls |
| 9 | No trace | Pressed 18:13 and 18:14, a report from the 14:42 model | The file was not saved, it stays 14:57 |

Prepared on paper: a click order with waits, the words to say, the numbers to expect (1,003 floors, 336 villas, 778 walls, 485 warnings, 688 accepted), and a list of what not to do. Those numbers came from the rehearsal, so they were known before any live press (READ dryrun-3nov.md).

Records cannot settle these, UNKNOWN:

- Who saw what. No record names a manager (SEARCHED). I do not know if the managers saw this kit, the fallback, or the 1 Nov demo.
- If the kit was opened and pressed. Between 16:30 and 19:30 the only run files are the two step 9 presses on the fallback (RAN). A press on another model folder, or a run outside the searched folders, would not show. Revit journals were not read.
- The records disagree: steps/log.md says 1 to 9 ran twice, the sheet says 2 to 9 ran once and the second pass stopped at step 2 (Q413).
- 1 Nov demo: only step 2 traces near 11:00, and SA-demo-done.rvt was written at 04:07, before the demo (RAN). Steps 4 to 9 that day not searched in full.

Honest reading, CHOSEN: if the fallback was shown, every model result on screen was made hours before. If the kit was pressed, steps 2 to 9 were computed live, but on past schedules, a past position and a past override list, with known answers. Either way a watcher had good reason to call it staged.

## 5. What breaks with every past output removed

Run on copies under Documents\SiteAdaptation\truth_4nov, by the worker, the reader program of the tool, built from main at PR 183, NBH 012, with main's config and rules. C:\SA-demo\inputs hashed before and after each pass: 43 files, 0 changed (RAN). 01_Output and position were moved out of the copy. Revit not used, so button 1 and buttons 5 to 9 are by code reading only.

**Control.** Schedules present, position out: step 2 ran 42 s, step 4 ran in 21 s past job and 17 s Rev 06, 336 plots. So position_parsons.json is not read by the worker at steps 2 and 4, and no past file was opened from outside the copies (RAN, opened list of the answers). Its effect comes through site.json from button 1.

| Taken away | Step | What happened, the worker's words |
|---|---|---|
| Schedules, path left in the request | 2 | Refused: "the input schedule:IMC is not on disk" |
| Schedules, not named | 2 | Refused, in turn: controlCheck, controlFaces, locationFit, locationLabels, plotTable "needs the input schedule:*" |
| Those 5 steps also taken out | 2 | Stops at 31 s: "the location plan fit needs the schedule of IMC ... (R351)". No read file written. Other steps of the read depend on the fit |
| Border, plots.csv, labels.csv | 2 | Refused: plotTable "needs the input border", roadLevels "needs the input plotsCsv". With both steps taken out, the rest runs, no plot table |
| All past files | 2 | Same stops, ends at the fit. No read files |
| Read with no past file | 4 | Refused: "the input baseplanRead is not on disk". Both modes |
| plots.csv and labels.csv | 4 | Refused on every one of the 13 steps: "needs the input plotsCsv". Both modes |
| keyRules from the rules file | 4 | Refused, both modes: "the level profile parsons2024Rev6 holds none, the C# builds by keyRules alone (R354)" |
| floor_outlines and prototype_offsets | 4, Rev 06 | Refused: "the request names no input floorOutlines" |
| Override list emptied | 4, past job | Refused: "the override list holds no override of parsons" |
| Override list emptied | 4, Rev 06 | **Builds**, 336 plots, values below |
| site.json | 4 | Worker ends "done" with 13 step files and no plan. The add-in stops before this (CheckRun.cs:243-246) |

**Wrong values.** Rev 06 with the full list against Rev 06 with an empty one, NBH 012, plots with a number apart by over 5 mm or 0.005 (RAN):

| Field | Plots of 336 |
|---|---|
| Ramp slope percent | 301 |
| Door level | 233 |
| SSL | 233 |
| FFL | 232 |
| Generic floor height | 232 |
| Height above door | 116 |
| Ramp height offset | 79 |
| Steps | 17 |
| Rotation | 11 |
| Mirror | 11 |

An older record agrees, by a different method: FFL within 5 mm of the IMC schedule on 324 of 324 with the list and 94 of 324 without (READ 6b-data.md:61, 73). Across the 5 TU zones with no list: FFL within 5 mm on 716 of 1,759 plots (READ Q361). Which side is right on these plots is a design question, the rule may be right where the past differs. But the rule alone does not reproduce the past job, and 90% is the bar.

**Not tested, UNKNOWN:** the removal of keyRules past its first refusal, a run on a schedule made by a new team, and buttons 5 to 9 on a plan with no past value in Revit in this round. A new project run to step 5 with no list is on record (672 floors, PR 178, on a scratch config).

## 6. Zones and NBHs

Plots are from config/area_codes.json, sum 2,591 (READ). Overrides are per key (RAN).

| Zone | NBH, plots | Parsons key, Native | Tuned on | Tested on |
|---|---|---|---|---|
| 2 | 012, 337 | IMC, IMD 1.4 | Yes, most, reference key. 1,227 overrides | Past plan, parity, zone compare, 6b. The only NBH pressed through the pane on record |
| 2 | 013, 410 | IMD, IMC 1.3 | Yes, 1,444 | Parity, zone compare, 6b |
| 1 | 021, 432 | IMA, IMC 1.3 | Yes, no level reference, 518 | Parity, 6b |
| 1 | 022, 301 | IMB, IMD 1.4 | Yes, no level reference, 403 | Parity, 6b |
| 3 | 02, 324 | IME, IMA 1.1 | Yes, 1,033 | Parity, zone compare, 6b |
| 3 | 03, 490 | IMF, IMB 1.2 | Yes, 1,729 | Parity, zone compare, 6b |
| 4 | 01A, 297 | IMG, IMA 1.1, IMB 1.2 | Yes, 850 | Parity, zone compare, 6b |

**Every NBH tuned was also tested, none was held back** (SEARCHED). Parity and byte equal compare the tool with itself. 6b ran by test host, not by the pane, and did not test Rev 06 (READ 6b-revit.md).

Never used:

- NBH 01b, Zone 4: 72 blocks, 109 units in the DLUS (READ data-book.md:145), no sales row, no area_codes row, no hit in the records (SEARCHED).
- Whole project through the pane: stops at step 3 (Q417), time unknown (Q414).
- 90 plots the Parsons keys name and do not join to a face (a subtraction from area_codes.json).
- Any other project or phase: no record, so the tool has never met a project it was not tuned on (CHOSEN from SEARCHED).

## 7. Check of last night, PRs 165 to 182

All 18 are merged (RAN). "Not seen" means not seen in Revit. Read from PR bodies and steps/log.md, not rerun by this audit.

| PR | Asked | Merged | Not seen | Gap |
|---|---|---|---|---|
| 165 PARAM | Pane row, New project sheet, floors bound | R506 to R508 | Press by step row | A model keeps the old binding until a press of 1 |
| 166 KNOWN | Lane A known issues | R520, R521, Q376, half of Q397 | New override line | Q355 fixes 1 of 12. Copy offer of Q397 left |
| 167 guide | Guide after PR 165 | Records | Words come from PR 165 | None |
| 168 REPORT | Why the report failed, Q307, Q408, ISO name, BIM sheets | R550 to R555 | Final code not pressed then, Update window | **Why it failed stays UNKNOWN.** Log 105,411 matches, PR 105,433. That check compares two routes over the same data, not with any past schedule, so it says nothing on right or wrong |
| 169 MASS | Q391, no overlap over 1 mm | R530 | Button 7 on the fallback model | NBH 012 meets it. Whole project: 2 pairs and 11 plots over (Q470, Q471) |
| 170 RULES | Table of values, ramp limit changes the result | R541 | Table not opened at merge, later done | **Ramp limit shows no change under the past job** (Q419), wall top was the proof |
| 171 COORD | Button 1 on a model with no coordinates | R510 to R514, R523 | R523 stop on a real model | New project check has no sender in the add-in |
| 172 TEXTS | Texts of Lanes A and B | R542, R543 | R542 section (Q454) | No review by the speed rule. Q415 reopened |
| 173 | Lane C done | HEADSUP | Nothing to see | None |
| 174 COORD window | Window as seen in Revit | R510 badge, R512 order | Its two changes, R523, Pick surfaces | Q403 closed on Lane C's own check |
| 175 R541 records | Record Lane B's pass | Q440 closed | Report section, Rev 06, Browse time | Results not rerun by Lane C |
| 176 6b | 7 zones, Links, compare, HELP pass | R556 to R558 | **Pane buttons not pressed on this build, test host only.** No person looked at plots. Rev 6 on zones | ZC and ZD not rerun after R557. Built before RAMP |
| 177 | Lane B done | HEADSUP | Nothing to see | None |
| 178 RAMP | Shared corners within 5 mm, 443 overlaps gone, target 0 | R470, R515 to R518 | New project path with the shipped set, scratch config used | **Past job misses: 2 overlaps, 360 of 555 corners over 1 mm** (Q430, Q432). New project meets 0. Opens Q439 |
| 179 | Lane A done | HEADSUP | Nothing to see | None |
| 180 TOPO | Toposolid cut to the NBH, under 5 mm, tick on | R570 to R575 | Merged code on the named model with its floors | **Hull, not cut to the NBH** (Q473). **263 of 1,008 points over 5 mm** (Q472, Q475). Tall block, 783 floors overlap (Q474). Lines reach no window (Q476). Tick came in PR 183 |
| 181 | Lane D done | HEADSUP | Nothing to see | None |
| 182 | Correct Q439 text | Text says button 5 stops | Cites Lane D's run | Earlier text from code reading was wrong |

PR 183 (Road topography tick) came after this list: seen in the pane image, not in Revit.

**Q439, the step 5 stop.** SA-dryrun3nov-done.rvt on main's config: button 5 reads all 1,003 tagged floors as outline changed, places nothing and makes no toposolid (RAN by Lane D per PR 180, READ steps/questions-open.md).

- Cause: R517 of PR 178 pulls every floor 2 mm inside its plot in every mode, so the plan outline differs from the floors in the model. A changed outline is skipped and listed (FloorsRun.cs:476). With all skipped the button stops: "nothing of the scope is placed or already placed" (FloorsRun.cs:163).
- User sees: dry run place 0, skip 1,003. A real press rolls back with that message and up to 1,003 lines. How that window shows it is UNKNOWN.
- Scope: a fresh model on main is fine, 1,003 floors placed (RAN by Lane D). Other models built before the pull should do the same, by the same code (READ, not RAN).
- Quieter fault, READ not RAN: on any model, if the plan changes the outline of some plots, those floors are skipped and left off plan, the others stand, and a real press shows those lines on no window (Q476).
- So the fallback of the dry run no longer works on main. Limit: PR 180 says the merged code was not pressed on that model, so the exact build of Lane D's run is UNKNOWN.

## 8. What must change so the tool runs from raw inputs only

| # | Change | Size |
|---|---|---|
| 1 | Step 2 reads with no past schedule: fit the location plan on control points or on labels against baseplan faces, take the official number from the DLUS or the location plan, drop the rule that a schedule row must hold levels (LocationPlan.cs:1464) | Large |
| 2 | Remove the need for plots.csv, labels.csv and phase1_border.csv: step 4 builds from the plot table of step 2, border from a CAD ring the team picks | Large |
| 3 | A rule set a new project can stand on: split keyRules into manual rules and past job measures, mark each, and give each past measure a manual or team value. FFL, door level and yard first | Large |
| 4 | Link fits from the prototype, not stored: measure insert offsets from the prototype files, so a new prototype set works (linkOrigin.byCode) | Medium |
| 5 | Past job mode and its override list become an optional compare: Rev 06 applies no past entry (Q255), the override list holds team edits only | Medium |
| 6 | Hold out test: retune on some zones, run on the others, report the score. Or get a project the tool has not seen | Medium |
| 7 | Remove needless stops: prototype_offsets.json, floor_outlines.json past values, addin.position.file under CAD | Small |
| 8 | A shipped set with no past names: empty site names, drawings, schedules, type names, shifts, and New project asks for them (Q379) | Medium |
| 9 | Set BW thickness from the manual, and take parameter GUIDs from a client sheet with its source stated | Small |
| 10 | Report: no past job shares on a new project, say in plain words on each sheet which values were past, rule or computed | Small |
| 11 | Plan status that tells the truth: a mark per value of computed, rule, pick or past, in the plan, the check table and the report | Medium |
| 12 | Button 5 replaces floors the tool placed when the outline changed (Q439) | Medium |

Order, CHOSEN: 7, 5 and 11 first, as they make the tool say the truth at small cost. Then 1 and 2, without which nothing else can be shown on a new project. Then 3, 4 and 6. Q441 asks Bader.

Until 1 and 2 are built, the honest line to the managers is: the tool reproduces and checks the past job, and places geometry from CAD for it. It does not yet design a new site by itself.

## How this was checked

- 13 read only helpers: 9 by button, one for section 3, one for sections 4 and 6, one for section 7, one for the questions. Their reports are the source of Tables 1 and 2 and sections 3, 4, 6, 7.
- Second check by another method: the counts of Table 2 from the dry run plan, the override list counted by script, the stops by a real worker run, and a sample opened by hand: ReadInputs.cs:159, CheckRun.cs:43 and 243, FloorsRun.cs:160 and 473, data_book.py:246 and 330, dryrun-3nov.md, the write times. All held.
- Conflicts found and corrected: a helper left the mode of the 3 Nov run UNKNOWN, the saved request says roshnPastJob (RAN). One helper read Q361 as 1,043 within 5 mm, its text says 1,043 over, so 716 within (READ). A helper's count of 336 rule and 0 copied on rotation is right for the override list, while the plan marks 11 rotation values with a different status, reason not traced.
- Not checked by hand: most line numbers of Tables 1 and 2, the rows of section 7, and the marks of section 3.
