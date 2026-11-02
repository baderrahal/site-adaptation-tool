# FIX-1NOV, steps for Bader

The checks of the four rounds of 1 Nov 2026, FIX-1NOV-A, B, C and D, on Revit 2024. One action per step. Each part ends with what to look for.

Merged to main: fix/1nov-d (PR 126), fix/1nov-b (PR 127), fix/1nov-a (PR 128), fix/1nov-d2 (PR 129), fix/1nov-a2 (PR 130), fix/1nov-b-task6 (PR 131), fix/1nov-c (this sheet). Pending when this sheet was written, 2 Nov: fix/1nov-d3. A pending round's part says so, skip it until its PR is merged.

## 1. Pull, build and install

1. Close Revit 2024. The install refuses while it runs.
2. Open VS Code.
3. File, Open Folder, pick C:\Users\p003653k\OneDrive - Parsons Corp\Documents\GitHub\site-adaptation-tool, then Select Folder.
4. Terminal, New Terminal. A PowerShell terminal opens at the bottom, in the repo folder.
5. Run each line below on its own, in this order, and wait for the prompt to come back after each.

```
git switch main
git pull
dotnet build dotnet\SiteAdaptation.sln -c Release
C:\Users\p003653k\Tools\pwsh\pwsh.exe -File dotnet\SiteAdaptation.AddIn\Install-SiteAdaptation.ps1 -NoBuild
```

Look for: git pull ends on the merge of fix/1nov-c. The build ends with "0 Error(s)". The install shows no red line and ends with two lines, "Manifest ... SiteAdaptationAddIn_Revit2024.addin names ..." and "one SiteAdaptation.Common.dll in ... folders of the tool, SHA-256 ...". If it says "Revit 2024 is running", close Revit and run the last line again.

## 2. The model and the config

1. Copy C:\SA-demo\SA-demo-start.rvt to a new name, for example C:\SA-demo\SA-fix1nov.rvt. Every test below runs on that copy.
2. C:\SA-demo\config\project.json is from 691e1fc and lacks the keys of today (addin.lightMode linkFolder, massFolder, houseLayer, the coordinates block, addin.parameterSheet). Either pick the repo config C:\Users\p003653k\OneDrive - Parsons Corp\Documents\GitHub\site-adaptation-tool\config\project.json in the pane, or tell Lane B to refresh C:\SA-demo\config, it does so on your word only.
3. Start Revit 2024 and open the copy. If Revit asks about the Site Adaptation add-in, press Always Load.
4. In the pane untick Dry run first. With it ticked 4 Check refuses and 5 to 9 cannot be reached (Q195).

When 5 Floors and ramps stops on the Revit box "Highlighted floors overlap.", press OK. Cancel undoes the step (Q324).

Look for: the pane Site Adaptation on the right, the inputs of C:\SA-demo\inputs as steps/demo-fill.md lists them, scope Neighbourhood 012, Villa mode Masses.

## 3. Ramp joints, the check row (Lane A, PR 128)

1. Run buttons 1 to 4 on NBH 012 with the repo config of main. A config saved before FIX-1NOV-A has no floors.rampJoint, and its build stops with "floors.rampJoint.join must be true or false (R470)".
2. In the 4 Check window, pick the chip Floor.
3. Sort or filter the rows by the field rampJoint.

Look for: 1,110 rampJoint rows on NBH 012, 670 Fail and 440 Pass. Each reads like "corner 2 shared with plots 2290, gap 0.012 m, Fail, passes at most 0.001 m". The ramps themselves are unchanged, since R470 ships with floors.rampJoint.join false (Q340).

## 4. Mass links and one reload (Lane B, PR 127)

1. Run 1 to 6 as steps/demo-fill.md says.
2. Press 7 Place villas, Run, then Apply. The first run makes 13 mass files in Documents\SiteAdaptation\masses, 2 to 3 minutes each, so about 30 minutes the first time (Q322).
3. Manage, Manage Links, tab Revit.
4. Close Manage Links, select a placed villa on plot 2291, then one on plot 2289, and read Properties each time.
5. In the config, change addin.lightMode.solidHeightM from 3 to 4 and save the file.
6. Press 7 Place villas, Run, then Apply.

Look for: 13 link types named like the prototypes, RRE-WP-NSP-KEO-ARC-<code>-ZZ-MOD-<n>.rvt, all Loaded. 336 link instances. Plot 2291 shows the mirror, plot 2289 does not. footprint_012.json in the work folder with a row per prototype. After step 6 every DT3 instance is 4.0 m tall, 68 of 68 in Lane B's run. A second run of 1 to 7 with no change shows nothing to place and makes no mass file again.

## 5. Coordinates read back under Manage (Lane B, PR 127)

1. In the config set coordinates.source to "cad". The default is positionFile (Q328).
2. Press 1 Set up site, Run.
3. Press Apply.
4. Manage, Location, tab Location.
5. Manage, Coordinates, Specify Coordinates at Point or Report Shared Coordinates on the survey point.

Look for: in step 2 the right column says the Road Layout "states UTM84-37N, EPSG 32637" and every other file "states no coordinate system". Project Address Makkah. Active position "ROSHN Makkah", shared easting and northing those the Road Layout gives, elevation 0, angle 0, and the 3 control point rows with a gap of 0.000 mm.

## 6. The shared parameter Excel (Lane D, PR 126 and PR 129)

1. Open config\shared_parameters.xlsx of the repo in Excel, sheet Parameters.
2. Close Excel. In Revit press 1 Set up site, then Apply.
3. Manage, Project Parameters.
4. After 7 Place villas of part 4, select a placed villa and read Properties.

Look for: 11 rows with the columns Parameter, GUID, Data type, Group, Category, Filled by, Sample value, Keep. The Group column reads Text for the 10 link rows and Data for Plot (fix/1nov-d2, PR 129, Q336). Project Parameters lists the same 11 names, the 9 RRE_ZZ_SP_ link parameters, CASE, and Plot on Floors. RRE_ZZ_SP_RVT-Name reads "plot | code | steps | colour | option", for example "2441 | VD2 | 1 | XX | XX", and RRE_ZZ_SP_Prototype-FFL is empty.

## 7. No curb is built (Lane A, PR 128)

1. After 5 Floors and ramps, open Project Browser, Families, Floors, Floor.
2. Read the floor types listed.

Look for: only the types of floors.types of the config, Generic 150mm, yard, Ramps 0.5%, Ramps 2% and Flat Ramps. No curb type and no floor named curb. Curbs are read only, to find the plot front (R355). The pane row Curbs stays and still takes the Road Layout drawing.

## 8. The scanner picks used by button 2 (Lane A2, PR 130, R473, R474)

Not run in Revit yet. Lane B held the lock and the test host applies nothing, so this is the first check in Revit. The logic ran on copies through the calls of the add-in and the worker.

1. Press 2 Read data on NBH 012. The window What the tool read opens.
2. In Conflicts, on one plot, pick the second choice of a villa type row. Note the plot and the value.
3. Press Use these picks and wait for the read to end.
4. Open 3 Review data and find that plot.

Look for: that plot's Villa type is the value you picked, and every plot you left alone keeps its value. The file read.json in <work>\read_<hash> has "sources", with plotOutline MP_PLOT_LINE + MP_PLOT_SUBD from proposal, villaBlock and villaType from default unless a saved pick applies, and your plot under "conflicts". Known: the build still reads plots.csv, so a picked plot can differ from the build (Q349). A saved project that holds a villa block or villa type pick ends the read with 2 issue lines saying why (Q346).

## 9. Update from report (Lane A2, PR 130, R472)

Not run in Revit yet, the same as part 8.

1. After 4 Check to 9 Report on NBH 012, copy 012_Levels_parsons2024Rev6.xlsx and 012_Report.xlsx from the work folder.
2. In the copies, raise one FFL by 0.10 on sheet Levels.
3. On sheet Placed, change the Type of one link row to another villa type.
4. Press Update from report, Browse, and pick the two copies.
5. Read the tiles and the two rows, then press Apply 2 changes.
6. Run Update from report again with an Excel of an older build of the same NBH.

Look for: at step 5, Changes found 2, one row FFL with its rule check and one row Villa type, and the message after Apply saves 2 entries on those plots. There is no stop saying "the work folder holds no plan_012.json". At step 6, one line "<file> is of another build, its Plan build id is <n>, the plan of the model <n> (R402)", and nothing is listed. Known: a second Update before 4 Check runs again lists the same changes again, and nothing doubles (Q347).

## 10. Save setup writes the model at once (Lane C, R445, Q280)

1. In the pane press New project.
2. Under Start from pick ROSHN Makkah (saved), then press Save setup.
3. Without pressing any button of the ribbon, press Save on the Quick Access Toolbar, then close the project.
4. Open the project again.
5. Press Ctrl+Z once right after a Save setup, then look at the pane.

Look for: New project opens as before, its foot shows no line about a model. After step 4 the pane shows the config, rules and inputs of the saved project with no button run, the setup came back from the model. The undo list of Revit holds "Site Adaptation: Save setup". After step 5 the pane shows the setup from before that Save setup, the same as the model holds.

## 11. Links mode (Lane D, fix/1nov-d3)

Pending: this part comes with fix/1nov-d3, not merged when this sheet was written. Skip it until fix/1nov-d3 is on main, then read the Lane D line of steps/HEADSUP.md of the merged branch.

## 12. The demo check (Lane B, PR 131)

Lane B ran the demo by hand through the real windows, two passes on NBH 012. Its record is steps/demo-check.md.

1. Open steps/demo-check.md.
2. Read Go or no-go and the four points under it.
3. Decide Q308 and Q309, the stray export in C:\SA-demo\inputs and the refresh of the demo config.

Look for: GO for a live run of 1 to 9 on NBH 012 with Dry run off, once the four points are done. NO-GO with Dry run on until Q195 is fixed, the round after this one.

## Questions for Bader

Every open question of steps/questions-open.md on the merge of fix/1nov-c, 163 open, by topic, the oldest first, each with its recommended answer. [demo] and [new project] mark the ones a demo or a new project meets first. The full text is in steps/questions-open.md.

First for a demo or a new project: Q187, Q195, Q201, Q202, Q224, Q227, Q237, Q243, Q254, Q275, Q281, Q282, Q286, Q293, Q296, Q308, Q309, Q320, Q322, Q324, Q325, Q328, Q335.

### Levels, ramps and floors

- Q151 Q075 moved without the majority run. Recommended: accept the move. The run with --replace native comes when the Native reads are back, and it re-measures the other 3,876 native entries then.
- Q159 Q055 reads 11 more 1.4 plots off the ramp. Recommended: keep the one rule on all 16.
- Q162 Reads of entryM with heightM or rampFrontM. Recommended: keep the one rule on every value, as the sentence of Q119 reads.
- Q163 A yard that stands only by its mirror. Recommended: a mirror override marks every value of a yard or of a plot that would not stand without it, found by a build with and without the override as the turned walls of Q120 are.
- Q164 Levels count and the link Z of 7. Recommended: no. Levels stays the plots whose floors the last committed run of 6 set and read back within 1 mm. A link off its FFL after 7 becomes a check of 7 Place villas once 5d writes its run log.
- Q172 heightM gap with an El @ Entry edit. Recommended: keep the gap to levelRule.ffl of the plan and measure the effect on the rebuilt plan in 6a.
- Q224 The mode the ROSHN config runs [demo]. Recommended: keep roshnPastJob for ROSHN Makkah, its answer keys are the past job, and rev6 for every new project.
- Q225 The level of a yard floor under Rev 6. Recommended: keep the yard floor off under rev6, the owner builds it (2.3.B).
- Q227 Civil file format and plot match [new project]. Recommended: a CSV with Plot No. and El @ Entry, the plot matched as text, a civil export with another layout converted before it is named.
- Q228 Default slopes of road plus slope. Recommended: keep these defaults, the user sets them per project.
- Q230 Value status of the Rev 6 values. Recommended: a valueStatus.fieldsRev6 overlay keyed as fields, El @ Entry and the ramp type to designBasis.doorLevel, the yard fields empty.
- Q231 Civil door level where no road level. Recommended: keep it so, the plot listed in noRoadLevel.
- Q232 yardGrade in plan.json. Recommended: add floors.yardGrade to plan.json under rev6 in the round that shows it in the Excel.
- Q233 Slope limit at 3 or 4 decimals. Recommended: keep the raw value at 3 decimals.
- Q234 Read step road levels under Rev 6. Recommended: leave the read as it is, it reads, the build chooses.
- Q255 The override list under rev6. Recommended: a rev6 build applies no override of a past job, only the hand edits of the team (byHand), each override left with why.
- Q256 The one road surface covers a quarter of ROSHN. Recommended: keep the one surface, a new project names its one finished road surface, ROSHN runs roshnPastJob (Q224).
- Q260 Outline change of a placed floor. Recommended: In 5, delete the tagged floor and place it again inside the transaction, listing the old UniqueId.
- Q262 Level Excel and the read back values. Recommended: The Levels sheet reads levels_<run id>.json and shows the level read back beside the plan.
- Q263 Levels count still at its level. Recommended: RevitCountsReader takes levels_<run id>.json of the last committed run of 6 and counts a plot only while its tagged floors stand within 1 mm of it.
- Q273 6 Levels on a floor moved by hand. Recommended: 6 lists a floor whose level differs from both its tag and the plan as changed by hand and leaves it, as the other buttons do, unless the user sets it to the plan.
- Q303 The slope limit in the split test. Recommended: test the steps alone and report the slope, R414 stays off since 0.5 is under majority.strongShare 0.90.
- Q304 The side of an uneven split. Recommended: count the third unit mark from the high road end, so the lone unit stands at the low end as on the keys.
- Q340 The level of a joint an override moves. Recommended: keep join false until a rule for a joint an override moves is chosen, then set join true.
- Q341 Back corners two ramps share. Recommended: leave back joints apart, listed as Fail rows, until a rule for the back of a ramp row is chosen.
- Q343 Passing joint rows in the check chips. Recommended: keep every row as asked, and count only Fail rows in the chips, a later change of R471 and the check table.
- Q344 Joints at the edge of a build. Recommended: keep the scope, each corner takes the road level at its own place, so the two builds agree wherever the road sample is the same point, and list edge joints later.

### Walls

- Q149 M14, the boundary wall of an irregular plot. Recommended: the PDF reading, the outer face of the wall on the plot limit and its 200 mm inside the plot, and the canvas text written so.
- Q150 M15, the foundation depth. Recommended: keep the canvas text, at least 1.20 m, as the check of 9 Report, since a foundation deeper than the design depth is no fault on site.
- Q259 Garden door place of each unit. Recommended: a read of the garden walls and doors inside the 13 ARC prototype files by a Revit probe, the door place per unit into the rules profile, then R417.
- Q265 Ends status of walls after Q115. Recommended: Set walls.ends to MEASURED weak by keyRules.wallOpen for the parted cluster walls and keep UNKNOWN for the rest, a change of config by Lane A.
- Q266 Walls on edges other units meet. Recommended: Measure in Phase 6 whether a villa stands on the edge, from the prototype base shapes of light mode, and leave the span open the same way.
- Q267 Base level of a boundary wall. Recommended: The level of floorSettings.levels.yard, the base offset to the plan base, as coded.
- Q268 Scope of a wall on two plots. Recommended: Any plot in the scope, as coded.
- Q269 Untagged BW wall on a plan wall. Recommended: Match by both ends, skip and list it, never change it, as coded.
- Q299 The front of a notched irregular plot. Recommended: check these 6 against the links of their keys in a later Lane A round, and add an override where the front is wrong.

### Plots, clusters, hand and mirror

- Q160 A plot list takes unit plots only. Recommended: keep it, the buttons place villas on unit plots alone.
- Q178 A computed mirror edit breaks the hand rule. Recommended: keep it, the team sees the break and decides.
- Q185 A plot list over two NBHs stops button 7. Recommended: yes, stop, the plan is per NBH.
- Q191 The plan name of a plot list. Recommended: the nbhId its plots share, else all.
- Q211 7 after a 4 Check on a plot list. Recommended: stop as now, 7 asks for a 4 Check on the NBH.
- Q212 4 Check stops 7 on another NBH. Recommended: keep as R377 says, one plan per newest 4 Check.
- Q226 The hand of a detached unit under Rev 6. Recommended: the team sets it in 3 Review data, the proposal stays empty with its reason.
- Q229 A row of attached units with no single cluster split. Recommended: the team sets the cluster in 3 Review data, the reason names the row.
- Q235 Hand table cover per unit code. Recommended: only the codes with a hand, the hand tables of keyRules.
- Q238 TH1-IM in the cluster sequences. Recommended: add the TH1-IM sequences, DP1 TH1-IM TH1-IM DP1 and those the manual table of 2.1.D allows, to clusters as data, no code change.
- Q239 Mirror of DP2 and DP4 with no cluster table. Recommended: take pair left and pair right of the hand alone table of the same file, the fallback plan_rules.hand_table makes since Q098.
- Q240 The 121 IMC plots of the audit. Recommended: record 116, detached plus no proposal, as the plots with no Rev 6 mirror.
- Q300 Plots with no cluster proposal. Recommended: list those plots too, with their reason, so buttons 5 to 8 skip them until the team sets their cluster by hand.

### Villas, masses and links

- Q192 Solid height of light mode. Recommended: 3.0 m, one storey.
- Q193 Footprint limit of the base against the CAD block. Recommended: 100 mm, measured on NBH 012 in 5d.
- Q194 Bind the RRE parameters on Generic Models in button 1. Recommended: yes, the parameter sheet gains Generic Models, until then a solid lists its plot number as an issue and its tag holds the plot.
- Q206 The base boundary of a prototype. Recommended: the outer loop of the floor of the lowest level by default, the element named in config, and the Revit run after 5c measures every candidate of the 13 prototypes against the CAD villa block of the same code and keeps the one closest on most.
- Q270 Where a prototype set upgrades now. Recommended: The set target goes under the work root as prototypes_2024\<discipline>, STR to prototypes_2024\STR.
- Q271 Python build takes the pane folder. Recommended: plan_from_book.py takes a --prototypes folder for parity runs, the config folder by default.
- Q275 First log step limit of an upgrade job [demo]. Recommended: An upgrade job takes a first log step limit of its own in config, 20 minutes, as an image job has its own in Q154, and the C10 file of the probe is deleted and made again.
- Q277 Footprint block names unlike the unit. Recommended: Keep choosing the block by position as R404 does now, never by its name.
- Q279 Mass base, slab edge or outer wall face. Recommended: Keep the slab edge as the base, it is read on all 13 and exact, and measure the wall face as a second figure where the walls close.
- Q320 Keep the solids path of light mode [demo]. Recommended: keep linkFolder solids as the fallback until the Revit run places the 13 mass files and reloads one, then remove it in a later round.
- Q321 Mass base from walls, third attempt stopped. Recommended: keep the built rule with the flag, a prober counts the location line and the yard walls of the 13 prototypes, then Bader picks: exact corners by line intersections, or the slab for all.
- Q322 Mass files made inside button 7 [demo]. Recommended: a separate pane command Make mass files with a loader and a count, run once per prototype set, button 7 only places, the test host step limit for it from config.

### Coordinates and site position

- Q205 The corner table coordinates of Review data. Recommended: X, Y in model coordinates, Shared coordinates the CAD value.
- Q325 A file that states no coordinate system [new project]. Recommended: keep it so, a file that states nothing is listed and does not stop.
- Q326 Shared origin at the plot box centre. Recommended: keep the centre of the plot box rounded to 1 m.
- Q327 Elevation of the shared origin from CAD. Recommended: Bader names a CAD source for the height, such as the lowest road level of the LandXML under the origin, until then 0 from config.
- Q328 Default source of the site position [demo]. Recommended: switch coordinates.source to cad after the runner's Revit run reads back the named position, the site location and the three control points, the levels and units to move into config in a later round.
- Q329 The rule that picks the control points. Recommended: keep this rule.

### Set up site and shared parameters

- Q157 The type button 1 copies new types from. Recommended: copy each new type from the type of the IMC answer key of the same name, its layers and widths read once into config by the read job, and stop when the key has none.
- Q158 Step 1 Done on a model already set up. Recommended: a run where nothing differs counts as Done for the step state and writes site.json and the Settings entity of R367 when they are missing, with no change to the model, and says so.
- Q331 Plot is no link parameter of the keys. Recommended: keep Plot as the 11th row, Category Floors, Filled by 5 Floors, since button 5 and the check of 4 tell a placed floor by it.
- Q332 FFL bound but never written. Recommended: as built, bound and empty.
- Q333 Categories beyond the link instances. Recommended: keep the categories of the keys, a user edits the Category cell to narrow them.
- Q334 An xlsx read inside Revit. Recommended: as built, for the parameter sheet alone.
- Q335 A parameter sheet picked in the pane [new project]. Recommended: as built, one sheet for every button, the pane row a stop until it names the sheet of config.
- Q336 Link parameter group is Text not Data. Recommended: Text, as the past links hold it.

### Scanner and reading the inputs

- Q201 Data book files on a PC with no Python [new project]. Recommended: config names them under the work root data_book, a missing one stops button 2, a later phase has the worker make them in C#.
- Q202 The schedule folder in config [new project]. Recommended: keep it in config.
- Q236 Label size in the PDF check. Recommended: no, pdfplumber and PdfPig words may differ in size.
- Q237 Folder of the Rev 9 workbooks [new project]. Recommended: a config key that names the folder of the workbooks of a new project.
- Q242 The reason of an input the new project check cannot read. Recommended: keep the one line, the user opens the file to see why.
- Q243 Layer pairs for plot outlines in the scan [new project]. Recommended: keep singles and pairs, with pairMinSegments in the profile.
- Q244 A tie in a scan proposal. Recommended: propose none on a tie, the user picks in the scanner window.
- Q245 The old and the new code of one row agree. Recommended: a check row, not a conflict.
- Q248 Villa blocks on one layer per type. Recommended: propose as one source every insert layer whose block names hold a code of R425.
- Q249 A sheet whose numbers shift by area. Recommended: where button 2 has read the location plan fit, join a sheet by the official number of the plot table, else by the CAD label and the shift.
- Q250 The pair trace on the Road Layout. Recommended: trace the pairs of a file only with a layer that covers a plot alone, on ROSHN MP_PLOT_LINE (392 plots), which keeps the proposed pair.
- Q251 The centroid of today's plots.json. Recommended: take the centroid from the first point in PlotLabels too, in a round that reruns the reader parity.
- Q252 PDF tables and turned text in the scan. Recommended: R180 pairs only, a PDF table waits for a real one to measure.
- Q253 The header row of a scanned sheet. Recommended: the header is the row above the first row that joins a plot.
- Q254 The order and share of villa type sources [new project]. Recommended: keep the order blocks, texts, Excel, PDF and that share.
- Q257 Threads of the scan pair trace. Recommended: 4, or the cores less one on a PC that runs the scan alone.
- Q258 The time cap of the scan pair trace. Recommended: 360 s with the list, and the pairs of a file only with a layer that covers a plot alone (Q250), which the Road Layout has none of.
- Q288 Scan picks in the settings at version 1. Recommended: keep formatVersion 1, every PC runs the same add-in after the install of Part 1.
- Q289 Board rows the scan does not read. Recommended: keep the four until a scanner round reads mirror, survey point and road level, then add their rows.
- Q290 Curbs and road surface left out of the scan. Recommended: leave them out until round 4j-1b makes the Road Layout scan fast.
- Q291 Where the line Current files is shown. Recommended: the pane line now, a row of the Excel report in Part 2.
- Q295 Test host runs skip the scan. Recommended: keep it so until the test host can answer the window.
- Q345 The key of a conflict pick. Recommended: the saved form <plot>|<field>|<second source>, the docs and the two tests set to it by Lane C.
- Q346 A villa block pick of one layer. Recommended: keep it so, the villa block row of What the tool read names the drawing, not a layer, a Lane C and Lane D change.
- Q348 A conflict pick of the first source. Recommended: keep it, and let the window save only the conflicts a person changed, a Lane C change, then take both sources.
- Q349 A conflict pick and the build. Recommended: let the build take the plot table value where a conflict pick set it, a change to the plan logic in its own round, with parity.

### Update from report

- Q168 The villa type of Update from report. Recommended: keep saving it as an override field unit, and a build by keyRules applies it in Phase 6.
- Q169 Key of an update entry with none of its job. Recommended: take the answer key whose join holds the plot.
- Q170 updateChecks of manualRules. Recommended: M04 oneFflPerPiece, M08 stepsAtMost and stepHeights, M09 riser, M10 oneStepUnits.
- Q171 Which breaks untick a change. Recommended: only a manual rule break unticks, as the mock says. A break of a profile rule stays ticked, with the rule named.
- Q173 Level profile of Update from report. Recommended: the Level profile of the pane, once Lane B exposes it to the buttons.
- Q174 The plan Update from report reads. Recommended: the plan of the last real run of 4 Check, named by its run record, so the plan is the one the report came from.
- Q176 Plain names of the update rule checks. Recommended: keep the 9 names as drafted, listed in docs/design/14-ui-labels.md.
- Q177 Manual rule ids in Rule check. Recommended: show the id until 5c gives each manualRules entry a plain name, then the name with the id as tooltip.
- Q179 Guide line, side box and row mark of Update. Recommended: add a How to use line, such as "Each row is a cell the team changed. Untick a change to keep the model, then Apply.", and keep no side box and no selected row, since each row shows all it holds.
- Q347 A second Update before 4 Check runs again. Recommended: keep it, and mark a change whose override entry is already in the list as saved in the window, a later change.

### Check table and the rules

- Q165 Shape of the block manualRules. Recommended: a list with id, text, group, section and steps, the steps worded as in 13-workflow-changes.md, as R381 reads it, and 5c writes it so.
- Q166 Manual rule of a check row. Recommended: take the row's own manual rule ID once 5c writes it, else the IDs of its step as now.
- Q167 Manual rule on the other sheets. Recommended: keep it on the Summary and Issues lines.
- Q175 Run log format 9 Report reads. Recommended: keep this shape as the format R383 writes for every button.
- Q180 The status of designValues in the R401 block. Recommended: Bader confirms them CHOSEN by Bader, and the block says so.
- Q182 The 0.05 m side band of majority.side_neighbours. Recommended: move it to hands.sideBandM in config in the next code PR.
- Q184 Plain rule names and past phrases of the check table. Recommended: accept, Bader checks the wording in the shots.
- Q195 Dry run of button 4 [demo]. Recommended: a Start body runs the dry build later into dry\<run id>, Revit does not wait.
- Q196 addin.overrideFile in config. Recommended: add "config/overrides/roshn-makkah.json".
- Q197 key_join.json and the key reads as inputs. Recommended: two optional pane rows, Key join and Key reads.
- Q198 Past gaps of the 51 missing and 3 question rows. Recommended: take them from the past_all.csv rows of the keys.
- Q199 What the tool does, per manual rule. Recommended: Code drafts one line per rule from docs/rules and Bader checks them together.
- Q204 The manual rule of a Review data row. Recommended: M01 now, per column once manualRules is in the profile (M02 cluster, M13 front edge).
- Q209 The Manual column mixes section and revision. Recommended: show the section only, and the revision in the title.
- Q210 "(Q015)" in a past job text a user reads. Recommended: drop the id from the text a user reads.
- Q213 The use of an override entry under Skip. Recommended: accept.
- Q217 Follow every rule count, table against build chip. Recommended: the table takes the build's count.
- Q241 designBasis.doorLevel.source reads as a note in R401. Recommended: rename the key to sourceKind in config, Python and C# in the next Lane A round.
- Q301 The build takes the door level pick. Recommended: a Lane A round lets the build take doorLevelSource of the settings over config and adds the civil file to the request of 4 Check.
- Q302 Door level row under the past job. Recommended: show the row only when the mode is rev6.
- Q305 Hide Confirmed under the past job. Recommended: show the column Confirmed and the button Confirm all proposed only when the plan holds a proposal, as the second help line already does.
- Q307 What the report Summary shows after a run. Recommended: Lane A fills the top line and the Levels row, adds walls to Placed, and takes one issue count for pane and report.

### Pane, New project and the windows

- Q152 Mode of the last run of a button. Recommended: mode is how the real run was confirmed, afterDryRun when Apply followed a dry run, direct when the box was unticked.
- Q153 A lone copy of a tagged element. Recommended: keep it, list and leave the element and skip the item, so the tool never takes over an element it did not place.
- Q161 A changed step reruns every higher step. Recommended: keep it, the report reads what 5 to 8 placed.
- Q181 When the labels J11 and J14 change. Recommended: in the PR of the window that changes those labels.
- Q186 Close J2 as no work. Recommended: close it.
- Q187 Prefill of a new project from config, J16 [new project]. Recommended: build it in 5d as CHOSEN, the key map from inputRoot and the drawings named first.
- Q188 Load the entity on a session seen with no open event. Recommended: yes, in a later PR.
- Q189 Check table modal or modeless. Recommended: modal for now, modeless in 6c.
- Q200 Export to Excel of Review data and of Site Adaptation Rules. Recommended: the worker writes the xlsx (R350), the button asks the worker, for both windows.
- Q203 A worker end on a document not active. Recommended: the message at once in 5c, the wait of 02-folders.md in 5d.
- Q207 No foot and no Esc in the three windows. Recommended: add both as the spec says.
- Q208 The How to use line of Rules. Recommended: Code drafts one line, such as "Pick a rule to see what the tool does for it.", and Bader checks it.
- Q281 Board rows against the nine pane inputs [new project]. Recommended: keep the mapping, the other inputs stay in the pane.
- Q282 Where the project name is kept [new project]. Recommended: keep it read only, the name is the title of the model.
- Q284 Villa mode row the boards drop. Recommended: keep the row.
- Q285 Name the Rules row shows. Recommended: keep the project field as it is.
- Q286 Approve the four drafts of NEWPROJ-UI [demo]. Recommended: approve each as drafted, then each is copied into baseline/.
- Q287 Button messages that name a work file. Recommended: a later round rewords those messages by what the file holds.
- Q293 Approve the three drafts of SCAN-UI [demo]. Recommended: approve the three as drawn, the labels in docs/design/14-ui-labels.md.
- Q294 Rules chosen in Set up site, when used. Recommended: keep it so, the window says the choice is saved with the setup.
- Q296 A saved project holds no drawing path [demo]. Recommended: Keep it so, a saved project of one job must not point another job at its drawings, and the demo browses the five files once.
- Q306 A neighbourhood picked after the first read. Recommended: button 2 takes the project key as button 1 does, since the pick does not change what is read.
- Q308 The stray export in the demo inputs [demo]. Recommended: delete the file, and Lane C makes the export offer the work folder.
- Q309 Refresh the demo config and add-in [demo]. Recommended: refresh C:\SA-demo\config from main and reinstall main before a live run, then run it once more on a copy.
- Q324 The Revit warning box at step 5 [demo]. Recommended: the step takes these overlap warnings itself and lists them as issues, so no box stops a run.

### Test host and runs

- Q154 The freeze guard limit for an image job. Recommended: an image job takes the image step limit, testHost.imageStepMinutes, for the not responding check, every other run 5 minutes. Run-TestHost.ps1 does so since 5b and prints the limit, -NotRespondingMinutes overrides it.
- Q183 The time cap of a buttons job of the test host. Recommended: accept for the Revit run after 5c, and change it by the measured open and run times.
- Q222 The NBH 03 image of Q063 was not run. Recommended: Bader names the area and the level.
- Q223 The test host gives up after one refused raise of its phase event. Recommended: retry a refused raise on the next tick, a test host change in 5d.
- Q323 A keyCopy run refused by its guard. Recommended: copy the key under the name the job opens and let the guard skip that one file, with a test, in the next Lane B round.
